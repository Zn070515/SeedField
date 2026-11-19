"""Render one wasteland asset from a JSON brief.

Usage:
  blender --background --factory-startup --python art/blender/render_brief_asset.py -- \
      --brief art/workflow/briefs/waste_shield_generator.json \
      --output art/workflow/out/waste_shield_generator --size 512 --review-angles 4

The brief is a design note, not a runtime contract. The render uses the same
materials, lights, camera and Cycles settings as `render_wasteland_assets.py`.
Runtime registration (manifest + ArtAssetTests) is a separate, explicit step.
"""

from __future__ import annotations

import argparse
import json
import math
import random
import sys
from datetime import datetime, timezone
from pathlib import Path

import bpy

sys.path.insert(0, str(Path(__file__).resolve().parent))
import seedfield_art as art  # noqa: E402
import render_wasteland_assets as wl  # noqa: E402


def args() -> argparse.Namespace:
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--brief", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--size", type=int, default=512)
    parser.add_argument("--review-angles", type=int, default=4)
    return parser.parse_args(argv)


def build_shield_generator(m: dict) -> None:
    """Shield projector: small hub, four radial arms, outboard pylons and rings."""
    art.cylinder("ShieldPad", 1.05, 0.18, (0, 0, 0.09), 8, m["concrete"])
    art.cylinder("ShieldDeck", 0.92, 0.12, (0, 0, 0.24), 8, m["steel_dark"])
    for index in range(4):
        wl.panel_seams(
            f"ShieldDeckSeam{index}",
            0,
            0,
            0.305,
            1.6,
            1.6,
            3,
            m["steel"],
        )

    # Arms and outboard pylons deliberately break the octagonal outline.
    for index in range(4):
        angle = math.tau * index / 4 + math.pi / 4
        art.box(
            f"ShieldArm{index}",
            (1.05, 0.26, 0.16),
            (math.cos(angle) * 1.10, math.sin(angle) * 1.10, 0.22),
            (0, 0, angle),
            m["steel_dark"],
        )
        x = math.cos(angle) * 1.62
        y = math.sin(angle) * 1.62
        art.box(
            f"ShieldPylon{index}",
            (0.34, 0.34, 0.66),
            (x, y, 0.55),
            (0, 0, angle),
            m["steel"],
        )
        art.box(
            f"ShieldPylonCap{index}",
            (0.42, 0.42, 0.10),
            (x, y, 0.92),
            (0, 0, angle),
            m["rust"],
        )
        art.cylinder(
            f"ShieldPylonLamp{index}",
            0.08,
            0.05,
            (x, y, 1.00),
            8,
            m["teal"],
        )
        wl.tube(
            f"ShieldCable{index}",
            (math.cos(angle) * 0.58, math.sin(angle) * 0.58, 0.42),
            (x * 0.86, y * 0.86, 0.42),
            0.035,
            m["copper"],
            6,
        )

    art.cylinder("ShieldDrum", 0.66, 0.62, (0, 0, 0.62), 16, m["steel"])
    art.cylinder("ShieldDrumTop", 0.56, 0.08, (0, 0, 0.97), 16, m["steel_dark"])
    wl.panel_seams("ShieldDrumSeams", 0, 0, 1.02, 0.9, 0.9, 4, m["steel"])
    wl.bolt_ring("ShieldDrum", 0, 0, 1.03, 0.48, 0.48, 12, m["copper"])

    for index in range(8):
        angle = math.tau * index / 8 + math.pi / 8
        art.box(
            f"ShieldFin{index}",
            (0.48, 0.10, 0.30),
            (math.cos(angle) * 0.82, math.sin(angle) * 0.82, 0.62),
            (0, 0, angle),
            m["rust"] if index % 2 else m["steel"],
        )
        art.cylinder(
            f"ShieldFinBolt{index}",
            0.045,
            0.06,
            (math.cos(angle) * 1.06, math.sin(angle) * 1.06, 0.62),
            6,
            m["copper"],
        )

    art.torus("ShieldRingOuter", 1.36, 0.035, (0, 0, 0.32), m["teal"], 72)
    art.torus("ShieldRingMid", 1.08, 0.028, (0, 0, 0.33), m["copper"], 64)
    art.torus("ShieldRingInner", 0.78, 0.024, (0, 0, 0.34), m["steel"], 52)
    art.torus("ShieldHalo", 0.52, 0.025, (0, 0, 1.12), m["teal"], 48)

    art.cylinder("ShieldEmitter", 0.30, 0.34, (0, 0, 1.16), 16, m["steel_dark"])
    art.torus("ShieldEmitterRing", 0.34, 0.035, (0, 0, 1.34), m["teal"], 32)
    art.sphere("ShieldOrb", 0.23, (0, 0, 1.38), m["teal"])

    wl.hazard_strip("ShieldHazardN", 0, -0.98, 0.20, 1.0, 0.10, 0.0, m, 5)
    wl.hazard_strip("ShieldHazardS", 0, 0.98, 0.20, 1.0, 0.10, 0.0, m, 5)
    wl.hazard_strip("ShieldHazardW", -0.98, 0, 0.20, 1.0, 0.10, math.pi / 2, m, 5)
    wl.hazard_strip("ShieldHazardE", 0.98, 0, 0.20, 1.0, 0.10, math.pi / 2, m, 5)
    wl.bolt_ring("ShieldPad", 0, 0, 0.20, 1.00, 1.00, 16, m["steel"])
    wl.grate("ShieldGrate", 0.0, -0.72, 0.20, 0.52, 0.24, m["steel_dark"], m["steel"], 7)

    art.box(
        "ShieldPanel",
        (0.30, 0.16, 0.20),
        (-0.72, 0.62, 0.34),
        (0, 0, math.radians(20)),
        m["steel_dark"],
    )
    for index in range(3):
        art.cylinder(
            f"ShieldLamp{index}",
            0.032,
            0.03,
            (-0.77 + index * 0.08, 0.62, 0.46),
            8,
            m["teal"] if index == 0 else m["amber"],
        )

    wl.rubble("Shield", 0.72, -0.78, 0.42, 8, m, 307, 0.06)


def build_repair_bay(m: dict) -> None:
    """Repair bay: rectangular gantry frame with an open center and a lift platform."""
    # Open frame: four perimeter beams + a circular base under the lift.
    art.box("RepairPadN", (2.90, 0.28, 0.16), (0, -0.86, 0.08), mat=m["concrete"])
    art.box("RepairPadS", (2.90, 0.28, 0.16), (0, 0.86, 0.08), mat=m["concrete"])
    art.box("RepairPadW", (0.28, 2.00, 0.16), (-1.31, 0, 0.08), mat=m["concrete"])
    art.box("RepairPadE", (0.28, 2.00, 0.16), (1.31, 0, 0.08), mat=m["concrete"])
    for index, (x, y) in enumerate(
        ((-1.31, -0.86), (1.31, -0.86), (-1.31, 0.86), (1.31, 0.86))
    ):
        art.box(
            f"RepairCorner{index}",
            (0.44, 0.44, 0.18),
            (x, y, 0.09),
            mat=m["steel_dark"],
        )
    art.cylinder("RepairBase", 0.70, 0.16, (0, 0, 0.08), 16, m["concrete"])
    art.cylinder("RepairBasePlate", 0.58, 0.08, (0, 0, 0.20), 16, m["steel_dark"])

    for index, y in enumerate((-0.78, 0.78)):
        art.box(
            f"RepairRail{index}",
            (2.40, 0.22, 0.32),
            (0, y, 0.42),
            mat=m["steel"],
        )
        art.box(
            f"RepairRailTop{index}",
            (2.40, 0.14, 0.06),
            (0, y, 0.60),
            mat=m["rust"],
        )
        for tooth in range(6):
            art.box(
                f"RepairTooth{index}{tooth}",
                (0.10, 0.10, 0.05),
                (-0.90 + tooth * 0.36, y, 0.64),
                mat=m["hazard"],
            )

    art.box("RepairBeam", (0.24, 1.90, 0.36), (1.22, 0, 0.55), mat=m["steel_dark"])
    art.box("RepairBeamTop", (0.30, 2.00, 0.08), (1.22, 0, 0.77), mat=m["rust"])
    for index, y in enumerate((-0.62, 0.62)):
        art.box(
            f"RepairArm{index}",
            (1.00, 0.14, 0.14),
            (0.55, y, 0.78),
            mat=m["steel"],
        )
        art.cylinder(
            f"RepairArmJoint{index}",
            0.10,
            0.10,
            (0.10, y, 0.78),
            8,
            m["copper"],
        )

    art.cylinder("RepairLift", 0.52, 0.28, (0, 0, 0.38), 16, m["steel"])
    art.cylinder("RepairLiftTop", 0.44, 0.06, (0, 0, 0.55), 16, m["olive"])
    art.torus("RepairLiftRing", 0.56, 0.030, (0, 0, 0.56), m["amber"], 40)

    for index, (x, y) in enumerate(
        ((-1.15, -0.82), (1.15, -0.82), (-1.15, 0.82), (1.15, 0.82))
    ):
        art.cylinder(
            f"RepairPost{index}",
            0.10,
            0.55,
            (x, y, 0.42),
            8,
            m["steel_dark"],
        )
        art.cylinder(
            f"RepairLamp{index}",
            0.07,
            0.04,
            (x, y, 0.72),
            8,
            m["amber"],
        )

    wl.hazard_strip("RepairHazardW", -1.28, 0, 0.22, 1.6, 0.10, math.pi / 2, m, 6)
    wl.hazard_strip("RepairHazardE", 1.28, 0, 0.22, 1.6, 0.10, math.pi / 2, m, 6)
    wl.tube("RepairCable", (-1.0, -0.78, 0.42), (-1.0, 0.78, 0.42), 0.04, m["copper"], 6)
    wl.grate("RepairGrate", 0.30, -0.86, 0.18, 0.90, 0.22, m["steel_dark"], m["steel"], 6)
    wl.rubble("Repair", 0.90, -0.62, 0.40, 6, m, 401, 0.06)


def ensure_flavor_materials(m: dict) -> None:
    if "teal_soft" not in m:
        m["teal_soft"] = wl.wasteland_material(
            "TealSoft",
            (0.02, 0.20, 0.18),
            0.2,
            0.38,
            grime=(0.01, 0.07, 0.06),
            scale=6.0,
            emission=(0.10, 0.72, 0.62),
            strength=1.4,
        )
    if "teal_focus" not in m:
        m["teal_focus"] = wl.wasteland_material(
            "TealFocus",
            (0.02, 0.26, 0.24),
            0.2,
            0.32,
            grime=(0.01, 0.08, 0.07),
            scale=6.0,
            emission=(0.10, 0.90, 0.80),
            strength=2.2,
        )
    if "paint_cyan" not in m:
        m["paint_cyan"] = wl.wasteland_material(
            "PaintCyan",
            (0.03, 0.34, 0.32),
            0.15,
            0.55,
            grime=(0.01, 0.12, 0.11),
            scale=9.0,
            grime_strength=0.35,
        )


def build_shield_generator_flavor(m: dict, hybrid: bool = False) -> None:
    """Flavor pass: asymmetric annex/spool/barrels, segmented dark ring, softer cyan."""
    rng = random.Random(511)
    ensure_flavor_materials(m)
    focus = "teal_focus" if hybrid else "teal_soft"

    art.cylinder("FlavorPad", 1.08, 0.18, (0, 0, 0.09), 8, m["concrete"])
    art.box(
        "FlavorAnnex",
        (0.72, 0.52, 0.34),
        (1.30, 0.55, 0.35),
        (0, 0, math.radians(35)),
        m["steel_dark"],
    )
    art.box(
        "FlavorAnnexTop",
        (0.66, 0.46, 0.06),
        (1.30, 0.55, 0.55),
        (0, 0, math.radians(35)),
        m["rust"],
    )
    art.box(
        "FlavorWindow",
        (0.32, 0.20, 0.04),
        (1.38, 0.53, 0.58),
        (0, 0, math.radians(35)),
        m["rubber"],
    )
    for index in range(3):
        art.box(
            f"FlavorStencil{index}",
            (0.04, 0.16, 0.02),
            (1.18 + index * 0.08, 0.50, 0.59),
            (0, 0, math.radians(35)),
            m["paint_cyan"],
        )
    art.cylinder("FlavorAntenna", 0.025, 0.55, (1.46, 0.38, 0.80), 6, m["steel"])
    art.cylinder("FlavorAntennaTip", 0.05, 0.04, (1.46, 0.38, 1.08), 6, m["amber"])

    art.cylinder(
        "FlavorSpool",
        0.30,
        0.30,
        (-1.20, -0.20, 0.34),
        14,
        m["rust_dark"] if hybrid else m["copper"],
    )
    art.cylinder("FlavorSpoolHub", 0.10, 0.36, (-1.20, -0.20, 0.34), 10, m["steel_dark"])
    art.torus("FlavorSpoolRing", 0.32, 0.03, (-1.20, -0.20, 0.50), m["rust"], 24)
    wl.tube("FlavorCable", (-1.20, -0.20, 0.30), (-1.88, -0.56, 0.16), 0.04, m["rubber"], 6)

    for index, (x, y) in enumerate(((0.62, -0.92), (0.92, -0.78))):
        art.cylinder(
            f"FlavorBarrel{index}",
            0.18,
            0.42,
            (x, y, 0.21),
            12,
            m["rust_dark"] if hybrid else m["rust"],
        )
        art.torus(
            f"FlavorBarrelBand{index}",
            0.19,
            0.02,
            (x, y, 0.30),
            m["steel_dark"],
            18,
        )
        art.cylinder(f"FlavorBarrelLid{index}", 0.18, 0.03, (x, y, 0.43), 12, m["copper"])

    art.cylinder("FlavorDeck", 0.92, 0.12, (0, 0, 0.24), 8, m["steel_dark"])
    art.cylinder("FlavorWell", 0.76, 0.10, (0, 0, 0.31), 16, m["slag"])
    art.cylinder("FlavorDrum", 0.64, 0.60, (0, 0, 0.62), 16, m["steel"])
    art.cylinder("FlavorDrumTop", 0.54, 0.08, (0, 0, 0.96), 16, m["steel_dark"])

    for index in range(7):
        angle = math.tau * index / 7 + math.pi / 7
        length = 0.42 if index != 3 else 0.26
        art.box(
            f"FlavorFin{index}",
            (length, 0.10, 0.26),
            (math.cos(angle) * 0.80, math.sin(angle) * 0.80, 0.62),
            (0, 0, angle + (0.12 if index == 5 else 0)),
            m["rust"] if index % 2 else m["steel"],
        )
    art.box(
        "FlavorPatch",
        (0.36, 0.18, 0.04),
        (0.62, 0.30, 0.72),
        (0, 0, math.radians(40)),
        m["hazard"],
    )

    art.torus("FlavorRingHousing", 1.36, 0.10, (0, 0, 0.30), m["steel_dark"], 72)
    art.torus("FlavorRingInner", 0.78, 0.03, (0, 0, 0.34), m["copper"], 52)
    for index in range(8):
        angle = math.tau * index / 8
        x = math.cos(angle) * 1.36
        y = math.sin(angle) * 1.36
        art.box(
            f"FlavorLamp{index}",
            (0.26, 0.07, 0.05),
            (x, y, 0.38),
            (0, 0, angle),
            m[focus],
        )
        if index % 2 == 0:
            art.box(
                f"FlavorClamp{index}",
                (0.16, 0.20, 0.10),
                (x, y, 0.39),
                (0, 0, angle),
                m["rust"],
            )
    art.box(
        "FlavorBroken",
        (0.30, 0.12, 0.06),
        (0.0, -1.36, 0.40),
        (0, 0, math.pi / 2),
        m["slag"],
    )

    for index in range(4):
        angle = math.tau * index / 4 + math.pi / 4
        x = math.cos(angle) * 1.62
        y = math.sin(angle) * 1.62
        art.box(
            f"FlavorArm{index}",
            (1.05, 0.24, 0.14),
            (math.cos(angle) * 1.10, math.sin(angle) * 1.10, 0.22),
            (0, 0, angle),
            m["steel_dark"],
        )
        art.box(
            f"FlavorPylon{index}",
            (0.32, 0.32, 0.62),
            (x, y, 0.52),
            (0, 0, angle),
            m["steel"],
        )
        cap = 0.36 if hybrid else 0.40
        art.box(
            f"FlavorPylonCap{index}",
            (cap, cap, 0.09),
            (x, y, 0.88),
            (0, 0, angle),
            m["rust"] if index != 2 else m["slag"],
        )
        art.cylinder(
            f"FlavorPylonLamp{index}",
            0.07,
            0.04,
            (x, y, 0.95),
            8,
            m[focus] if index != 2 else m["red"],
        )

    if hybrid:
        for index in range(4):
            angle = math.tau * index / 4
            art.box(
                f"HybridArc{index}",
                (0.44, 0.09, 0.05),
                (math.cos(angle) * 1.36, math.sin(angle) * 1.36, 0.395),
                (0, 0, angle),
                m[focus],
            )
        art.torus("HybridInnerRing", 1.10, 0.025, (0, 0, 0.36), m[focus], 64)
        art.cylinder("HybridCore", 0.30, 0.04, (0, 0, 1.03), 16, m[focus])
        art.box(
            "HybridChevronA",
            (0.36, 0.06, 0.02),
            (-0.18, -0.56, 0.31),
            (0, 0, math.radians(35)),
            m["paint_cyan"],
        )
        art.box(
            "HybridChevronB",
            (0.36, 0.06, 0.02),
            (0.18, -0.56, 0.31),
            (0, 0, math.radians(-35)),
            m["paint_cyan"],
        )

    art.cylinder("FlavorEmitter", 0.28, 0.30, (0, 0, 1.12), 16, m["steel_dark"])
    art.torus("FlavorEmitterRing", 0.30, 0.028, (0, 0, 1.28), m[focus], 32)
    art.sphere(
        "FlavorOrb",
        0.22 if hybrid else 0.18,
        (0, 0, 1.30),
        m[focus],
    )
    for index in range(3):
        angle = math.tau * index / 3
        art.box(
            f"FlavorCage{index}",
            (0.04, 0.04, 0.34),
            (math.cos(angle) * 0.26, math.sin(angle) * 0.26, 1.18),
            (0, 0, angle),
            m["steel"],
        )

    art.cylinder("FlavorScorch", 0.32, 0.01, (0.55, -0.35, 0.31), 12, m["slag"])
    for index in range(6):
        art.box(
            f"FlavorDebris{index}",
            (rng.uniform(0.08, 0.16), rng.uniform(0.05, 0.10), 0.04),
            (rng.uniform(-0.9, 0.9), rng.uniform(-0.9, 0.9), 0.30),
            (0, 0, rng.uniform(0, math.tau)),
            m["rust"] if index % 2 else m["steel_dark"],
        )
    wl.hazard_strip("FlavorHazard", 0, -1.00, 0.20, 1.0, 0.09, 0.0, m, 5)
    wl.bolt_ring("FlavorBolts", 0, 0, 0.20, 1.02, 1.02, 14, m["steel"])
    wl.rubble("FlavorRubble", 0.80, -0.80, 0.38, 7, m, 503, 0.06)


def build_shield_generator_hybrid(m: dict) -> None:
    build_shield_generator_flavor(m, hybrid=True)


def build_repair_bay_flavor(m: dict) -> None:
    """Flavor pass: asymmetric cabin/crane/spool, cyan lift, damaged corner."""
    ensure_flavor_materials(m)
    art.box("RepairPadN", (2.90, 0.28, 0.16), (0, -0.86, 0.08), mat=m["concrete"])
    art.box("RepairPadS", (2.70, 0.28, 0.16), (0.10, 0.86, 0.08), mat=m["concrete"])
    art.box("RepairPadW", (0.28, 2.00, 0.16), (-1.31, 0, 0.08), mat=m["concrete"])
    art.box("RepairPadE", (0.28, 1.90, 0.16), (1.31, -0.05, 0.08), mat=m["concrete"])
    for index, (x, y) in enumerate(
        ((-1.31, -0.86), (1.31, -0.86), (-1.31, 0.86), (1.31, 0.86))
    ):
        if index == 3:
            art.box(
                "RepairPatchCorner",
                (0.50, 0.50, 0.10),
                (x, y, 0.10),
                (0, 0, math.radians(12)),
                m["hazard"],
            )
            art.box(
                "RepairBentPlate",
                (0.34, 0.20, 0.05),
                (x - 0.10, y - 0.10, 0.22),
                (0, 0, math.radians(35)),
                m["slag"],
            )
        else:
            art.box(
                f"RepairCorner{index}",
                (0.44, 0.44, 0.18),
                (x, y, 0.09),
                mat=m["steel_dark"],
            )
    art.cylinder("RepairBase", 0.70, 0.16, (0, 0, 0.08), 16, m["concrete"])
    art.cylinder("RepairBasePlate", 0.58, 0.08, (0, 0, 0.20), 16, m["steel_dark"])

    for index, y in enumerate((-0.78, 0.78)):
        art.box(
            f"RepairRail{index}",
            (2.40, 0.22, 0.32),
            (0, y, 0.42),
            mat=m["steel"],
        )
        art.box(
            f"RepairRailTop{index}",
            (2.40, 0.14, 0.06),
            (0, y, 0.60),
            mat=m["rust"],
        )
        for tooth in range(6):
            art.box(
                f"RepairTooth{index}{tooth}",
                (0.10, 0.10, 0.05),
                (-0.90 + tooth * 0.36, y, 0.64),
                mat=m["hazard"],
            )
    art.box("RepairBeam", (0.24, 1.90, 0.36), (1.22, 0, 0.55), mat=m["steel_dark"])
    art.box("RepairBeamTop", (0.30, 2.00, 0.08), (1.22, 0, 0.77), mat=m["rust"])
    for index, y in enumerate((-0.62, 0.62)):
        art.box(
            f"RepairArm{index}",
            (1.00, 0.14, 0.14),
            (0.55, y, 0.78),
            mat=m["steel"],
        )
        art.cylinder(
            f"RepairArmJoint{index}",
            0.10,
            0.10,
            (0.10, y, 0.78),
            8,
            m["copper"],
        )

    art.box(
        "RepairCabin",
        (0.66, 0.50, 0.42),
        (1.55, 0.35, 0.39),
        (0, 0, math.radians(-15)),
        m["steel_dark"],
    )
    art.box(
        "RepairCabinTop",
        (0.60, 0.44, 0.06),
        (1.55, 0.35, 0.62),
        (0, 0, math.radians(-15)),
        m["rust"],
    )
    art.box(
        "RepairWindow",
        (0.30, 0.18, 0.04),
        (1.60, 0.30, 0.64),
        (0, 0, math.radians(-15)),
        m["rubber"],
    )
    for index in range(3):
        art.box(
            f"RepairStencil{index}",
            (0.04, 0.14, 0.02),
            (1.42 + index * 0.07, 0.28, 0.65),
            (0, 0, math.radians(-15)),
            m["paint_cyan"],
        )
    art.cylinder("RepairCraneMast", 0.10, 0.90, (1.20, -0.15, 0.65), 8, m["steel"])
    art.box("RepairCraneArm", (1.20, 0.16, 0.14), (0.65, -0.15, 1.10), mat=m["steel_dark"])
    art.box("RepairCraneArmTop", (0.90, 0.08, 0.05), (0.65, -0.15, 1.18), mat=m["rust"])
    art.cylinder("RepairCraneHook", 0.035, 0.55, (0.20, -0.15, 0.86), 6, m["steel"])
    art.cylinder("RepairHookClaw", 0.10, 0.08, (0.20, -0.15, 0.56), 8, m["copper"])

    for index in range(4):
        art.cylinder(
            f"RepairCanister{index}",
            0.05,
            0.22,
            (-1.05 + index * 0.12, -0.55, 0.34),
            8,
            m["hazard"] if index % 2 else m["copper"],
        )
    art.box("RepairToolRack", (0.60, 0.14, 0.08), (-0.95, 0.55, 0.36), mat=m["steel_dark"])

    art.cylinder("RepairSpool", 0.24, 0.26, (1.05, 0.78, 0.28), 12, m["rust_dark"])
    art.cylinder("RepairSpoolHub", 0.08, 0.30, (1.05, 0.78, 0.28), 8, m["steel_dark"])
    wl.tube("RepairCableOut", (1.05, 0.78, 0.26), (1.80, 0.95, 0.14), 0.035, m["rubber"], 6)

    for index, (x, y) in enumerate(((-0.95, -0.70), (-0.72, -0.80))):
        art.cylinder(f"RepairBarrel{index}", 0.16, 0.36, (x, y, 0.18), 12, m["rust_dark"])
        art.torus(
            f"RepairBarrelBand{index}",
            0.17,
            0.02,
            (x, y, 0.26),
            m["steel_dark"],
            16,
        )

    art.cylinder("RepairLift", 0.52, 0.28, (0, 0, 0.38), 16, m["steel"])
    art.cylinder("RepairLiftTop", 0.44, 0.06, (0, 0, 0.55), 16, m["olive"])
    art.torus("RepairLiftRing", 0.56, 0.03, (0, 0, 0.56), m["teal_focus"], 40)
    art.cylinder("RepairLiftCore", 0.20, 0.04, (0, 0, 0.60), 16, m["teal_focus"])

    for index, (x, y) in enumerate(
        ((-1.15, -0.82), (1.15, -0.82), (-1.15, 0.82), (1.15, 0.82))
    ):
        if index == 3:
            art.cylinder(f"RepairLamp{index}", 0.07, 0.04, (x, y, 0.32), 8, m["red"])
        else:
            art.cylinder(f"RepairLamp{index}", 0.07, 0.04, (x, y, 0.72), 8, m["amber"])

    wl.hazard_strip("RepairHazardW", -1.28, 0, 0.22, 1.6, 0.10, math.pi / 2, m, 6)
    wl.hazard_strip("RepairHazardE", 1.28, 0, 0.22, 1.6, 0.10, math.pi / 2, m, 6)
    wl.tube("RepairCable", (-1.0, -0.78, 0.42), (-1.0, 0.78, 0.42), 0.04, m["copper"], 6)
    wl.grate("RepairGrate", 0.30, -0.86, 0.18, 0.90, 0.22, m["steel_dark"], m["steel"], 6)
    art.cylinder("RepairScorch", 0.30, 0.01, (0.45, -0.25, 0.31), 12, m["slag"])
    art.box(
        "RepairChevronA",
        (0.34, 0.06, 0.02),
        (0.05, -0.55, 0.31),
        (0, 0, math.radians(35)),
        m["paint_cyan"],
    )
    art.box(
        "RepairChevronB",
        (0.34, 0.06, 0.02),
        (0.40, -0.55, 0.31),
        (0, 0, math.radians(-35)),
        m["paint_cyan"],
    )
    wl.rubble("RepairRubble", 0.85, -0.75, 0.38, 7, m, 733, 0.06)


BUILDERS = {
    "waste_shield_generator": (build_shield_generator, 4.0, 0.55),
    "waste_shield_generator_flavor": (build_shield_generator_flavor, 4.3, 0.55),
    "waste_shield_generator_hybrid": (build_shield_generator_hybrid, 4.3, 0.55),
    "waste_repair_bay": (build_repair_bay, 4.2, 0.50),
    "waste_repair_bay_flavor": (build_repair_bay_flavor, 4.6, 0.55),
}


def main() -> int:
    options = args()
    brief_path = Path(options.brief).resolve()
    brief = json.loads(brief_path.read_text(encoding="utf-8"))
    asset_id = brief["assetId"]
    if asset_id not in BUILDERS:
        raise SystemExit(f"No builder registered for {asset_id}")

    output = Path(options.output).resolve()
    output.mkdir(parents=True, exist_ok=True)

    builder, ortho, target_z = BUILDERS[asset_id]
    art.clear_scene()
    builder(wl.materials())
    wl.setup_wasteland_lights(target_z)
    art.configure_render(options.size)
    bpy.context.scene.cycles.samples = 64
    rig = art.setup_camera("topdown", ortho, target_z)

    main_path = output / f"{asset_id}_topdown_000.png"
    art.render_angle(rig, 0, main_path)
    print(f"BRIEF_ASSET_RENDERED {asset_id} {main_path.name}")

    review_dir = output / "review"
    for index in range(max(0, options.review_angles)):
        angle = round(360 * index / options.review_angles)
        art.render_angle(
            rig,
            angle,
            review_dir / f"{asset_id}_angle_{angle:03d}.png",
        )

    meta = {
        "assetId": asset_id,
        "brief": str(brief_path),
        "output": str(output),
        "size": options.size,
        "reviewAngles": options.review_angles,
        "camera": "topdown-90",
        "sourceScript": "art/blender/render_brief_asset.py",
        "generatedAt": datetime.now(timezone.utc).isoformat(),
        "status": "review-only",
        "license": "original-procedural-source",
    }
    art.write_meta(output / "meta.json", meta)
    print(f"BRIEF_ASSET_META {output / 'meta.json'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
