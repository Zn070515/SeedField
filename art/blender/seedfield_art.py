"""Shared Blender helpers for SeedField style-test assets.

This module is imported by the asset render scripts. It intentionally does not
auto-run a scene; each script decides which assets to build.
"""

from __future__ import annotations

import json
import math
from pathlib import Path

import bpy


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


def bevel(obj, width: float = 0.03, segments: int = 2) -> None:
    mod = obj.modifiers.new("Bevel", "BEVEL")
    mod.width = width
    mod.segments = segments


def cylinder(name, radius, depth, location, vertices=8, mat=None, rotation=0.0):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices,
        radius=radius,
        depth=depth,
        location=location,
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
    bevel(obj)
    return obj


def torus(name, major, minor, location, mat=None, segments=48):
    bpy.ops.mesh.primitive_torus_add(
        major_radius=major,
        minor_radius=minor,
        major_segments=segments,
        minor_segments=10,
        location=location,
    )
    obj = bpy.context.object
    obj.name = name
    if mat is not None:
        assign(obj, mat)
    return obj


def sphere(name, radius, location, mat=None):
    bpy.ops.mesh.primitive_uv_sphere_add(radius=radius, location=location)
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


def setup_lights(target_z: float, route: str) -> None:
    world = bpy.data.worlds.new("World")
    bpy.context.scene.world = world
    world.use_nodes = True
    background = world.node_tree.nodes["Background"]
    background.inputs["Color"].default_value = (0.004, 0.007, 0.012, 1.0)
    background.inputs["Strength"].default_value = 0.55

    target = bpy.data.objects.new("LightTarget", None)
    bpy.context.scene.collection.objects.link(target)
    target.location = (0, 0, target_z)

    if route == "topdown":
        lights = [
            ("Key", (5.0, -4.0, 10.0), 1100.0, 5.0, (1.0, 0.88, 0.74)),
            ("Fill", (-6.0, -5.0, 8.0), 700.0, 5.0, (0.45, 0.72, 1.0)),
            ("Rim", (0.0, 7.0, 7.0), 700.0, 4.0, (0.45, 0.88, 1.0)),
        ]
    else:
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


def setup_camera(route: str, ortho_scale: float, target_z: float = 0.0):
    rig = bpy.data.objects.new("CameraRig", None)
    bpy.context.scene.collection.objects.link(rig)
    rig.location = (0, 0, target_z)

    data = bpy.data.cameras.new("ART_CAMERA")
    data.type = "ORTHO"
    data.ortho_scale = ortho_scale
    camera = bpy.data.objects.new("ART_CAMERA", data)
    bpy.context.scene.collection.objects.link(camera)
    camera.parent = rig

    if route == "topdown":
        camera.location = (0, 0, 14.0)
    else:
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


def configure_render(size: int) -> None:
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 64
    scene.cycles.use_denoising = True
    scene.cycles.device = "CPU"
    scene.render.resolution_x = size
    scene.render.resolution_y = size
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Medium High Contrast"


def render_angle(rig, angle: int, output: Path) -> None:
    rig.rotation_euler[2] = math.radians(angle)
    bpy.context.view_layer.update()
    output.parent.mkdir(parents=True, exist_ok=True)
    bpy.context.scene.render.filepath = str(output)
    bpy.ops.render.render(write_still=True)


def write_meta(path: Path, payload: dict) -> None:
    path.write_text(
        json.dumps(payload, indent=2, ensure_ascii=False) + "\n",
        encoding="utf-8",
    )
