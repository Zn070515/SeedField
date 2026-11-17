"""Power-node style test v2, following the Flux concept silhouette.

The v2 design is a stepped industrial tower: octagonal tiers, copper coil bands,
a teal column, a floating white-hot core and short electric arcs. It is still a
greybox-plus-materials proof, not final art.

Usage:
  blender --background --factory-startup --python art/blender/render_power_node_v2.py -- \
      --output art/generated/power_node_v2
"""

from __future__ import annotations

import json
import math
import random
import sys
from pathlib import Path

import bpy

RESOLUTION = 512
ANGLES = [0, 90, 180, 270]


def arg_value(flag: str, default: str) -> str:
    argv = sys.argv
    if flag in argv:
        index = argv.index(flag)
        if index + 1 < len(argv):
            return argv[index + 1]
    return default


OUTPUT = Path(arg_value("--output", "art/generated/power_node_v2")).resolve()


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
        key = "Emission Color" if "Emission Color" in bsdf.inputs else "Emission"
        bsdf.inputs[key].default_value = (*emission, 1.0)
        bsdf.inputs["Emission Strength"].default_value = strength
    return mat


def assign(obj, mat) -> None:
    obj.data.materials.append(mat)


def bevel(obj, width: float = 0.035, segments: int = 2) -> None:
    mod = obj.modifiers.new("Bevel", "BEVEL")
    mod.width = width
    mod.segments = segments


def cylinder(name, radius, depth, z, vertices=8, mat=None, rotation=0.0):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices,
        radius=radius,
        depth=depth,
        location=(0, 0, z),
        rotation=(0, 0, rotation),
    )
    obj = bpy.context.object
    obj.name = name
    if mat is not None:
        assign(obj, mat)
    bevel(obj)
    return obj


def box(name, size, location, rotation=(0, 0, 0), mat=None):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = size
    obj.rotation_euler = rotation
    if mat is not None:
        assign(obj, mat)
    bevel(obj, 0.02, 2)
    return obj


def torus(name, major, minor, z, mat=None, segments=48):
    bpy.ops.mesh.primitive_torus_add(
        major_radius=major,
        minor_radius=minor,
        major_segments=segments,
        minor_segments=10,
        location=(0, 0, z),
    )
    obj = bpy.context.object
    obj.name = name
    if mat is not None:
        assign(obj, mat)
    return obj


def look_at(obj, target) -> None:
    constraint = obj.constraints.new("TRACK_TO")
    constraint.target = target
    constraint.track_axis = "TRACK_NEGATIVE_Z"
    constraint.up_axis = "UP_Y"


def build_node() -> dict:
    steel = material("Steel", (0.30, 0.34, 0.38), 0.85, 0.30)
    dark = material("DarkSteel", (0.08, 0.095, 0.115), 0.8, 0.38)
    rubber = material("Rubber", (0.012, 0.015, 0.02), 0.1, 0.75)
    copper = material("Copper", (0.78, 0.32, 0.09), 0.9, 0.22)
    teal = material("Teal", (0.03, 0.20, 0.22), 0.5, 0.3)
    cyan = material(
        "Cyan",
        (0.03, 0.22, 0.28),
        0.2,
        0.2,
        (0.05, 0.80, 1.0),
        5.0,
    )
    hot = material(
        "Hot",
        (0.7, 0.9, 1.0),
        0.0,
        0.15,
        (0.85, 0.98, 1.0),
        12.0,
    )
    amber = material(
        "Amber",
        (0.3, 0.14, 0.02),
        0.2,
        0.3,
        (1.0, 0.55, 0.10),
        4.0,
    )

    collection = bpy.data.collections.new("PowerNodeV2")
    bpy.context.scene.collection.children.link(collection)
    bpy.context.view_layer.active_layer_collection = (
        bpy.context.view_layer.layer_collection.children["PowerNodeV2"]
    )

    # Stepped base: the silhouette must remain a tower at 64px.
    cylinder("Tier1", 2.45, 0.55, 0.275, 8, dark, math.radians(22.5))
    cylinder("Tier1Trim", 2.25, 0.10, 0.58, 8, steel, math.radians(22.5))
    cylinder("Tier2", 2.00, 0.50, 0.82, 8, steel, math.radians(22.5))
    cylinder("Tier2Trim", 1.80, 0.09, 1.10, 8, dark, math.radians(22.5))
    cylinder("Tier3", 1.52, 0.45, 1.30, 8, steel, math.radians(22.5))

    # Copper coil bands and vertical ribs.
    for z, radius in ((0.44, 2.22), (0.72, 2.02), (1.02, 1.78), (1.34, 1.42)):
        torus(f"Coil{z}", radius, 0.075, z, copper, 32)
    for index in range(8):
        angle = math.radians(22.5 + index * 45)
        x = math.cos(angle) * 2.05
        y = math.sin(angle) * 2.05
        box(
            f"Rib{index}",
            (0.13, 0.30, 0.45),
            (x, y, 0.85),
            (0, 0, angle),
            copper,
        )

    # Central column and rings.
    cylinder("Column", 0.55, 1.35, 2.05, 24, teal)
    torus("ColumnRingLow", 0.58, 0.045, 1.55, amber, 32)
    torus("ColumnRingHigh", 0.58, 0.045, 2.55, amber, 32)
    cylinder("Head", 0.88, 0.48, 2.92, 6, steel)
    cylinder("HeadTrim", 0.74, 0.08, 3.18, 6, dark)
    cylinder("CoreDisk", 0.46, 0.07, 3.24, 24, cyan)
    torus("CoreHalo", 0.62, 0.05, 3.26, amber, 32)

    # Floating core.
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.26, location=(0, 0, 3.62))
    orb = bpy.context.object
    orb.name = "FloatingCore"
    assign(orb, hot)
    torus("OrbRing", 0.43, 0.035, 3.60, amber, 32)

    # Short deterministic electric arcs.
    random.seed(7)
    for arc_index in range(3):
        curve = bpy.data.curves.new(f"Arc{arc_index}", "CURVE")
        curve.dimensions = "3D"
        curve.bevel_depth = 0.035
        curve.bevel_resolution = 3
        spline = curve.splines.new("POLY")
        points = []
        angle = arc_index * 2.1 + 0.6
        radius = 0.85 + arc_index * 0.18
        for step in range(5):
            t = step / 4.0
            r = radius * (1.0 - 0.35 * t)
            points.append(
                (
                    math.cos(angle) * r + random.uniform(-0.06, 0.06),
                    math.sin(angle) * r + random.uniform(-0.06, 0.06),
                    3.55 - t * 0.70,
                )
            )
        spline.points.add(len(points) - 1)
        for point, coordinate in zip(spline.points, points):
            point.co = (*coordinate, 1.0)
        obj = bpy.data.objects.new(f"Arc{arc_index}", curve)
        bpy.context.scene.collection.objects.link(obj)
        curve.materials.append(cyan)

    return {
        "cyan": cyan,
        "hot": hot,
        "amber": amber,
        "copper": copper,
    }


def setup_world_lights() -> None:
    world = bpy.data.worlds.new("World")
    bpy.context.scene.world = world
    world.use_nodes = True
    background = world.node_tree.nodes["Background"]
    background.inputs["Color"].default_value = (0.004, 0.007, 0.012, 1.0)
    background.inputs["Strength"].default_value = 0.55

    target = bpy.data.objects.new("LightTarget", None)
    bpy.context.scene.collection.objects.link(target)
    target.location = (0, 0, 1.6)

    lights = [
        ("Key", (6.0, -6.5, 9.0), 1100.0, 5.0, (1.0, 0.86, 0.70)),
        ("Fill", (-7.0, -3.0, 5.0), 700.0, 4.5, (0.45, 0.72, 1.0)),
        ("Rim", (0.0, 7.0, 7.0), 1200.0, 4.0, (0.45, 0.88, 1.0)),
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


def setup_camera():
    rig = bpy.data.objects.new("CameraRig", None)
    bpy.context.scene.collection.objects.link(rig)

    data = bpy.data.cameras.new("Camera")
    data.type = "ORTHO"
    data.ortho_scale = 7.4
    camera = bpy.data.objects.new("Camera", data)
    bpy.context.scene.collection.objects.link(camera)
    camera.parent = rig
    elevation = math.radians(35.0)
    distance = 12.0
    camera.location = (
        0.0,
        -distance * math.cos(elevation),
        distance * math.sin(elevation),
    )
    look_at(camera, rig)
    bpy.context.scene.camera = camera
    return rig


def configure_render() -> None:
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 64
    scene.cycles.use_denoising = True
    scene.cycles.device = "CPU"
    scene.render.resolution_x = RESOLUTION
    scene.render.resolution_y = RESOLUTION
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Medium High Contrast"


def render_views(rig, materials) -> list[dict]:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    renders = []

    for angle in ANGLES:
        rig.rotation_euler[2] = math.radians(angle)
        bpy.context.view_layer.update()
        path = OUTPUT / f"power_node_v2_{angle:03d}.png"
        scene.render.filepath = str(path)
        bpy.ops.render.render(write_still=True)
        renders.append({"view": angle, "file": path.name})

    for key in ("cyan", "hot"):
        materials[key].node_tree.nodes["Principled BSDF"].inputs[
            "Emission Strength"
        ].default_value = 0.03
    materials["amber"].node_tree.nodes["Principled BSDF"].inputs[
        "Emission Strength"
    ].default_value = 0.4
    rig.rotation_euler[2] = 0.0
    bpy.context.view_layer.update()
    offline = OUTPUT / "power_node_v2_off.png"
    scene.render.filepath = str(offline)
    bpy.ops.render.render(write_still=True)
    renders.append({"view": "off", "file": offline.name})
    return renders


def main() -> None:
    clear_scene()
    materials = build_node()
    setup_world_lights()
    rig = setup_camera()
    configure_render()
    renders = render_views(rig, materials)
    meta = {
        "asset": "power_node_v2",
        "status": "style-test greybox with materials; follows Flux concept",
        "resolution": RESOLUTION,
        "angles": ANGLES,
        "camera": {
            "type": "orthographic",
            "elevation_degrees": 35,
            "azimuth_degrees": ANGLES,
            "ortho_scale": 7.4,
        },
        "renders": renders,
        "source": "art/blender/render_power_node_v2.py",
        "notes": "No normal/emission mask yet; final art must be re-authored.",
    }
    (OUTPUT / "meta.json").write_text(json.dumps(meta, indent=2), encoding="utf-8")
    print(f"POWER_NODE_V2_RENDERED {OUTPUT}")


if __name__ == "__main__":
    main()
