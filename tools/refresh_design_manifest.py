"""Recompute size and SHA-256 for every file listed in the design-bible manifest.

Usage: python tools/refresh_design_manifest.py
"""

from __future__ import annotations

import hashlib
import json
import pathlib
import sys

ROOT = pathlib.Path(__file__).resolve().parents[1] / "docs" / "design-bible" / "v1.9-rev2"
MANIFEST = ROOT / "MANIFEST.json"


def main() -> int:
    data = json.loads(MANIFEST.read_text(encoding="utf-8"))
    changed: list[str] = []

    for entry in data["files"]:
        path = ROOT / entry["path"]
        if not path.is_file():
            print(f"MISSING: {entry['path']}", file=sys.stderr)
            return 1

        payload = path.read_bytes()
        size = len(payload)
        digest = hashlib.sha256(payload).hexdigest()

        if entry.get("size_bytes") != size or entry.get("sha256") != digest:
            changed.append(entry["path"])

        entry["size_bytes"] = size
        entry["sha256"] = digest

    data["file_count"] = len(data["files"])
    MANIFEST.write_text(
        json.dumps(data, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )

    print(f"manifest refreshed: {len(data['files'])} files, {len(changed)} changed")
    for path in changed:
        print(f"  updated {path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
