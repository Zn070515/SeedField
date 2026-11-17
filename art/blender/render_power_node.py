"""Render the SeedField power-node style test.

Usage:
  blender --background --factory-startup --python art/blender/render_power_node.py -- \
      --output art/generated/power_node

Outputs: power_node_00.png ... power_node_03.png (four azimuths), power_node_off.png,
meta.json. The model is intentionally a greybox-plus-materials proof, not final art.
"""

from __future__ import annotations

import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector

RESOLUTION = 512
ANGLES = [0, 90, 180, 270]


def arg_value(flag: str, default: str) -> str:
    argv = sys.argv
    if flag in argv:
        index = argv.index(flag)
        if index + 1 < len(argv):
            return argv[index + 1]
    return default


OUTPUT = Path(arg_value("--output", "art/generated/power_node")).resolve()


def clear_scene() -> None:
    bpy.ops.wm.read_factory_settings(use_empty=True)


def material(
    name: str,
    color: tuple[float, float, float],
    metallic: float = 0.0,
    roughness: float = 0.5,
    emission: tuple[float, float, float] | None = None,
    strength: float = 0.0,
):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    if emission is not None:
        if "Emission Color" in bsdf.inputs:
            bsdf.inputs["Emission Color"].default_value = (*emission, 1.0)
        else:
            bsdf.inputs["Emission"].default_value = (*emission, 1.0)
        bsdf.inputs["Emission Strength"].default_value = strength
    return mat


def assign(obj, mat) -> None:
    obj.data.materials.append(mat)


def bevel(obj, width: float = 0.04, segments: int = 2) -> None:
    mod = obj.modifiers.new("Bevel", "BEVEL")
    mod.width = width
    mod.segments = segments


def cylinder(name, radius, depth, location, vertices=48, mat=None):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices,
        radius=radius,
        depth=depth,
        location=location,
    )
    obj = bpy.context.object
    obj.name = name
    if mat is not None:
        assign(obj, mat)
    return obj


def box(name, size, location, rotation=(0, 0, 0), mat=None):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = size
    obj.rotation_euler = rotation
    if mat is not None:
        assign(obj, mat)
    bevel(obj, 0.03, 2)
    return obj


def look_at(obj, target) -> None:
    constraint = obj.constraints.new("TRACK_TO")
    constraint.target = target
    constraint.track_axis = "TRACK_NEGATIVE_Z"
    constraint.up_axis = "UP_Y"


def build_power_node() -> dict:
    metal = material("Steel", (0.16, 0.19, 0.22), 0.85, 0.34)
    dark = material("DarkMetal", (0.035, 0.045, 0.055), 0.75, 0.42)
    rubber = material("Rubber", (0.012, 0.015, 0.02), 0.1, 0.75)
    copper = material("Copper", (0.42, 0.20, 0.08), 0.9, 0.28)
    cyan = material(
        "CyanCore",
        (0.03, 0.12, 0.14),
        0.2,
        0.2,
        (0.08, 0.72, 0.95),
        4.0,
    )
    white_hot = material(
        "WhiteHot",
        (0.5, 0.6, 0.6),
        0.0,
        0.2,
        (0.70, 0.95, 1.00),
        5.0,
    )
    amber = material(
        "Amber",
        (0.25, 0.12, 0.02),
        0.2,
        0.3,
        (1.00, 0.55, 0.10),
        5.0,
    )

    collection = bpy.data.collections.new("PowerNode")
    bpy.context.scene.collection.children.link(collection)
    bpy.context.view_layer.active_layer_collection = (
        bpy.context.view_layer.layer_collection.children["PowerNode"]
    )

    cylinder("Base", 2.25, 0.45, (0, 0, 0.225), 8, dark)
    cylinder("BaseTrim", 2.05, 0.08, (0, 0, 0.47), 8, metal)
    cylinder("Body", 1.55, 1.15, (0, 0, 1.05), 8, metal)
    cylinder("BodyBand", 1.62, 0.12, (0, 0, 1.42), 8, dark)

    # The core is a tower, not a flat disk: it must remain above the body so the
    # silhouette reads as "power" at thumbnail scale.
    cylinder("Core", 0.62, 1.55, (0, 0, 1.55), 24, cyan)
    cylinder("CoreHot", 0.16, 1.55, (0, 0, 1.55), 16, white_hot)
    cylinder("CoreCollar", 0.86, 0.16, (0, 0, 1.72), 24, dark)
    bpy.ops.mesh.primitive_torus_add(
        major_radius=0.92,
        minor_radius=0.08,
        major_segments=48,
        minor_segments=10,
        location=(0, 0, 1.80),
    )
    collar_ring = bpy.context.object
    collar_ring.name = "CoreRing"
    assign(collar_ring, amber)

    for index in range(4):
        angle = math.radians(45 + index * 90)
        x = math.cos(angle) * 1.85
        y = math.sin(angle) * 1.85
        box(
            f"Fin{index}",
            (0.16, 0.62, 0.95),
            (x, y, 1.05),
            (0, 0, angle),
            copper,
        )

    bpy.ops.mesh.primitive_torus_add(
        major_radius=1.45,
        minor_radius=0.07,
        major_segments=48,
        minor_segments=10,
        location=(0, 0, 1.48),
    )
    ring = bpy.context.object
    ring.name = "BodyRing"
    assign(ring, amber)

    for index in range(4):
        angle = math.radians(index * 90)
        x = math.cos(angle) * 2.0
        y = math.sin(angle) * 2.0
        cylinder(
            f"Port{index}",
            0.17,
            0.42,
            (x, y, 0.42),
            20,
            copper,
        ).rotation_euler[0] = math.radians(90)

    for index in range(8):
        angle = math.radians(index * 45 + 22.5)
        x = math.cos(angle) * 2.05
        y = math.sin(angle) * 2.05
        bpy.ops.mesh.primitive_uv_sphere_add(
            radius=0.07,
            location=(x, y, 0.52),
        )
        assign(bpy.context.object, metal)

    bpy.ops.mesh.primitive_torus_add(
        major_radius=1.55,
        minor_radius=0.05,
        major_segments=8,
        minor_segments=6,
        location=(0, 0, 0.50),
    )
    base_ring = bpy.context.object
    base_ring.name = "BaseIndicator"
    assign(base_ring, amber)

    return {"core": cyan, "hot": white_hot, "amber": amber}


def setup_world_lights() -> None:
    world = bpy.data.worlds.new("World")
    bpy.context.scene.world = world
    world.use_nodes = True
    background = world.node_tree.nodes["Background"]
    background.inputs["Color"].default_value = (0.004, 0.007, 0.012, 1.0)
    background.inputs["Strength"].default_value = 0.35

    target = bpy.data.objects.new("LightTarget", None)
    bpy.context.scene.collection.objects.link(target)
    target.location = (0, 0, 1.0)

    lights = [
        ("Key", (5.5, -5.5, 8.0), 900.0, 5.0, (1.0, 0.86, 0.70)),
        ("Fill", (-6.0, -2.5, 4.5), 280.0, 4.0, (0.40, 0.70, 1.0)),
        ("Rim", (0.0, 6.0, 6.0), 650.0, 3.5, (0.45, 0.85, 1.0)),
    ]
    for name, location, energy, size, color in lights:
        data = bpy.data.lights.new(name, "AREA")
        data.energy = energy
        data.shape = "DISK"
        data.size = size
        data.color = color
        light = bpy.data.objects.new(name, data)
        bpy.context.scene.collection.objects.link(light)
        light.location = location
        look_at(light, target)


def setup_camera() -> tuple:
    rig = bpy.data.objects.new("CameraRig", None)
    bpy.context.scene.collection.objects.link(rig)

    data = bpy.data.cameras.new("Camera")
    data.type = "ORTHO"
    data.ortho_scale = 6.2
    camera = bpy.data.objects.new("Camera", data)
    bpy.context.scene.collection.objects.link(camera)
    camera.parent = rig
    elevation = math.radians(35.0)
    distance = 10.0
    camera.location = (0.0, -distance * math.cos(elevation), distance * math.sin(elevation))
    look_at(camera, rig)
    bpy.context.scene.camera = camera
    return rig, camera


def configure_render() -> None:
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 64
    scene.cycles.use_denoising = True
    scene.cycles.device = "CPU"
    scene.render.resolution_x = RESOLUTION
    scene.render.resolution_y = RESOLUTION
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.image_settings.color_depth = "8"
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Medium High Contrast"


def render_views(rig, materials) -> list[dict]:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    renders = []

    for angle in ANGLES:
        rig.rotation_euler[2] = math.radians(angle)
        bpy.context.view_layer.update()
        path = OUTPUT / f"power_node_{angle:03d}.png"
        scene.render.filepath = str(path)
        bpy.ops.render.render(write_still=True)
        renders.append({"view": angle, "file": path.name})

    # Offline state: kill the core emission and dim the amber indicators.
    materials["core"].node_tree.nodes["Principled BSDF"].inputs[
        "Emission Strength"
    ].default_value = 0.04
    materials["hot"].node_tree.nodes["Principled BSDF"].inputs[
        "Emission Strength"
    ].default_value = 0.04
    materials["amber"].node_tree.nodes["Principled BSDF"].inputs[
        "Emission Strength"
    ].default_value = 0.5
    rig.rotation_euler[2] = 0.0
    bpy.context.view_layer.update()
    offline = OUTPUT / "power_node_off.png"
    scene.render.filepath = str(offline)
    bpy.ops.render.render(write_still=True)
    renders.append({"view": "off", "file": offline.name})

    return renders


def main() -> None:
    clear_scene()
    materials = build_power_node()
    setup_world_lights()
    rig, _ = setup_camera()
    configure_render()
    renders = render_views(rig, materials)
    meta = {
        "asset": "power_node",
        "status": "style-test greybox with materials",
        "resolution": RESOLUTION,
        "angles": ANGLES,
        "camera": {
            "type": "orthographic",
            "elevation_degrees": 35,
            "azimuth_degrees": ANGLES,
            "ortho_scale": 6.2,
        },
        "renders": renders,
        "source": "art/blender/render_power_node.py",
        "notes": "No normal/emission mask yet; final art must be re-authored.",
    }
    (OUTPUT / "meta.json").write_text(
        json.dumps(meta, indent=2),
        encoding="utf-8",
    )
    print(f"POWER_NODE_RENDERED {OUTPUT}")


if __name__ == "__main__":
    main()
