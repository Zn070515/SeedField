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
    """Open five-fold cradle: a permanent shrine, not a larger power tower."""
    art.cylinder(
        "Foundation", 3.35, 0.38, (0, 0, 0.19), 12, m["dark"], math.radians(15)
    )
    art.cylinder(
        "FoundationRim",
        3.10,
        0.12,
        (0, 0, 0.44),
        12,
        m["steel"],
        math.radians(15),
    )
    art.cylinder(
        "Cradle", 2.35, 0.42, (0, 0, 0.68), 8, m["steel"], math.radians(22.5)
    )
    art.torus("CradleRing", 2.22, 0.075, (0, 0, 0.92), m["copper"], 48)

    for index in range(5):
        angle = math.radians(index * 72 + 18)
        art.box(
            f"Foot{index}",
            (0.95, 0.72, 0.22),
            (math.cos(angle) * 2.55, math.sin(angle) * 2.55, 0.50),
            (0, 0, angle),
            m["dark"],
        )
        art.box(
            f"Buttress{index}",
            (0.52, 0.40, 2.05),
            (math.cos(angle) * 1.95, math.sin(angle) * 1.95, 1.22),
            (0, math.radians(-17), angle),
            m["steel"],
        )
        accent = m["cyan"] if index % 2 == 0 else m["amber"]
        art.sphere(
            f"BranchSocket{index}",
            0.16,
            (math.cos(angle) * 3.02, math.sin(angle) * 3.02, 0.62),
            accent,
        )
        art.box(
            f"Petal{index}",
            (0.40, 0.34, 1.30),
            (math.cos(angle) * 1.30, math.sin(angle) * 1.30, 2.48),
            (0, math.radians(24), angle),
            m["steel"],
        )
        art.sphere(
            f"PetalTip{index}",
            0.10,
            (math.cos(angle) * 1.56, math.sin(angle) * 1.56, 3.08),
            m["amber"] if index % 2 == 0 else m["cyan"],
        )

    art.torus("GuardRing", 1.55, 0.075, (0, 0, 2.05), m["steel"], 48)
    for index in range(3):
        art.torus(
            f"SeedArc{index}",
            1.22,
            0.045,
            (0, 0, 1.95),
            m["copper"],
            48,
            rotation=(math.radians(90), 0, math.radians(index * 60)),
        )
    art.sphere("CoreSeed", 0.58, (0, 0, 1.95), m["ivory"])
    art.sphere("CoreHot", 0.30, (0, 0, 1.95), m["hot"])
    art.torus("SeedHalo", 0.78, 0.035, (0, 0, 1.95), m["amber"], 48)
    art.cylinder("Crown", 0.42, 0.16, (0, 0, 2.62), 8, m["ivory"], 0.0)
    for index in range(8):
        angle = math.radians(index * 45 + 22.5)
        art.sphere(
            f"Bolt{index}",
            0.06,
            (math.cos(angle) * 3.02, math.sin(angle) * 3.02, 0.34),
            m["steel"],
        )


def build_worker(m: dict) -> None:
    """Asymmetric tracked frame: excavation arm front-right, sensor mast rear-left."""
    art.box("Chassis", (2.6, 1.60, 0.55), (0, 0, 0.52), mat=m["steel"])
    art.box("ChassisBelly", (2.2, 1.30, 0.24), (0, 0, 0.20), mat=m["dark"])
    for side in (-1, 1):
        art.box(
            "Track",
            (0.42, 1.95, 0.38),
            (side * 1.30, 0, 0.26),
            mat=m["rubber"],
        )
        art.box(
            "TrackTrim",
            (0.46, 1.70, 0.09),
            (side * 1.30, 0, 0.48),
            mat=m["steel"],
        )
    art.box("Cab", (0.80, 0.82, 0.72), (-0.55, -0.34, 1.10), mat=m["dark"])
    art.cylinder(
        "SensorLens", 0.17, 0.07, (-0.55, 0.09, 1.28), 16, m["cyan"]
    )
    art.cylinder("Antenna", 0.035, 1.10, (-0.92, -0.55, 1.50), 8, m["steel"])
    art.sphere("AntennaTip", 0.065, (-0.92, -0.55, 2.08), m["cyan"])

    # Module socket: an empty cradle so replacement modules change the silhouette.
    art.box("SocketFrame", (0.95, 0.55, 0.55), (-0.90, 0.48, 0.92), mat=m["steel"])
    art.box("SocketVoid", (0.72, 0.38, 0.32), (-0.90, 0.48, 0.92), mat=m["rubber"])
    art.box("CyanStripe", (1.05, 0.08, 0.06), (0.15, 0.80, 0.62), mat=m["cyan"])

    # Front excavation arm with a visible elbow and bucket.
    art.box(
        "ArmLower",
        (1.55, 0.30, 0.30),
        (0.95, 0.42, 1.05),
        (0, math.radians(-18), 0.10),
        m["copper"],
    )
    art.cylinder("Elbow", 0.24, 0.36, (1.62, 0.46, 1.30), 12, m["dark"])
    art.box(
        "ArmUpper",
        (1.35, 0.24, 0.24),
        (2.08, 0.52, 1.18),
        (0, math.radians(28), 0.05),
        m["steel"],
    )
    art.box("Bucket", (0.58, 0.66, 0.40), (2.55, 0.56, 0.86), mat=m["dark"])
    art.box("BucketEdge", (0.14, 0.70, 0.10), (2.84, 0.56, 0.70), mat=m["steel"])
    art.box("Counterweight", (0.55, 1.10, 0.55), (-1.55, 0.10, 0.62), mat=m["dark"])


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
        "oblique_scale": 8.6,
        "topdown_scale": 7.6,
        "target_z": 1.3,
        "angles": [0, 36],
    },
    "worker_frame": {
        "build": build_worker,
        "oblique_scale": 7.6,
        "topdown_scale": 7.2,
        "target_z": 0.9,
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
