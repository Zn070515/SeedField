"""Render the A/B camera style-test set: power facility, seed core, worker frame.

Routes:
  oblique  - 35 degree orthographic three-quarter
  topdown  - 90 degree strict top-down

Usage:
  blender --background --factory-startup --python art/blender/render_style_assets.py -- \
      --route oblique --output art/generated/style_oblique --size 512

The geometry is a greybox-plus-materials style test, not final art.
"""

from __future__ import annotations

import argparse
import hashlib
import math
import sys
from pathlib import Path

import bpy

sys.path.insert(0, str(Path(__file__).resolve().parent))
import seedfield_art as art  # noqa: E402


def args() -> argparse.Namespace:
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--route", choices=["oblique", "topdown"], required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--size", type=int, default=512)
    return parser.parse_args(argv)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def materials() -> dict:
    return {
        "steel": art.material("Steel", (0.30, 0.34, 0.38), 0.85, 0.30),
        "dark": art.material("DarkSteel", (0.08, 0.095, 0.115), 0.8, 0.38),
        "rubber": art.material("Rubber", (0.012, 0.015, 0.02), 0.1, 0.75),
        "copper": art.material("Copper", (0.78, 0.32, 0.09), 0.9, 0.22),
        "teal": art.material("Teal", (0.03, 0.20, 0.22), 0.5, 0.3),
        "cyan": art.material(
            "Cyan", (0.03, 0.22, 0.28), 0.2, 0.2, (0.05, 0.80, 1.0), 5.0
        ),
        "hot": art.material(
            "Hot", (0.7, 0.9, 1.0), 0.0, 0.15, (0.85, 0.98, 1.0), 12.0
        ),
        "ivory": art.material(
            "Ivory", (0.55, 0.48, 0.35), 0.2, 0.3, (1.0, 0.82, 0.52), 3.0
        ),
        "amber": art.material(
            "Amber", (0.3, 0.14, 0.02), 0.2, 0.3, (1.0, 0.55, 0.10), 4.0
        ),
        "violet": art.material(
            "Violet", (0.16, 0.08, 0.25), 0.2, 0.3, (0.62, 0.32, 1.0), 3.0
        ),
    }


def build_power(m: dict) -> None:
    art.cylinder("Tier1", 2.45, 0.55, (0, 0, 0.275), 8, m["dark"], math.radians(22.5))
    art.cylinder("Tier1Trim", 2.25, 0.10, (0, 0, 0.58), 8, m["steel"], math.radians(22.5))
    art.cylinder("Tier2", 2.0, 0.50, (0, 0, 0.82), 8, m["steel"], math.radians(22.5))
    art.cylinder("Tier2Trim", 1.8, 0.09, (0, 0, 1.10), 8, m["dark"], math.radians(22.5))
    art.cylinder("Tier3", 1.52, 0.45, (0, 0, 1.30), 8, m["steel"], math.radians(22.5))
    for z, radius in ((0.44, 2.22), (0.72, 2.02), (1.02, 1.78), (1.34, 1.42)):
        art.torus(f"Coil{z}", radius, 0.075, (0, 0, z), m["copper"], 32)
    for index in range(8):
        angle = math.radians(22.5 + index * 45)
        art.box(
            f"Rib{index}",
            (0.13, 0.30, 0.45),
            (math.cos(angle) * 2.05, math.sin(angle) * 2.05, 0.85),
            (0, 0, angle),
            m["copper"],
        )
    art.cylinder("Column", 0.55, 1.35, (0, 0, 2.05), 24, m["teal"])
    art.torus("ColumnRingLow", 0.58, 0.045, (0, 0, 1.55), m["amber"], 32)
    art.torus("ColumnRingHigh", 0.58, 0.045, (0, 0, 2.55), m["amber"], 32)
    art.cylinder("Head", 0.88, 0.48, (0, 0, 2.92), 6, m["steel"])
    art.cylinder("HeadTrim", 0.74, 0.08, (0, 0, 3.18), 6, m["dark"])
    art.cylinder("CoreDisk", 0.46, 0.07, (0, 0, 3.24), 24, m["cyan"])
    art.torus("CoreHalo", 0.62, 0.05, (0, 0, 3.26), m["amber"], 32)
    art.sphere("FloatingCore", 0.26, (0, 0, 3.62), m["hot"])
    art.torus("OrbRing", 0.43, 0.035, (0, 0, 3.60), m["amber"], 32)


def build_core(m: dict) -> None:
    art.cylinder("Tier1", 3.0, 0.55, (0, 0, 0.275), 8, m["dark"], math.radians(22.5))
    art.cylinder("Tier2", 2.35, 0.45, (0, 0, 0.75), 8, m["steel"], math.radians(22.5))
    art.cylinder("Tier3", 1.75, 0.35, (0, 0, 1.15), 8, m["dark"], math.radians(22.5))
    art.torus("TierRing", 2.05, 0.06, (0, 0, 1.02), m["copper"], 48)
    art.cylinder("CoreShell", 0.95, 1.25, (0, 0, 1.70), 24, m["ivory"])
    art.sphere("CoreSeed", 0.32, (0, 0, 2.05), m["hot"])
    art.torus("GuardLow", 1.12, 0.07, (0, 0, 1.32), m["steel"], 48)
    art.torus("GuardHigh", 1.12, 0.07, (0, 0, 2.08), m["steel"], 48)
    for index in range(5):
        angle = math.radians(index * 72)
        art.box(
            f"Arm{index}",
            (1.55, 0.20, 0.18),
            (math.cos(angle) * 1.35, math.sin(angle) * 1.35, 1.20),
            (0, 0, angle),
            m["steel"],
        )
        accent = m["cyan"] if index % 2 == 0 else m["amber"]
        art.sphere(
            f"BranchSocket{index}",
            0.14,
            (math.cos(angle) * 2.18, math.sin(angle) * 2.18, 1.20),
            accent,
        )
        art.box(
            f"Pillar{index}",
            (0.24, 0.24, 1.25),
            (math.cos(angle) * 1.45, math.sin(angle) * 1.45, 1.72),
            (0, 0, angle),
            m["dark"],
        )
    for index in range(8):
        angle = math.radians(index * 45 + 22.5)
        art.sphere(
            f"Bolt{index}",
            0.06,
            (math.cos(angle) * 2.55, math.sin(angle) * 2.55, 0.56),
            m["steel"],
        )


def build_worker(m: dict) -> None:
    art.box("Chassis", (2.3, 1.45, 0.42), (0, 0, 0.38), mat=m["steel"])
    art.box("ChassisBelly", (1.9, 1.15, 0.20), (0, 0, 0.15), mat=m["dark"])
    for side in (-1, 1):
        art.box(
            "Track",
            (0.38, 1.70, 0.34),
            (side * 1.18, 0, 0.22),
            mat=m["rubber"],
        )
        art.box(
            "TrackTrim",
            (0.42, 1.45, 0.08),
            (side * 1.18, 0, 0.42),
            mat=m["steel"],
        )
    art.cylinder("Turret", 0.48, 0.34, (0, 0, 0.76), 6, m["dark"])
    art.box("Barrel", (1.15, 0.15, 0.15), (0.78, 0, 0.82), mat=m["copper"])
    art.box("BarrelTip", (0.22, 0.20, 0.20), (1.38, 0, 0.82), mat=m["steel"])
    art.box("SensorPod", (0.30, 0.30, 0.38), (-0.42, 0.42, 0.88), mat=m["dark"])
    art.cylinder("SensorLens", 0.16, 0.06, (-0.42, 0.42, 1.08), 16, m["cyan"])
    art.cylinder("Antenna", 0.035, 1.0, (-0.52, -0.42, 1.15), 8, m["steel"])
    art.sphere("AntennaTip", 0.06, (-0.52, -0.42, 1.65), m["cyan"])
    art.box("CyanStripe", (0.9, 0.08, 0.05), (0, 0.70, 0.58), mat=m["cyan"])


ASSETS = {
    "power_node": {
        "build": build_power,
        "oblique_scale": 7.4,
        "topdown_scale": 6.4,
        "target_z": 1.8,
        "angles": [0, 90, 180, 270],
    },
    "seed_core": {
        "build": build_core,
        "oblique_scale": 8.2,
        "topdown_scale": 7.2,
        "target_z": 1.2,
        "angles": [0, 45],
    },
    "worker_frame": {
        "build": build_worker,
        "oblique_scale": 4.8,
        "topdown_scale": 4.2,
        "target_z": 0.5,
        "angles": [0, 90, 180, 270],
    },
}


def main() -> None:
    import argparse

    options = args()
    output_root = Path(options.output).resolve()
    output_root.mkdir(parents=True, exist_ok=True)
    route = options.route

    for name, spec in ASSETS.items():
        art.clear_scene()
        mats = materials()
        spec["build"](mats)
        art.setup_lights(spec["target_z"], route)
        art.configure_render(options.size)
        ortho = spec["oblique_scale"] if route == "oblique" else spec["topdown_scale"]
        rig = art.setup_camera(route, ortho, spec["target_z"])

        angles = [0] if route == "topdown" else spec["angles"]
        files = []
        for angle in angles:
            path = output_root / f"{name}_{route}_{angle:03d}.png"
            art.render_angle(rig, angle, path)
            files.append(
                {
                    "angle": angle,
                    "file": path.name,
                    "sha256": sha256(path),
                }
            )

        meta = {
            "schemaVersion": 1,
            "assetId": f"{name}.{route}.v0",
            "kind": "style-test",
            "source": "art/blender/render_style_assets.py",
            "route": route,
            "resolutionPx": [options.size, options.size],
            "orthographicScale": ortho,
            "cameraElevationDegrees": 90 if route == "topdown" else 35,
            "directions": len(angles),
            "files": files,
            "license": "original-procedural-source",
            "notice": "Greybox style test; not final art. Validate in Godot before merge.",
        }
        art.write_meta(output_root / f"{name}_{route}.meta.json", meta)
        print(f"STYLE_ASSET {name} {route} dirs={len(angles)}")

    print(f"STYLE_ASSETS_RENDERED {output_root}")


if __name__ == "__main__":
    main()
