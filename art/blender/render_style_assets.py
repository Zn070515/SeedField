"""Render SeedField Gate 2 style assets.

Routes:
  oblique  - 35 degree orthographic three-quarter
  topdown  - 90 degree strict top-down

Asset sets:
  power_node / seed_core / worker_frame  - existing A/B style assets
  worker_frame_drill / _sensor           - swappable module silhouettes
  seed_core_<state>                      - idle / regenerating / bootstrap / besieged
  direction_probe                        - 16-angle worker for 4/8/16 comparison

Usage:
  blender --background --factory-startup --python art/blender/render_style_assets.py -- \
      --route oblique --output art/generated/style_oblique --size 512 \
      --asset worker_frame_drill --passes normal,emissive
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import sys
import time
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
    parser.add_argument("--asset", default="all")
    parser.add_argument("--passes", default="")
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
        "alarm": art.material(
            "Alarm", (0.42, 0.03, 0.01), 0.2, 0.3, (1.0, 0.08, 0.03), 6.0
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


def build_core(m: dict, state: str = "idle") -> None:
    """Open five-fold cradle; state only changes emitters, cages and petals."""
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

    petal_tilt = 34 if state == "bootstrap" else 24
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
            (0, math.radians(petal_tilt), angle),
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

    if state == "regenerating":
        for index, z in enumerate((1.18, 1.65, 2.12)):
            art.torus(
                f"RegenRing{index}",
                1.58 - index * 0.20,
                0.04,
                (0, 0, z),
                m["cyan"],
                48,
            )
        art.cylinder("RegenColumn", 0.12, 2.15, (0, 0, 1.90), 16, m["cyan"])
        art.sphere("RegenTip", 0.12, (0, 0, 3.02), m["hot"])
    elif state == "bootstrap":
        art.cylinder("BootstrapBeam", 0.24, 2.60, (0, 0, 2.15), 16, m["hot"])
        art.torus("BootstrapRing", 0.96, 0.05, (0, 0, 3.02), m["amber"], 48)
        for index in range(5):
            angle = math.radians(index * 72 + 18)
            art.box(
                f"BootstrapTab{index}",
                (0.34, 0.26, 0.42),
                (math.cos(angle) * 2.65, math.sin(angle) * 2.65, 0.72),
                (0, 0, angle),
                m["amber"],
            )
    elif state == "besieged":
        for index in range(6):
            angle = math.radians(index * 60 + 30)
            art.box(
                f"CageBar{index}",
                (0.16, 0.16, 2.55),
                (math.cos(angle) * 2.86, math.sin(angle) * 2.86, 1.55),
                (0, 0, angle),
                m["dark"],
            )
        art.torus("SiegeRing", 2.90, 0.065, (0, 0, 0.78), m["alarm"], 48)
        for index in range(8):
            angle = math.radians(index * 45 + 22.5)
            art.sphere(
                f"AlarmLight{index}",
                0.11,
                (math.cos(angle) * 3.12, math.sin(angle) * 3.12, 2.62),
                m["alarm"],
            )

    for index in range(8):
        angle = math.radians(index * 45 + 22.5)
        art.sphere(
            f"Bolt{index}",
            0.06,
            (math.cos(angle) * 3.02, math.sin(angle) * 3.02, 0.34),
            m["steel"],
        )


def build_worker(m: dict, module: str = "none") -> None:
    """Asymmetric tracked frame; module changes the socket and front silhouette."""
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
    art.cylinder("SensorLens", 0.17, 0.07, (-0.55, 0.09, 1.28), 16, m["cyan"])
    art.cylinder("Antenna", 0.035, 0.85, (-0.92, -0.55, 1.40), 8, m["steel"])
    art.sphere("AntennaTip", 0.065, (-0.92, -0.55, 1.85), m["cyan"])
    art.box("CyanStripe", (1.05, 0.08, 0.06), (0.15, 0.80, 0.62), mat=m["cyan"])
    art.box("Counterweight", (0.55, 1.10, 0.55), (-1.55, 0.10, 0.62), mat=m["dark"])

    # Rear socket: empty on the base frame, occupied by either module.
    art.box("SocketFrame", (0.95, 0.55, 0.55), (-0.90, 0.48, 0.92), mat=m["steel"])
    if module == "none":
        art.box("SocketVoid", (0.72, 0.38, 0.32), (-0.90, 0.48, 0.92), mat=m["rubber"])
        return

    if module == "drill":
        art.box("ModuleBox", (0.86, 0.50, 0.62), (-0.90, 0.48, 0.96), mat=m["copper"])
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
        return

    if module == "sensor":
        art.box("ModuleBox", (0.82, 0.48, 0.52), (-0.90, 0.48, 0.90), mat=m["steel"])
        art.cylinder("SensorMast", 0.075, 1.95, (-0.90, 0.48, 1.90), 12, m["steel"])
        for index, (z, radius) in enumerate(((1.45, 0.34), (2.02, 0.50), (2.58, 0.66))):
            art.torus(
                f"ArrayRing{index}",
                radius,
                0.035,
                (-0.90, 0.48, z),
                m["cyan"] if index == 2 else m["steel"],
                32,
            )
        art.sphere("ArrayCore", 0.22, (-0.90, 0.48, 2.72), m["hot"])
        art.box("ArrayBrace", (0.42, 0.30, 0.30), (-0.90, 0.48, 2.10), mat=m["dark"])
        return

    raise ValueError(f"unknown worker module '{module}'")


def build_worker_base(m: dict) -> None:
    build_worker(m, "none")


def build_worker_drill(m: dict) -> None:
    build_worker(m, "drill")


def build_worker_sensor(m: dict) -> None:
    build_worker(m, "sensor")


def build_direction_probe(m: dict) -> None:
    build_worker(m, "drill")


def build_core_idle(m: dict) -> None:
    build_core(m, "idle")


def build_core_regenerating(m: dict) -> None:
    build_core(m, "regenerating")


def build_core_bootstrap(m: dict) -> None:
    build_core(m, "bootstrap")


def build_core_besieged(m: dict) -> None:
    build_core(m, "besieged")


WORKER_ANGLES = [0, 45, 90, 135, 180, 225, 270, 315]
PROBE_ANGLES = [0, 23, 45, 68, 90, 113, 135, 158, 180, 203, 225, 248, 270, 293, 315, 338]

ASSETS = {
    "power_node": {
        "build": build_power,
        "oblique_scale": 7.4,
        "topdown_scale": 6.4,
        "target_z": 1.8,
        "angles": [0, 90, 180, 270],
        "topdown": True,
        "masks": True,
    },
    "seed_core": {
        "build": build_core_idle,
        "oblique_scale": 8.6,
        "topdown_scale": 7.6,
        "target_z": 1.3,
        "angles": [0, 36],
        "topdown": True,
        "masks": True,
        "state": "idle",
    },
    "seed_core_regenerating": {
        "build": build_core_regenerating,
        "oblique_scale": 8.6,
        "topdown_scale": 7.6,
        "target_z": 1.3,
        "angles": [0],
        "topdown": False,
        "masks": True,
        "state": "regenerating",
    },
    "seed_core_bootstrap": {
        "build": build_core_bootstrap,
        "oblique_scale": 8.6,
        "topdown_scale": 7.6,
        "target_z": 1.3,
        "angles": [0],
        "topdown": False,
        "masks": True,
        "state": "bootstrap",
    },
    "seed_core_besieged": {
        "build": build_core_besieged,
        "oblique_scale": 8.6,
        "topdown_scale": 7.6,
        "target_z": 1.3,
        "angles": [0],
        "topdown": False,
        "masks": True,
        "state": "besieged",
    },
    "worker_frame": {
        "build": build_worker_base,
        "oblique_scale": 7.6,
        "topdown_scale": 7.2,
        "target_z": 0.9,
        "angles": WORKER_ANGLES,
        "topdown": True,
        "masks": False,
    },
    "worker_frame_drill": {
        "build": build_worker_drill,
        "oblique_scale": 7.6,
        "topdown_scale": 7.2,
        "target_z": 0.9,
        "angles": WORKER_ANGLES,
        "topdown": False,
        "masks": True,
    },
    "worker_frame_sensor": {
        "build": build_worker_sensor,
        "oblique_scale": 7.6,
        "topdown_scale": 7.2,
        "target_z": 0.9,
        "angles": WORKER_ANGLES,
        "topdown": False,
        "masks": True,
    },
    "direction_probe": {
        "build": build_direction_probe,
        "oblique_scale": 7.6,
        "topdown_scale": 7.2,
        "target_z": 0.9,
        "angles": PROBE_ANGLES,
        "topdown": False,
        "masks": False,
    },
}


def parse_passes(value: str) -> set[str]:
    wanted = {part.strip() for part in value.split(",") if part.strip()}
    if "all" in wanted:
        return {"normal", "emissive"}
    return wanted


def render_emissive(output: Path) -> None:
    """Render only emitted materials; other surfaces become pure black occluders."""
    black = bpy.data.materials.new("MaskBlack")
    black.use_nodes = True
    nodes = black.node_tree.nodes
    nodes.clear()
    emission = nodes.new("ShaderNodeEmission")
    emission.inputs["Color"].default_value = (0.0, 0.0, 0.0, 1.0)
    output_node = nodes.new("ShaderNodeOutputMaterial")
    black.node_tree.links.new(emission.outputs["Emission"], output_node.inputs["Surface"])

    for obj in bpy.context.scene.objects:
        if obj.type != "MESH":
            continue
        source = None
        for slot in obj.material_slots:
            mat = slot.material
            if mat is None or not mat.use_nodes:
                continue
            bsdf = mat.node_tree.nodes.get("Principled BSDF")
            if bsdf is None:
                continue
            strength = float(bsdf.inputs["Emission Strength"].default_value)
            if strength <= 0.5:
                continue
            key = "Emission Color" if "Emission Color" in bsdf.inputs else "Emission"
            color = tuple(bsdf.inputs[key].default_value)[:3]
            source = (color, strength)
            break

        if source is None:
            obj.data.materials.clear()
            obj.data.materials.append(black)
            continue

        color, strength = source
        emissive = bpy.data.materials.new(f"MaskEmit_{obj.name}")
        emissive.use_nodes = True
        em_nodes = emissive.node_tree.nodes
        em_nodes.clear()
        em_node = em_nodes.new("ShaderNodeEmission")
        em_node.inputs["Color"].default_value = (*color, 1.0)
        em_node.inputs["Strength"].default_value = min(2.0, max(0.5, strength / 3.0))
        em_out = em_nodes.new("ShaderNodeOutputMaterial")
        emissive.node_tree.links.new(em_node.outputs["Emission"], em_out.inputs["Surface"])
        obj.data.materials.clear()
        obj.data.materials.append(emissive)

    scene = bpy.context.scene
    scene.render.film_transparent = True
    scene.view_settings.view_transform = "Raw"
    scene.view_settings.look = "None"
    scene.view_settings.exposure = 0.0
    scene.view_settings.gamma = 1.0
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.filepath = str(output)
    bpy.ops.render.render(write_still=True)


def render_normal(output: Path) -> None:
    """Render a camera-space normal map with an emission material override."""
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 8
    scene.render.film_transparent = True
    scene.view_settings.view_transform = "Raw"
    scene.view_settings.look = "None"
    scene.view_settings.exposure = 0.0
    scene.view_settings.gamma = 1.0

    mat = bpy.data.materials.new("MaskNormal")
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    nodes.clear()

    geometry = nodes.new("ShaderNodeNewGeometry")
    transform = nodes.new("ShaderNodeVectorTransform")
    transform.vector_type = "NORMAL"
    transform.convert_from = "WORLD"
    transform.convert_to = "CAMERA"
    flip = nodes.new("ShaderNodeVectorMath")
    flip.operation = "MULTIPLY"
    flip.inputs[1].default_value = (1.0, 1.0, -1.0)
    scale = nodes.new("ShaderNodeVectorMath")
    scale.operation = "MULTIPLY"
    scale.inputs[1].default_value = (0.5, 0.5, 0.5)
    offset = nodes.new("ShaderNodeVectorMath")
    offset.operation = "ADD"
    offset.inputs[1].default_value = (0.5, 0.5, 0.5)
    emission = nodes.new("ShaderNodeEmission")
    emission.inputs["Strength"].default_value = 1.0
    output_node = nodes.new("ShaderNodeOutputMaterial")

    links.new(geometry.outputs["Normal"], transform.inputs["Vector"])
    links.new(transform.outputs["Vector"], flip.inputs[0])
    links.new(flip.outputs["Vector"], scale.inputs[0])
    links.new(scale.outputs["Vector"], offset.inputs[0])
    links.new(offset.outputs["Vector"], emission.inputs["Color"])
    links.new(emission.outputs["Emission"], output_node.inputs["Surface"])

    for obj in scene.objects:
        if obj.type == "MESH":
            obj.data.materials.clear()
            obj.data.materials.append(mat)

    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.filepath = str(output)
    bpy.ops.render.render(write_still=True)

def main() -> None:
    options = args()
    output_root = Path(options.output).resolve()
    output_root.mkdir(parents=True, exist_ok=True)
    route = options.route
    selected = (
        {name.strip() for name in options.asset.split(",") if name.strip()}
        if options.asset != "all"
        else set(ASSETS)
    )
    passes = parse_passes(options.passes)
    report = {"route": route, "assets": []}

    for name, spec in ASSETS.items():
        if name not in selected:
            continue
        if route == "topdown" and not spec.get("topdown", False):
            continue

        art.clear_scene()
        mats = materials()
        spec["build"](mats)
        art.setup_lights(spec["target_z"], route)
        ortho = spec["oblique_scale"] if route == "oblique" else spec["topdown_scale"]
        rig = art.setup_camera(route, ortho, spec["target_z"])

        angles = [0] if route == "topdown" else spec["angles"]
        files = []
        asset_start = time.perf_counter()
        for angle in angles:
            art.configure_render(options.size)
            path = output_root / f"{name}_{route}_{angle:03d}.png"
            art.render_angle(rig, angle, path)
            files.append(
                {
                    "angle": angle,
                    "file": path.name,
                    "sha256": sha256(path),
                    "bytes": path.stat().st_size,
                }
            )

        if spec.get("masks", False) and passes:
            rig.rotation_euler[2] = 0.0
            bpy.context.view_layer.update()
            if "emissive" in passes:
                emissive_path = output_root / f"{name}_{route}_000_emissive.png"
                render_emissive(emissive_path)
                files[0]["emissive"] = emissive_path.name
            if "normal" in passes:
                normal_path = output_root / f"{name}_{route}_000_normal.png"
                render_normal(normal_path)
                files[0]["normal"] = normal_path.name

        elapsed = time.perf_counter() - asset_start
        meta = {
            "schemaVersion": 2,
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
            "notice": "Style test; not final art. Validate in Godot before merge.",
        }
        if "state" in spec:
            meta["state"] = spec["state"]
        art.write_meta(output_root / f"{name}_{route}.meta.json", meta)
        report["assets"].append(
            {
                "name": name,
                "directions": len(angles),
                "seconds": round(elapsed, 3),
                "files": files,
            }
        )
        print(f"STYLE_ASSET {name} {route} dirs={len(angles)} seconds={elapsed:.2f}")

    report_path = output_root / f"render-report-{route}.json"
    report_path.write_text(
        json.dumps(report, indent=2, ensure_ascii=False) + "\n", encoding="utf-8"
    )
    print(f"STYLE_ASSETS_RENDERED {output_root}")
    print(f"STYLE_RENDER_REPORT {report_path}")


if __name__ == "__main__":
    main()
