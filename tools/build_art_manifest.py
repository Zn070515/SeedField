"""Rebuild game/SeedField.Game/Art/manifest.json from runtime art.

The runtime PNGs under Art/style_<route>/ are the source of truth for file names,
directions, hashes and alpha bounds. Pivots are derived from the alpha bounding
box so sprites sit on their world anchor. Normal/emissive passes are optional
siblings named <frame>_normal.png and <frame>_emissive.png.

Usage:
  python tools/build_art_manifest.py
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import struct
import zlib
from pathlib import Path

ASSETS = (
    "power_node",
    "seed_core",
    "seed_core_regenerating",
    "seed_core_bootstrap",
    "seed_core_besieged",
    "worker_frame",
    "worker_frame_drill",
    "worker_frame_sensor",
    "worker_frame_weapon",
    "direction_probe",
)
TOPDOWN_ASSETS = {"power_node", "seed_core", "worker_frame"}
ROUTES = ("oblique", "topdown")
FOOTPRINTS = {
    "power_node": 300,
    "seed_core": 420,
    "seed_core_regenerating": 420,
    "seed_core_bootstrap": 420,
    "seed_core_besieged": 420,
    "worker_frame": 260,
    "worker_frame_drill": 260,
    "worker_frame_sensor": 260,
    "worker_frame_weapon": 260,
    "direction_probe": 260,
}
STATES = {
    "seed_core": "idle",
    "seed_core_regenerating": "regenerating",
    "seed_core_bootstrap": "bootstrap",
    "seed_core_besieged": "besieged",
}
PIVOT_OVERRIDES = {
    "seed_core": [0.5, 0.84],
    "seed_core_regenerating": [0.5, 0.84],
    "seed_core_bootstrap": [0.5, 0.84],
    "seed_core_besieged": [0.5, 0.84],
}


def decode_alpha(
    path: Path,
) -> tuple[tuple[int, int, int, int], float, int, int]:
    """Return (bbox, opaque coverage, width, height) for an 8-bit RGBA PNG."""
    data = path.read_bytes()
    if data[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError(f"{path}: not a PNG")

    width = height = bit_depth = color_type = interlace = 0
    idat = bytearray()
    offset = 8
    while offset < len(data):
        length = struct.unpack(">I", data[offset : offset + 4])[0]
        kind = data[offset + 4 : offset + 8]
        payload = data[offset + 8 : offset + 8 + length]
        offset += 12 + length
        if kind == b"IHDR":
            width, height, bit_depth, color_type = struct.unpack(">IIBB", payload[:10])
            interlace = payload[12]
        elif kind == b"IDAT":
            idat.extend(payload)
        elif kind == b"IEND":
            break

    if bit_depth != 8 or color_type != 6 or interlace != 0:
        raise ValueError(
            f"{path}: expected 8-bit RGBA non-interlaced, got depth={bit_depth} "
            f"color={color_type} interlace={interlace}"
        )

    raw = zlib.decompress(bytes(idat))
    stride = width * 4
    if len(raw) != height * (stride + 1):
        raise ValueError(f"{path}: unexpected decompressed size {len(raw)}")

    previous = bytearray(stride)
    min_x, min_y, max_x, max_y = width, height, -1, -1
    opaque = 0
    cursor = 0
    for y in range(height):
        filter_type = raw[cursor]
        row = bytearray(raw[cursor + 1 : cursor + 1 + stride])
        cursor += stride + 1
        for x in range(stride):
            left = row[x - 4] if x >= 4 else 0
            up = previous[x]
            up_left = previous[x - 4] if x >= 4 else 0
            if filter_type == 1:
                row[x] = (row[x] + left) & 0xFF
            elif filter_type == 2:
                row[x] = (row[x] + up) & 0xFF
            elif filter_type == 3:
                row[x] = (row[x] + ((left + up) >> 1)) & 0xFF
            elif filter_type == 4:
                estimate = left + up - up_left
                pa, pb, pc = (
                    abs(estimate - left),
                    abs(estimate - up),
                    abs(estimate - up_left),
                )
                predictor = left if pa <= pb and pa <= pc else (up if pb <= pc else up_left)
                row[x] = (row[x] + predictor) & 0xFF
            elif filter_type != 0:
                raise ValueError(f"{path}: unknown filter {filter_type}")
        previous = row

        for x in range(width):
            if row[x * 4 + 3] > 8:
                opaque += 1
                min_x = min(min_x, x)
                max_x = max(max_x, x)
                min_y = min(min_y, y)
                max_y = max(max_y, y)

    if max_x < 0:
        raise ValueError(f"{path}: fully transparent")
    coverage = opaque / float(width * height)
    return (min_x, min_y, max_x, max_y), coverage, width, height


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def build_manifest(repo: Path) -> dict:
    art_root = repo / "game" / "SeedField.Game" / "Art"
    source = "art/blender/render_style_assets.py"
    license_name = "original-procedural-source"
    entries = []

    for route in ROUTES:
        folder = art_root / f"style_{route}"
        for name in ASSETS:
            frame_pattern = re.compile(rf"^{re.escape(name)}_{route}_(\d{{3}})\.png$")
            runtime_files = sorted(
                (
                    path
                    for path in folder.glob(f"{name}_{route}_*.png")
                    if frame_pattern.match(path.name)
                ),
                key=lambda path: path.name,
            )
            if not runtime_files:
                if route == "topdown" and name not in TOPDOWN_ASSETS:
                    continue
                raise FileNotFoundError(
                    f"missing runtime assets: {folder}/{name}_{route}_*.png"
                )

            files = []
            front_bottom = None
            front_height = 0
            width = height = 0
            for runtime in runtime_files:
                bbox, coverage, width, height = decode_alpha(runtime)
                if coverage < 0.04:
                    raise ValueError(f"{runtime}: only {coverage:.1%} opaque pixels")
                if coverage > 0.80:
                    raise ValueError(
                        f"{runtime}: {coverage:.1%} opaque pixels; alpha likely broken"
                    )
                if (
                    bbox[0] < 2
                    or bbox[1] < 2
                    or bbox[2] > width - 3
                    or bbox[3] > height - 3
                ):
                    raise ValueError(f"{runtime}: sprite touches the frame edge {bbox}")
                if front_bottom is None:
                    front_bottom = bbox[3]
                    front_height = height

                entry = {
                    "path": f"style_{route}/{runtime.name}",
                    "sha256": sha256(runtime),
                    "alphaBBox": list(bbox),
                }
                for pass_name in ("normal", "emissive"):
                    pass_path = runtime.with_name(f"{runtime.stem}_{pass_name}.png")
                    if not pass_path.exists():
                        continue
                    pass_box, _, pass_width, pass_height = decode_alpha(pass_path)
                    if pass_width != width or pass_height != height:
                        raise ValueError(
                            f"{pass_path}: pass size {pass_width}x{pass_height} "
                            f"does not match diffuse {width}x{height}"
                        )
                    entry[pass_name] = {
                        "path": f"style_{route}/{pass_path.name}",
                        "sha256": sha256(pass_path),
                        "alphaBBox": list(pass_box),
                    }
                files.append(entry)

            if route == "topdown":
                pivot = [0.5, 0.5]
            else:
                pivot = PIVOT_OVERRIDES.get(
                    name,
                    [0.5, round((front_bottom + 1) / float(front_height), 3)],
                )

            asset = {
                "assetId": f"{name}.{route}.v0",
                "kind": "style-test",
                "route": route,
                "directions": len(files),
                "resolutionPx": [width, height],
                "footprintPx": FOOTPRINTS[name],
                "pivot": pivot,
                "source": source,
                "license": license_name,
                "status": "style-test-not-final",
                "files": files,
            }
            if name in STATES:
                asset["state"] = STATES[name]
            entries.append(asset)

    return {
        "schemaVersion": 3,
        "kind": "style-test-assets",
        "status": "not-final-art",
        "notice": "Static style sample. Not gameplay art; see docs/art and art/reference.",
        "assets": entries,
    }


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--check", action="store_true")
    options = parser.parse_args()

    repo = options.repo.resolve()
    manifest_path = repo / "game" / "SeedField.Game" / "Art" / "manifest.json"
    manifest = build_manifest(repo)
    text = json.dumps(manifest, indent=2, ensure_ascii=False) + "\n"

    if options.check:
        if not manifest_path.exists() or manifest_path.read_text(encoding="utf-8") != text:
            raise SystemExit("Art manifest is stale; run python tools/build_art_manifest.py")
        print("ART_MANIFEST_OK")
        return

    manifest_path.write_text(text, encoding="utf-8")
    print(f"ART_MANIFEST_WRITTEN {manifest_path}")


if __name__ == "__main__":
    main()
