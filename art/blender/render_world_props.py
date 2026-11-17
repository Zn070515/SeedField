"""Render topdown industrial world props for the frontier map.

Usage:
  blender --background --factory-startup --python art/blender/render_world_props.py -- \
      --output art/generated/world_props --size 512
"""

from __future__ import annotations

import argparse
import math
import sys
from pathlib import Path

import bpy

sys.path.insert(0, str(Path(__file__).resolve().parent))
import seedfield_art as art  # noqa: E402


def args() -> argparse.Namespace:
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", required=True)
    parser.add_argument("--size", type=int, default=512)
    return parser.parse_args(argv)


def materials() -> dict:
    return {
        "steel": art.material("Steel", (0.30, 0.34, 0.38), 0.85, 0.30),
        "dark": art.material("DarkSteel", (0.08, 0.095, 0.115), 0.8, 0.38),
        "rubber": art.material("Rubber", (0.012, 0.015, 0.02), 0.1, 0.75),
        "rust": art.material("Rust", (0.34, 0.13, 0.05), 0.6, 0.55),
        "copper": art.material("Copper", (0.60, 0.24, 0.07), 0.8, 0.35),
        "hazard": art.material("Hazard", (0.45, 0.30, 0.06), 0.3, 0.5),
        "cyan": art.material(
            "Cyan", (0.03, 0.22, 0.28), 0.2, 0.2, (0.05, 0.80, 1.0), 3.0
        ),
    }


def build_ruin_block(m: dict) -> None:
    art.box("Slab", (4.6, 3.6, 0.24), (0, 0, 0.12), mat=m["dark"])
    art.box("WallA", (3.2, 0.32, 1.35), (0.4, -1.0, 0.72), (0, 0, 0.18), m["steel"])
    art.box("WallB", (0.34, 2.6, 1.05), (-1.2, 0.4, 0.55), (0, 0, -0.2), m["steel"])
    art.box("BrokenWall", (1.8, 0.30, 0.75), (0.8, 1.15, 0.45), (0, 0, 0.45), m["rust"])
    for index in range(6):
        art.cylinder(
            f"Rebar{index}",
            0.035,
            1.2,
            (-1.6 + index * 0.55, -0.4 + index * 0.22, 0.75),
            6,
            m["rust"],
        )
    for index in range(8):
        angle = index * math.radians(45)
        art.box(
            f"Rubble{index}",
            (0.35, 0.22, 0.20),
            (math.cos(angle) * 1.9, math.sin(angle) * 1.5, 0.18),
            (0, 0, angle),
            m["dark"],
        )
    art.box("HazardStrip", (1.4, 0.12, 0.06), (0.1, 1.55, 0.28), mat=m["hazard"])


def build_pipe_junction(m: dict) -> None:
    art.box("Pad", (4.4, 4.4, 0.20), (0, 0, 0.10), mat=m["dark"])
    art.cylinder("Hub", 0.95, 0.55, (0, 0, 0.45), 12, m["copper"])
    art.torus("HubRing", 1.05, 0.06, (0, 0, 0.75), m["steel"], 32)
    for axis, rotation in (("X", (0, math.radians(90), 0)), ("Y", (math.radians(90), 0, 0))):
        pipe = art.cylinder(
            f"Pipe{axis}",
            0.28,
            3.6,
            (0, 0, 0.50),
            12,
            m["steel"],
        )
        pipe.rotation_euler = rotation
    for side in (-1, 1):
        art.box("Support", (0.55, 0.55, 0.65), (side * 1.55, 0, 0.32), mat=m["dark"])
        art.torus(
            "Valve",
            0.36,
            0.07,
            (side * 1.45, 0, 0.86),
            m["hazard"],
            24,
        )
    art.cylinder("Vent", 0.18, 2.0, (0, 1.55, 1.15), 10, m["rust"])
    art.sphere("VentTip", 0.16, (0, 1.55, 2.15), m["cyan"])


def build_wreck_hauler(m: dict) -> None:
    art.box("Hull", (3.4, 2.0, 0.45), (0, 0, 0.35), mat=m["rust"])
    art.box("Belly", (2.8, 1.6, 0.22), (0, 0, 0.16), mat=m["dark"])
    for side in (-1, 1):
        art.box(
            "Track",
            (0.44, 2.4, 0.34),
            (side * 1.62, 0, 0.24),
            mat=m["rubber"],
        )
    art.box("Cab", (1.0, 0.9, 0.55), (-0.8, -0.3, 0.75), (0, 0, 0.18), m["steel"])
    art.box("BrokenArm", (1.8, 0.28, 0.26), (1.2, 0.6, 0.65), (0, 0, 0.55), m["copper"])
    art.box("BentTip", (0.6, 0.5, 0.3), (2.25, 1.0, 0.48), (0, 0, -0.4), m["dark"])
    art.box("Scorch", (1.2, 1.1, 0.05), (0.4, -0.4, 0.58), mat=m["dark"])
    art.torus("Wheel", 0.20, 0.06, (1.4, -0.2, 0.40), m["steel"], 20)
    art.sphere("Ember", 0.10, (-0.3, 0.5, 0.62), m["hazard"])


ASSETS = {
    "ruin_block": (build_ruin_block, 6.4, 0.7),
    "pipe_junction": (build_pipe_junction, 6.2, 0.9),
    "wreck_hauler": (build_wreck_hauler, 5.4, 0.7),
}


def main() -> None:
    options = args()
    output = Path(options.output).resolve()
    output.mkdir(parents=True, exist_ok=True)
    for name, (builder, ortho, target_z) in ASSETS.items():
        art.clear_scene()
        mats = materials()
        builder(mats)
        art.setup_lights(target_z, "topdown")
        art.configure_render(options.size)
        rig = art.setup_camera("topdown", ortho, target_z)
        path = output / f"{name}_topdown_000.png"
        art.render_angle(rig, 0, path)
        print(f"WORLD_PROP {name} {path.name}")
    print(f"WORLD_PROPS_RENDERED {output}")


if __name__ == "__main__":
    main()
