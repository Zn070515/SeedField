"""Render the top-down wasteland asset pack.

Usage:
  blender --background --factory-startup --python art/blender/render_wasteland_assets.py -- \
      --output game/SeedField.Game/Art/style_topdown --size 512 [--only waste_core,ore_copper]

All assets are strict 90-degree orthographic, transparent RGBA, 512x512. They are
style-test assets, not final art. Geometry is hand-authored procedural parts:
layered plates, bolts, pipes, hazard stripes, emissive status lights and rubble.
"""

from __future__ import annotations

import argparse
import math
import random
import sys
from pathlib import Path

import bpy
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
import seedfield_art as art  # noqa: E402


def args() -> argparse.Namespace:
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", required=True)
    parser.add_argument("--size", type=int, default=512)
    parser.add_argument("--only", default="")
    return parser.parse_args(argv)


def wasteland_material(
    name,
    color,
    metallic,
    roughness,
    grime=(0.10, 0.06, 0.035),
    scale=14.0,
    grime_strength=0.55,
    emission=None,
    strength=0.0,
):
    """Principled material with procedural grime, roughness variation and bump."""
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    bsdf = nodes.get("Principled BSDF")
    bsdf.inputs["Metallic"].default_value = metallic
    if emission is not None:
        key = "Emission Color" if "Emission Color" in bsdf.inputs else "Emission"
        bsdf.inputs[key].default_value = (*emission, 1.0)
        bsdf.inputs["Emission Strength"].default_value = strength

    noise = nodes.new("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = scale
    noise.inputs["Detail"].default_value = 8.0
    noise.inputs["Roughness"].default_value = 0.68

    ramp = nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].position = 0.32
    ramp.color_ramp.elements[0].color = (*grime, 1.0)
    ramp.color_ramp.elements[1].position = 0.78
    ramp.color_ramp.elements[1].color = (*color, 1.0)
    links.new(noise.outputs["Fac"], ramp.inputs["Fac"])

    mix = nodes.new("ShaderNodeMixRGB")
    mix.blend_type = "MULTIPLY"
    mix.inputs["Fac"].default_value = grime_strength
    mix.inputs["Color1"].default_value = (*color, 1.0)
    mix.inputs["Color2"].default_value = (1.0, 0.82, 0.62, 1.0)
    links.new(ramp.outputs["Color"], mix.inputs["Color1"])
    links.new(mix.outputs["Color"], bsdf.inputs["Base Color"])

    rough = nodes.new("ShaderNodeMapRange")
    rough.inputs["From Min"].default_value = 0.0
    rough.inputs["From Max"].default_value = 1.0
    rough.inputs["To Min"].default_value = max(0.0, roughness - 0.18)
    rough.inputs["To Max"].default_value = min(1.0, roughness + 0.18)
    links.new(noise.outputs["Fac"], rough.inputs["Value"])
    links.new(rough.outputs["Result"], bsdf.inputs["Roughness"])

    bump = nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = 0.24
    bump.inputs["Distance"].default_value = 0.035
    links.new(noise.outputs["Fac"], bump.inputs["Height"])
    links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    return mat


def materials() -> dict:
    return {
        "rust": wasteland_material(
            "Rust", (0.36, 0.15, 0.06), 0.55, 0.74,
            grime=(0.09, 0.045, 0.02), scale=11.0, grime_strength=0.55),
        "rust_dark": wasteland_material(
            "RustDark", (0.18, 0.07, 0.035), 0.5, 0.82,
            grime=(0.045, 0.028, 0.018), scale=16.0, grime_strength=0.6),
        "steel": wasteland_material(
            "Steel", (0.31, 0.32, 0.31), 0.85, 0.5,
            grime=(0.055, 0.05, 0.045), scale=13.0),
        "steel_dark": wasteland_material(
            "SteelDark", (0.09, 0.09, 0.09), 0.8, 0.62,
            grime=(0.03, 0.026, 0.022), scale=18.0),
        "concrete": wasteland_material(
            "Concrete", (0.28, 0.26, 0.22), 0.0, 0.94,
            grime=(0.12, 0.10, 0.075), scale=8.0, grime_strength=0.6),
        "sand": wasteland_material(
            "Sand", (0.43, 0.36, 0.25), 0.0, 0.96,
            grime=(0.24, 0.19, 0.12), scale=9.0, grime_strength=0.45),
        "copper": wasteland_material(
            "Copper", (0.56, 0.26, 0.08), 0.8, 0.44,
            grime=(0.22, 0.09, 0.03), scale=12.0),
        "lead": wasteland_material(
            "Lead", (0.33, 0.31, 0.38), 0.55, 0.64,
            grime=(0.11, 0.10, 0.12), scale=14.0),
        "titanium": wasteland_material(
            "Titanium", (0.28, 0.47, 0.45), 0.7, 0.52,
            grime=(0.09, 0.15, 0.14), scale=12.0),
        "coal": wasteland_material(
            "Coal", (0.035, 0.035, 0.04), 0.15, 0.9,
            grime=(0.015, 0.015, 0.018), scale=20.0),
        "hazard": wasteland_material(
            "Hazard", (0.62, 0.46, 0.08), 0.3, 0.62,
            grime=(0.20, 0.14, 0.04), scale=15.0),
        "rubber": wasteland_material(
            "Rubber", (0.02, 0.02, 0.022), 0.05, 0.92,
            grime=(0.012, 0.012, 0.014), scale=22.0),
        "amber": wasteland_material(
            "Amber", (0.45, 0.2, 0.03), 0.2, 0.4,
            grime=(0.2, 0.08, 0.02), scale=6.0,
            emission=(1.0, 0.46, 0.08), strength=4.5),
        "teal": wasteland_material(
            "Teal", (0.02, 0.24, 0.21), 0.2, 0.35,
            grime=(0.01, 0.08, 0.07), scale=6.0,
            emission=(0.08, 0.85, 0.72), strength=3.4),
        "red": wasteland_material(
            "Red", (0.32, 0.03, 0.02), 0.2, 0.42,
            grime=(0.12, 0.012, 0.008), scale=6.0,
            emission=(1.0, 0.07, 0.03), strength=4.5),
        "slag": wasteland_material(
            "Slag", (0.10, 0.07, 0.05), 0.2, 0.96,
            grime=(0.04, 0.03, 0.02), scale=10.0),
        "olive": wasteland_material(
            "OliveDrab", (0.20, 0.22, 0.11), 0.25, 0.82,
            grime=(0.08, 0.075, 0.045), scale=12.0, grime_strength=0.55),
        "ochre": wasteland_material(
            "OchrePaint", (0.42, 0.27, 0.10), 0.3, 0.78,
            grime=(0.14, 0.085, 0.035), scale=10.0, grime_strength=0.5),
        "dust": wasteland_material(
            "Dust", (0.38, 0.30, 0.21), 0.0, 0.98,
            grime=(0.22, 0.17, 0.11), scale=7.0, grime_strength=0.45),
    }


def setup_wasteland_lights(target_z: float) -> None:
    world = bpy.data.worlds.new("WasteWorld")
    bpy.context.scene.world = world
    world.use_nodes = True
    background = world.node_tree.nodes["Background"]
    background.inputs["Color"].default_value = (0.010, 0.008, 0.005, 1.0)
    background.inputs["Strength"].default_value = 0.5

    target = bpy.data.objects.new("LightTarget", None)
    bpy.context.scene.collection.objects.link(target)
    target.location = (0, 0, target_z)

    lights = [
        ("Key", (5.0, -4.0, 10.0), 1500.0, 5.0, (1.0, 0.80, 0.58)),
        ("Fill", (-6.0, -5.0, 8.0), 420.0, 6.0, (0.60, 0.48, 0.36)),
        ("Rim", (0.0, 7.0, 7.0), 780.0, 4.0, (0.50, 0.72, 0.85)),
        ("Top", (0.0, 0.0, 12.0), 500.0, 3.0, (1.0, 0.90, 0.75)),
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
        art.look_at(light, target)


def tube(name, a, b, radius, mat, vertices=8):
    start = Vector(a)
    end = Vector(b)
    mid = (start + end) * 0.5
    length = (end - start).length
    obj = art.cylinder(name, radius, length, mid, vertices, mat)
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = (end - start).to_track_quat("Z", "Y")
    return obj


def bolt_ring(prefix, cx, cy, z, rx, ry, count, mat, radius=0.035):
    for index in range(count):
        angle = math.tau * index / count
        art.cylinder(
            f"{prefix}Bolt{index}",
            radius,
            0.05,
            (cx + math.cos(angle) * rx, cy + math.sin(angle) * ry, z),
            6,
            mat,
        )


def hazard_strip(prefix, cx, cy, z, length, width, rotation, mats, segments=7):
    step = length / segments
    for index in range(segments):
        offset = -length / 2 + step * (index + 0.5)
        mat = mats["hazard"] if index % 2 == 0 else mats["steel_dark"]
        art.box(
            f"{prefix}Stripe{index}",
            (step * 0.92, width, 0.035),
            (
                cx + math.cos(rotation) * offset,
                cy + math.sin(rotation) * offset,
                z,
            ),
            (0, 0, rotation),
            mat,
        )


def grate(prefix, cx, cy, z, width, depth, frame_mat, bar_mat, bars=7):
    art.box(f"{prefix}GrateFrame", (width, depth, 0.05), (cx, cy, z), mat=frame_mat)
    for index in range(bars):
        offset = -width / 2 + width * (index + 0.5) / bars
        art.box(
            f"{prefix}GrateBar{index}",
            (width / bars * 0.42, depth * 0.9, 0.04),
            (cx + offset, cy, z + 0.03),
            mat=bar_mat,
        )


def panel_seams(prefix, cx, cy, z, width, depth, count, mat, along_x=True):
    for index in range(count):
        if along_x:
            y = cy - depth / 2 + depth * (index + 1) / (count + 1)
            art.box(
                f"{prefix}Seam{index}",
                (width, 0.018, 0.02),
                (cx, y, z),
                mat=mat,
            )
        else:
            x = cx - width / 2 + width * (index + 1) / (count + 1)
            art.box(
                f"{prefix}Seam{index}",
                (0.018, depth, 0.02),
                (x, cy, z),
                mat=mat,
            )


def rubble(prefix, cx, cy, spread, count, mats, seed, z=0.07):
    rng = random.Random(seed)
    for index in range(count):
        x = cx + rng.uniform(-spread, spread)
        y = cy + rng.uniform(-spread, spread)
        size = rng.uniform(0.05, 0.16)
        mat = mats["steel_dark"] if index % 3 else mats["rust"]
        if index % 4 == 0:
            art.cylinder(f"{prefix}Rubble{index}", size, 0.05, (x, y, z), 5, mat)
        else:
            art.box(
                f"{prefix}Rubble{index}",
                (size * 1.6, size, size * 0.7),
                (x, y, z),
                (0, 0, rng.uniform(0, math.tau)),
                mat,
            )


def build_core(m: dict) -> None:
    art.cylinder("Base", 1.72, 0.26, (0, 0, 0.13), 12, m["concrete"])
    art.cylinder("Deck", 1.46, 0.20, (0, 0, 0.34), 12, m["steel_dark"])
    art.torus("OuterRing", 1.28, 0.07, (0, 0, 0.46), m["rust"], 40)
    art.torus("InnerRing", 1.02, 0.05, (0, 0, 0.52), m["steel"], 36)
    art.cylinder("Pedestal", 0.56, 0.46, (0, 0, 0.58), 10, m["steel_dark"])
    art.torus("PedestalRing", 0.60, 0.045, (0, 0, 0.84), m["copper"], 28)
    art.sphere("Seed", 0.24, (0, 0, 0.94), m["teal"])
    art.cylinder("SeedCradle", 0.18, 0.16, (0, 0, 0.85), 8, m["steel"])
    for index in range(4):
        angle = math.tau * index / 4 + math.pi / 4
        tube(
            f"SeedCage{index}",
            (math.cos(angle) * 0.30, math.sin(angle) * 0.30, 0.82),
            (math.cos(angle) * 0.30, math.sin(angle) * 0.30, 1.10),
            0.018,
            m["steel"],
            6,
        )

    for index in range(6):
        angle = math.tau * index / 6
        x = math.cos(angle) * 1.10
        y = math.sin(angle) * 1.10
        art.box(
            f"Strut{index}",
            (0.14, 0.52, 0.42),
            (x, y, 0.60),
            (0, 0, angle),
            m["steel"],
        )
        tube(
            f"Cable{index}",
            (x * 0.72, y * 0.72, 0.58),
            (math.cos(angle) * 0.46, math.sin(angle) * 0.46, 0.86),
            0.022,
            m["copper"],
            6,
        )
        art.cylinder(
            f"Anchor{index}",
            0.10,
            0.16,
            (x * 1.36, y * 1.36, 0.24),
            6,
            m["hazard"],
        )

    for index in range(5):
        angle = math.tau * index / 5 + 0.3
        art.box(
            f"BrokenPlate{index}",
            (0.55, 0.28, 0.07),
            (math.cos(angle) * 1.38, math.sin(angle) * 1.38, 0.29),
            (0, 0, angle + 0.4),
            m["rust_dark"],
        )

    for index in range(8):
        angle = math.tau * index / 8 + 0.15
        art.box(
            f"RustStreak{index}",
            (0.46, 0.045, 0.018),
            (math.cos(angle) * 1.02, math.sin(angle) * 1.02, 0.445),
            (0, 0, angle),
            m["rust_dark"],
        )
    hazard_strip("West", -1.28, 0.0, 0.29, 1.0, 0.20, math.pi / 2, m, 6)
    hazard_strip("South", 0.0, -1.28, 0.29, 1.0, 0.20, 0.0, m, 6)
    panel_seams("Deck", 0, 0, 0.45, 2.5, 2.5, 4, m["steel_dark"])
    bolt_ring("Base", 0, 0, 0.27, 1.54, 1.54, 18, m["steel"])
    for index in range(4):
        angle = math.tau * index / 4 + math.pi / 4
        art.cylinder(
            f"Vent{index}",
            0.09,
            0.30,
            (math.cos(angle) * 1.18, math.sin(angle) * 1.18, 0.52),
            6,
            m["rust"],
        )
    rubble("Core", 0, 0, 1.55, 14, m, 11, 0.09)


def build_generator(m: dict) -> None:
    art.box("Skid", (2.72, 1.86, 0.16), (0, 0, 0.08), mat=m["steel_dark"])
    art.box("Frame", (2.30, 1.46, 0.18), (0, 0, 0.24), mat=m["steel"])
    art.box("Engine", (1.38, 1.04, 0.56), (-0.22, 0, 0.60), mat=m["olive"])
    art.box("TopPlate", (0.96, 0.58, 0.08), (-0.28, 0, 0.90), mat=m["steel_dark"])
    panel_seams("Engine", -0.28, 0, 0.94, 0.90, 0.52, 4, m["steel_dark"])
    for index in range(5):
        art.box(
            f"RustPatch{index}",
            (0.26, 0.10, 0.015),
            (-0.72 + index * 0.24, -0.34 + (index % 2) * 0.12, 0.94),
            (0, 0, index * 0.5),
            m["rust_dark"],
        )
    for index in range(4):
        art.cylinder(
            f"Piston{index}",
            0.10,
            0.18,
            (-0.70 + index * 0.30, 0.34, 0.94),
            8,
            m["copper"],
        )
        art.torus(
            f"PistonRing{index}",
            0.12,
            0.022,
            (-0.70 + index * 0.30, 0.34, 1.00),
            m["steel"],
            20,
        )

    art.cylinder("FanHousing", 0.46, 0.20, (0.82, 0, 0.64), 16, m["steel_dark"])
    art.cylinder("FanHub", 0.14, 0.16, (0.82, 0, 0.77), 10, m["rust"])
    for index in range(7):
        angle = math.tau * index / 7
        art.box(
            f"FanBlade{index}",
            (0.30, 0.055, 0.035),
            (0.82 + math.cos(angle) * 0.16, math.sin(angle) * 0.16, 0.78),
            (0, 0, angle),
            m["steel"],
        )
    art.torus("FanGuard", 0.48, 0.028, (0.82, 0, 0.80), m["steel"], 32)

    for index in range(2):
        x = 0.92
        y = -0.52 + index * 0.30
        art.cylinder(f"Stack{index}", 0.115, 1.10, (x, y, 0.72), 10, m["rust_dark"])
        art.torus(f"StackCap{index}", 0.14, 0.035, (x, y, 1.28), m["steel"], 20)
        art.cylinder(f"StackSoot{index}", 0.085, 0.03, (x, y, 1.31), 10, m["slag"])

    for index in range(2):
        x = -1.02
        y = -0.55 + index * 0.34
        art.cylinder(f"Drum{index}", 0.24, 0.62, (x, y, 0.42), 14, m["copper"])
        art.torus(f"DrumRing{index}", 0.25, 0.028, (x, y, 0.56), m["rust_dark"], 24)
    art.cylinder("DrumLid", 0.24, 0.05, (-1.02, 0.19, 0.75), 14, m["rust"])

    tube("PipeA", (-0.92, 0.30, 0.72), (0.72, 0.30, 0.72), 0.055, m["copper"], 8)
    tube("PipeB", (0.72, 0.30, 0.72), (0.72, -0.42, 0.72), 0.055, m["copper"], 8)
    art.torus("Valve", 0.14, 0.035, (0.30, 0.30, 0.76), m["hazard"], 20)
    art.box("GaugePanel", (0.36, 0.18, 0.24), (-0.96, 0.62, 0.52), mat=m["steel_dark"])
    for index in range(3):
        art.cylinder(
            f"Gauge{index}",
            0.035,
            0.03,
            (-1.04 + index * 0.08, 0.62, 0.66),
            8,
            m["amber"],
        )
    hazard_strip("Gen", 0, 0.84, 0.18, 2.2, 0.14, 0.0, m, 9)
    bolt_ring("Skid", 0, 0, 0.18, 1.20, 0.76, 14, m["steel"])
    rubble("Gen", 0.4, -0.9, 0.55, 10, m, 23, 0.1)


def build_relay(m: dict) -> None:
    art.cylinder("Pad", 1.16, 0.16, (0, 0, 0.08), 8, m["concrete"])
    art.cylinder("Hub", 0.34, 0.30, (0, 0, 0.28), 8, m["steel_dark"])
    for index in range(3):
        angle = math.tau * index / 3 + math.pi / 6
        art.box(
            f"Leg{index}",
            (0.16, 0.62, 0.18),
            (math.cos(angle) * 0.62, math.sin(angle) * 0.62, 0.20),
            (0, 0, angle),
            m["steel"],
        )
        art.cylinder(
            f"Foot{index}",
            0.12,
            0.10,
            (math.cos(angle) * 0.94, math.sin(angle) * 0.94, 0.13),
            6,
            m["rust"],
        )

    art.cylinder("Mast", 0.10, 0.80, (0, 0, 0.66), 10, m["steel"])
    art.cylinder("MastBase", 0.22, 0.14, (0, 0, 0.36), 8, m["rust"])
    dish = art.cylinder("Dish", 0.56, 0.07, (0.18, 0.08, 0.98), 18, m["steel"])
    dish.rotation_euler = (math.radians(18), math.radians(-12), 0)
    art.torus("DishRim", 0.58, 0.028, (0.18, 0.08, 1.02), m["copper"], 32)
    art.cylinder("Feed", 0.07, 0.30, (0.18, 0.08, 1.12), 8, m["steel_dark"])
    art.sphere("FeedTip", 0.07, (0.18, 0.08, 1.28), m["teal"])

    for index in range(3):
        angle = math.tau * index / 3 + 0.4
        tube(
            f"Antenna{index}",
            (0, 0, 1.02),
            (math.cos(angle) * 0.72, math.sin(angle) * 0.72, 1.22),
            0.018,
            m["steel"],
            6,
        )
        art.sphere(
            f"AntennaTip{index}",
            0.04,
            (math.cos(angle) * 0.72, math.sin(angle) * 0.72, 1.22),
            m["amber"],
        )

    art.torus("Spool", 0.26, 0.10, (-0.66, -0.52, 0.28), m["rust"], 24)
    art.cylinder("SpoolCore", 0.12, 0.22, (-0.66, -0.52, 0.28), 8, m["steel_dark"])
    tube("CableRun", (-0.66, -0.52, 0.32), (-0.1, -0.18, 0.34), 0.025, m["copper"], 6)
    art.box("ControlBox", (0.44, 0.30, 0.24), (0.62, -0.58, 0.30), mat=m["steel_dark"])
    art.box("ControlFace", (0.34, 0.20, 0.04), (0.62, -0.58, 0.43), mat=m["steel"])
    for index in range(2):
        art.cylinder(
            f"RelayLamp{index}",
            0.035,
            0.03,
            (0.54 + index * 0.14, -0.58, 0.46),
            8,
            m["teal"] if index == 0 else m["amber"],
        )
    hazard_strip("Relay", 0, -0.88, 0.18, 1.1, 0.12, 0.0, m, 6)
    rubble("Relay", 0, 0, 1.1, 8, m, 31, 0.08)


def build_turret(m: dict) -> None:
    art.cylinder("Pad", 1.34, 0.18, (0, 0, 0.09), 6, m["concrete"])
    art.cylinder("Ring", 1.02, 0.26, (0, 0, 0.29), 8, m["steel_dark"])
    art.torus("RingTrim", 1.06, 0.04, (0, 0, 0.43), m["rust"], 30)
    art.box("Body", (0.94, 0.86, 0.42), (0, 0, 0.58), mat=m["rust"])
    art.box("Mantlet", (0.72, 0.34, 0.30), (0.30, 0, 0.62), mat=m["steel"])
    for index in range(2):
        y = -0.16 + index * 0.32
        art.cylinder(
            f"Barrel{index}",
            0.065,
            1.06,
            (0.86, y, 0.66),
            10,
            m["steel"],
        )
        art.cylinder(
            f"Muzzle{index}",
            0.095,
            0.14,
            (1.40, y, 0.66),
            8,
            m["steel_dark"],
        )
    art.cylinder("AmmoDrum", 0.24, 0.34, (-0.46, -0.38, 0.50), 12, m["copper"])
    art.torus("AmmoRing", 0.25, 0.025, (-0.46, -0.38, 0.56), m["rust_dark"], 20)
    art.sphere("SensorEye", 0.10, (0.18, 0.34, 0.84), m["red"])
    art.box("Sight", (0.22, 0.14, 0.10), (0.18, 0.34, 0.74), mat=m["steel_dark"])
    hazard_strip("Turret", 0, -1.02, 0.19, 1.2, 0.12, 0.0, m, 6)
    bolt_ring("Pad", 0, 0, 0.20, 1.16, 1.16, 12, m["steel"])
    rng = random.Random(41)
    for index in range(9):
        art.cylinder(
            f"Casing{index}",
            0.028,
            0.10,
            (rng.uniform(-1.1, -0.55), rng.uniform(0.2, 0.9), 0.06),
            6,
            m["hazard"],
        )
    rubble("Turret", 0, 0, 1.25, 9, m, 43, 0.08)


def build_drill(m: dict) -> None:
    art.box("TrackL", (0.42, 2.30, 0.30), (-0.86, 0, 0.20), mat=m["rubber"])
    art.box("TrackR", (0.42, 2.30, 0.30), (0.86, 0, 0.20), mat=m["rubber"])
    for side in (-1, 1):
        for index in range(4):
            art.cylinder(
                f"Wheel{side}{index}",
                0.18,
                0.12,
                (side * 0.86, -0.76 + index * 0.50, 0.30),
                10,
                m["steel_dark"],
            )
    art.box("Hull", (1.70, 1.86, 0.36), (0, 0, 0.48), mat=m["olive"])
    art.box("Deck", (1.42, 1.50, 0.10), (0, 0, 0.68), mat=m["steel_dark"])
    grate("Drill", 0, 0.58, 0.74, 1.05, 0.42, m["steel_dark"], m["steel"], bars=8)
    for side in (-1, 1):
        tube(
            f"Hydraulic{side}",
            (side * 0.68, -0.62, 0.78),
            (side * 0.68, 0.34, 0.78),
            0.055,
            m["copper"],
            8,
        )
        art.cylinder(
            f"HydraulicCap{side}",
            0.08,
            0.12,
            (side * 0.68, 0.36, 0.78),
            8,
            m["steel"],
        )
        art.cylinder(
            f"DrillLamp{side}",
            0.055,
            0.04,
            (side * 0.56, -0.82, 0.72),
            8,
            m["amber"],
        )

    for x in (-0.52, 0.52):
        for y in (-0.52, 0.52):
            art.box(
                f"Tower{x}{y}",
                (0.12, 0.12, 0.86),
                (x, y, 1.06),
                mat=m["steel"],
            )
    for index in range(4):
        angle = math.tau * index / 4
        art.box(
            f"TowerBeam{index}",
            (1.26, 0.10, 0.12),
            (0, 0, 1.48),
            (0, 0, angle),
            m["steel_dark"],
        )
    art.cylinder("TowerHub", 0.20, 0.14, (0, 0, 1.50), 8, m["steel"])
    art.cylinder("DrillShaft", 0.24, 0.70, (0, 0, 0.92), 12, m["copper"])
    art.cylinder("DrillHead", 0.30, 0.24, (0, 0, 0.56), 12, m["steel"])
    art.torus("DrillCollar", 0.32, 0.045, (0, 0, 0.74), m["rust"], 24)
    for index in range(6):
        angle = math.tau * index / 6
        art.box(
            f"Bit{index}",
            (0.12, 0.16, 0.10),
            (math.cos(angle) * 0.28, math.sin(angle) * 0.28, 0.48),
            (0, 0, angle),
            m["steel_dark"],
        )

    art.box("Conveyor", (0.72, 1.90, 0.12), (1.28, 0.10, 0.38), mat=m["steel_dark"])
    for index in range(8):
        art.box(
            f"ConveyorBar{index}",
            (0.66, 0.08, 0.05),
            (1.28, -0.78 + index * 0.24, 0.46),
            mat=m["rust"],
        )
    art.cylinder("ConveyorRoller", 0.12, 0.66, (1.28, -0.92, 0.42), 10, m["steel"])
    for index in range(5):
        rng = random.Random(51 + index)
        art.sphere(
            f"OreChunk{index}",
            rng.uniform(0.08, 0.14),
            (
                1.42 + rng.uniform(-0.12, 0.12),
                1.02 + rng.uniform(-0.10, 0.10),
                0.52,
            ),
            m["copper"],
        )
    hazard_strip("Drill", 0, -1.02, 0.70, 1.1, 0.12, 0.0, m, 6)
    art.sphere("TowerLamp", 0.07, (-0.52, -0.52, 1.60), m["amber"])
    rubble("Drill", 0, 0, 1.20, 10, m, 53, 0.08)


def build_foundry(m: dict) -> None:
    art.box("Pad", (2.60, 2.10, 0.14), (0, 0, 0.07), mat=m["concrete"])
    art.cylinder("Furnace", 0.82, 1.00, (-0.42, 0.10, 0.64), 14, m["rust"])
    art.torus("FurnaceBandA", 0.84, 0.05, (-0.42, 0.10, 0.42), m["steel_dark"], 30)
    art.torus("FurnaceBandB", 0.84, 0.05, (-0.42, 0.10, 0.88), m["steel_dark"], 30)
    art.cylinder("FurnaceLid", 0.86, 0.12, (-0.42, 0.10, 1.16), 14, m["steel"])
    art.cylinder("FurnaceHatch", 0.60, 0.08, (-0.42, 0.10, 1.24), 12, m["steel_dark"])
    art.cylinder("FurnaceVent", 0.17, 0.05, (-0.42, 0.10, 1.30), 10, m["amber"])
    bolt_ring("Hatch", -0.42, 0.10, 1.29, 0.48, 0.48, 10, m["copper"], 0.032)
    for index in range(4):
        angle = math.tau * index / 4 + math.pi / 4
        art.box(
            f"FurnaceRib{index}",
            (0.10, 0.62, 0.06),
            (-0.42, 0.10, 1.22),
            (0, 0, angle),
            m["rust_dark"],
        )
    art.cylinder("Crucible", 0.46, 0.28, (0.64, -0.10, 0.34), 12, m["steel_dark"])
    art.cylinder("Melt", 0.34, 0.06, (0.64, -0.10, 0.50), 12, m["amber"])
    art.torus("CrucibleRim", 0.46, 0.045, (0.64, -0.10, 0.48), m["rust"], 26)
    for index in range(3):
        tube(
            f"TapPipe{index}",
            (-0.20 + index * 0.02, -0.30 + index * 0.18, 0.62),
            (0.48, -0.22 + index * 0.16, 0.42),
            0.045,
            m["copper"],
            8,
        )
    for index in range(4):
        angle = math.tau * index / 4 + math.pi / 4
        tube(
            f"RadialPipe{index}",
            (-0.42 + math.cos(angle) * 0.62, 0.10 + math.sin(angle) * 0.62, 0.92),
            (
                -0.42 + math.cos(angle) * 1.08,
                0.10 + math.sin(angle) * 1.08,
                0.42,
            ),
            0.045,
            m["copper"],
            8,
        )
    art.cylinder("Chimney", 0.24, 1.30, (0.86, 0.66, 0.76), 12, m["rust_dark"])
    art.torus("ChimneyCap", 0.29, 0.045, (0.86, 0.66, 1.42), m["steel"], 22)
    art.cylinder("ChimneySoot", 0.20, 0.03, (0.86, 0.66, 1.45), 12, m["slag"])
    art.torus("Valve", 0.16, 0.04, (-0.10, 0.62, 0.40), m["hazard"], 20)
    art.box("Control", (0.40, 0.24, 0.30), (-0.92, -0.66, 0.30), mat=m["steel_dark"])
    for index in range(3):
        art.cylinder(
            f"FoundryLamp{index}",
            0.035,
            0.03,
            (-1.02 + index * 0.10, -0.66, 0.47),
            8,
            m["amber"] if index < 2 else m["red"],
        )
    for index in range(7):
        rng = random.Random(61 + index)
        art.sphere(
            f"Slag{index}",
            rng.uniform(0.10, 0.20),
            (
                0.10 + rng.uniform(-0.35, 0.35),
                -0.76 + rng.uniform(-0.18, 0.18),
                0.20,
            ),
            m["slag"],
        )
    hazard_strip("Foundry", 0, 0.98, 0.15, 1.7, 0.14, 0.0, m, 8)
    grate("Foundry", 0.55, -0.62, 0.22, 0.86, 0.50, m["steel_dark"], m["steel"])
    rubble("Foundry", 0, 0, 1.35, 8, m, 67, 0.08)


def build_hostile(m: dict) -> None:
    art.cylinder("Base", 1.30, 0.22, (0, 0, 0.11), 5, m["steel_dark"])
    for index in range(5):
        angle = math.tau * index / 5 + 0.2
        art.box(
            f"Armor{index}",
            (0.72, 0.34, 0.18),
            (math.cos(angle) * 0.82, math.sin(angle) * 0.82, 0.28),
            (0, 0, angle),
            m["rust_dark"],
        )
    art.cylinder("Core", 0.52, 0.50, (0, 0, 0.46), 8, m["steel_dark"])
    art.sphere("Eye", 0.24, (0, 0, 0.74), m["red"])
    art.torus("Cage", 0.56, 0.05, (0, 0, 0.58), m["steel"], 26)
    for index in range(10):
        angle = math.tau * index / 10
        art.box(
            f"Spike{index}",
            (0.34, 0.10, 0.12),
            (math.cos(angle) * 1.02, math.sin(angle) * 1.02, 0.34),
            (0, 0, angle),
            m["steel"],
        )
    for index in range(4):
        angle = math.tau * index / 4 + math.pi / 4
        art.box(
            f"RedPlate{index}",
            (0.30, 0.10, 0.06),
            (math.cos(angle) * 0.66, math.sin(angle) * 0.66, 0.62),
            (0, 0, angle),
            m["red"],
        )
    tube("Antenna", (0.30, -0.30, 0.70), (0.62, -0.62, 1.20), 0.018, m["steel"], 6)
    art.sphere("AntennaTip", 0.045, (0.62, -0.62, 1.20), m["red"])
    rubble("Hostile", 0, 0, 1.25, 12, m, 71, 0.07)


def build_scout(m: dict) -> None:
    art.box("Chassis", (1.28, 0.84, 0.24), (0, 0, 0.26), mat=m["olive"])
    art.box("Skid", (1.44, 0.52, 0.08), (0, 0, 0.14), mat=m["steel_dark"])
    for x in (-0.46, 0.46):
        for y in (-0.48, 0.48):
            wheel = art.cylinder(
                f"Wheel{x}{y}",
                0.22,
                0.16,
                (x, y, 0.24),
                12,
                m["rubber"],
            )
            wheel.rotation_euler = (math.radians(90), 0, 0)
            art.cylinder(
                f"Hub{x}{y}",
                0.09,
                0.18,
                (x, y, 0.24),
                8,
                m["steel"],
            ).rotation_euler = (math.radians(90), 0, 0)
    art.box("Cargo", (0.46, 0.58, 0.20), (-0.36, 0, 0.46), mat=m["steel_dark"])
    art.box("Bumper", (0.12, 0.72, 0.14), (0.68, 0, 0.24), mat=m["steel"])
    art.box("CageA", (0.56, 0.05, 0.38), (0.18, -0.26, 0.52), mat=m["steel"])
    art.box("CageB", (0.56, 0.05, 0.38), (0.18, 0.26, 0.52), mat=m["steel"])
    art.box("CageC", (0.05, 0.52, 0.38), (-0.10, 0, 0.52), mat=m["steel"])
    art.cylinder("Mast", 0.05, 0.46, (0.34, 0.16, 0.74), 8, m["steel"])
    art.sphere("Sensor", 0.10, (0.34, 0.16, 1.00), m["teal"])
    art.cylinder("Antenna", 0.018, 0.62, (-0.52, -0.30, 0.78), 6, m["steel"])
    art.sphere("AntennaTip", 0.04, (-0.52, -0.30, 1.10), m["amber"])
    for side in (-1, 1):
        art.cylinder(
            f"Headlight{side}",
            0.055,
            0.05,
            (0.66, side * 0.26, 0.30),
            8,
            m["amber"],
        )
    hazard_strip("Scout", 0, 0.44, 0.39, 0.8, 0.08, 0.0, m, 5)
    rubble("Scout", 0, 0, 0.9, 5, m, 73, 0.05)


def build_sentry(m: dict) -> None:
    art.box("TrackL", (1.30, 0.30, 0.24), (0, -0.44, 0.18), mat=m["rubber"])
    art.box("TrackR", (1.30, 0.30, 0.24), (0, 0.44, 0.18), mat=m["rubber"])
    art.box("Hull", (1.18, 0.70, 0.26), (0, 0, 0.34), mat=m["olive"])
    art.cylinder("TurretRing", 0.42, 0.16, (0.08, 0, 0.50), 10, m["steel_dark"])
    art.box("Turret", (0.64, 0.54, 0.28), (0.10, 0, 0.64), mat=m["steel"])
    art.cylinder("Barrel", 0.055, 0.82, (0.68, 0, 0.66), 10, m["steel_dark"])
    art.cylinder("Muzzle", 0.085, 0.12, (1.10, 0, 0.66), 8, m["steel"])
    art.cylinder("Ammo", 0.18, 0.24, (-0.30, -0.28, 0.54), 10, m["copper"])
    art.sphere("Eye", 0.07, (0.26, 0.20, 0.78), m["red"])
    hazard_strip("Sentry", 0, 0.20, 0.28, 0.7, 0.07, 0.0, m, 5)
    rubble("Sentry", 0, 0, 0.8, 5, m, 79, 0.05)


def build_ore(name: str, mats, crystal_mat, base_mat, count, seed) -> None:
    rng = random.Random(seed)
    for index in range(count):
        angle = rng.uniform(0, math.tau)
        radius = rng.uniform(0.15, 1.15)
        x = math.cos(angle) * radius
        y = math.sin(angle) * radius
        size = rng.uniform(0.09, 0.24) * (1.25 - radius * 0.5)
        ico = bpy.ops.mesh.primitive_ico_sphere_add(
            subdivisions=1,
            radius=size,
            location=(x, y, size * 0.55),
        )
        obj = bpy.context.object
        obj.name = f"{name}Rock{index}"
        obj.scale = (rng.uniform(0.8, 1.4), rng.uniform(0.7, 1.2), rng.uniform(0.5, 0.9))
        obj.rotation_euler = (rng.uniform(0, 0.6), rng.uniform(0, 0.6), angle)
        art.assign(obj, crystal_mat if index % 3 == 0 else base_mat)

    for index in range(8):
        angle = rng.uniform(0, math.tau)
        radius = rng.uniform(0.2, 0.95)
        depth = rng.uniform(0.28, 0.62)
        cone = bpy.ops.mesh.primitive_cone_add(
            vertices=5,
            radius1=rng.uniform(0.06, 0.14),
            radius2=0.01,
            depth=depth,
            location=(
                math.cos(angle) * radius,
                math.sin(angle) * radius,
                depth * 0.5 + 0.05,
            ),
        )
        obj = bpy.context.object
        obj.name = f"{name}Crystal{index}"
        obj.rotation_euler = (
            rng.uniform(-0.35, 0.35),
            rng.uniform(-0.35, 0.35),
            angle,
        )
        art.assign(obj, crystal_mat)

    for index in range(36):
        angle = rng.uniform(0, math.tau)
        radius = rng.uniform(0.2, 1.5)
        art.cylinder(
            f"{name}Gravel{index}",
            rng.uniform(0.025, 0.06),
            0.05,
            (math.cos(angle) * radius, math.sin(angle) * radius, 0.03),
            5,
            base_mat,
        )


ASSETS = {
    "waste_core": (build_core, 4.1, 0.6),
    "waste_generator": (build_generator, 3.6, 0.5),
    "waste_relay": (build_relay, 3.4, 0.5),
    "waste_turret": (build_turret, 3.5, 0.5),
    "waste_drill": (build_drill, 3.8, 0.6),
    "waste_foundry": (build_foundry, 3.9, 0.6),
    "waste_hostile": (build_hostile, 3.6, 0.5),
    "waste_scout": (build_scout, 2.9, 0.4),
    "waste_sentry": (build_sentry, 2.9, 0.4),
    "prop_barrels": (None, 2.8, 0.4),
    "prop_sandbags": (None, 3.0, 0.4),
    "prop_scrap": (None, 3.0, 0.4),
    "ore_copper": (None, 3.4, 0.2),
    "ore_lead": (None, 3.4, 0.2),
    "ore_titanium": (None, 3.4, 0.2),
    "ore_coal": (None, 3.4, 0.2),
}


def build_props(name: str, m: dict) -> None:
    rng = random.Random(97 if name == "prop_barrels" else 101)
    if name == "prop_barrels":
        positions = [(-0.62, -0.18), (-0.20, 0.34), (0.34, -0.10)]
        for index, (x, y) in enumerate(positions):
            art.cylinder(f"Barrel{index}", 0.28, 0.72, (x, y, 0.36), 14, m["rust"])
            for band in (0.18, 0.54):
                art.torus(
                    f"BarrelBand{index}{band}",
                    0.29,
                    0.025,
                    (x, y, band),
                    m["steel_dark"],
                    22,
                )
            art.cylinder(f"BarrelLid{index}", 0.28, 0.04, (x, y, 0.74), 14, m["copper"])
        tipped = art.cylinder("TippedBarrel", 0.27, 0.66, (0.72, 0.62, 0.27), 14, m["copper"])
        tipped.rotation_euler = (math.radians(90), 0, math.radians(35))
        art.torus("TippedBand", 0.28, 0.025, (0.72, 0.62, 0.27), m["rust_dark"], 22)
        for index in range(8):
            art.cylinder(
                f"Can{index}",
                rng.uniform(0.03, 0.05),
                0.05,
                (rng.uniform(-1.1, 1.1), rng.uniform(-0.9, 0.9), 0.03),
                6,
                m["hazard"],
            )
    elif name == "prop_sandbags":
        for row in range(3):
            for index in range(4):
                x = -0.72 + index * 0.46 + (0.23 if row % 2 else 0)
                y = -0.42 + row * 0.42
                bag = bpy.ops.mesh.primitive_ico_sphere_add(
                    subdivisions=2,
                    radius=0.24,
                    location=(x, y, 0.16),
                )
                obj = bpy.context.object
                obj.name = f"Sandbag{row}{index}"
                obj.scale = (1.15, 0.75, 0.42)
                obj.rotation_euler = (0, 0, rng.uniform(-0.2, 0.2))
                art.assign(obj, m["sand"])
        for index in range(10):
            art.cylinder(
                f"SandSpill{index}",
                rng.uniform(0.02, 0.05),
                0.03,
                (rng.uniform(-1.2, 1.2), rng.uniform(-1.0, 0.9), 0.02),
                5,
                m["sand"],
            )
    else:
        for index in range(7):
            angle = rng.uniform(0, math.tau)
            length = rng.uniform(0.5, 1.1)
            art.box(
                f"Plate{index}",
                (length, rng.uniform(0.16, 0.32), 0.05),
                (rng.uniform(-0.4, 0.4), rng.uniform(-0.4, 0.4), 0.10 + index * 0.04),
                (0, 0, angle),
                m["rust"] if index % 2 else m["steel"],
            )
        for index in range(3):
            tube(
                f"Pipe{index}",
                (rng.uniform(-0.9, -0.2), rng.uniform(-0.8, 0.8), 0.12),
                (rng.uniform(0.2, 0.9), rng.uniform(-0.8, 0.8), 0.12),
                0.05,
                m["copper"],
                8,
            )
        gear = art.torus("Gear", 0.26, 0.09, (0.55, -0.55, 0.18), m["steel_dark"], 20)
        gear.rotation_euler = (math.radians(90), 0, 0)
        for index in range(8):
            angle = math.tau * index / 8
            art.box(
                f"Tooth{index}",
                (0.10, 0.08, 0.06),
                (0.55 + math.cos(angle) * 0.32, -0.55 + math.sin(angle) * 0.32, 0.18),
                (math.radians(90), 0, angle),
                m["steel"],
            )


def render_asset(name: str, spec, m: dict, output: Path, size: int) -> None:
    builder, ortho, target_z = spec
    art.clear_scene()
    materials_now = materials()
    if builder is not None:
        builder(materials_now)
    elif name.startswith("prop_"):
        build_props(name, materials_now)
    elif name.startswith("ore_"):
        palette = {
            "ore_copper": ("copper", "rust", 30, 111),
            "ore_lead": ("lead", "steel_dark", 28, 121),
            "ore_titanium": ("titanium", "steel_dark", 26, 131),
            "ore_coal": ("coal", "slag", 32, 141),
        }[name]
        crystal, base, count, seed = palette
        build_ore(name, materials_now, materials_now[crystal], materials_now[base], count, seed)
    setup_wasteland_lights(target_z)
    art.configure_render(size)
    bpy.context.scene.cycles.samples = 64
    rig = art.setup_camera("topdown", ortho, target_z)
    path = output / f"{name}_topdown_000.png"
    art.render_angle(rig, 0, path)
    print(f"WASTE_ASSET {name} {path.name}")


def main() -> None:
    options = args()
    output = Path(options.output).resolve()
    output.mkdir(parents=True, exist_ok=True)
    only = {item.strip() for item in options.only.split(",") if item.strip()}
    selected = [name for name in ASSETS if not only or name in only]
    for name in selected:
        render_asset(name, ASSETS[name], materials(), output, options.size)
    print(f"WASTE_ASSETS_RENDERED {len(selected)} {output}")


if __name__ == "__main__":
    main()
