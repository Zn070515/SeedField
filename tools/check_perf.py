#!/usr/bin/env python3
"""Compare a JINT_PARALLEL benchmark line with a fixed-machine baseline.

CI keeps a loose absolute budget for shared runners. This tool is for a fixed
development/acceptance machine: run a long benchmark, then compare p95/avg with a
versioned baseline and a relative tolerance.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path


def read_metrics(path: Path) -> dict[str, float]:
    for line in path.read_text(encoding="utf-8").splitlines():
        if "JINT_PARALLEL" not in line:
            continue
        result: dict[str, float] = {}
        for key, value in re.findall(r"([A-Za-z0-9_]+)=([0-9]+(?:\.[0-9]+)?)", line):
            result[key] = float(value)
        return result
    raise SystemExit(f"no JINT_PARALLEL line in {path}")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--baseline", required=True)
    parser.add_argument("--current", required=True)
    parser.add_argument("--devices", type=int, required=True)
    parser.add_argument("--factor", type=float, default=1.25)
    args = parser.parse_args()

    baseline = json.loads(Path(args.baseline).read_text(encoding="utf-8"))
    key = f"jintParallel{args.devices}"
    expected = baseline.get(key)
    if expected is None:
        raise SystemExit(f"baseline has no '{key}' entry")

    current = read_metrics(Path(args.current))
    for metric in ("avgTickMs", "p95Ms"):
        before = float(expected[metric])
        now = current.get(metric)
        if now is None:
            raise SystemExit(f"current benchmark has no '{metric}'")
        limit = before * args.factor
        status = "OK" if now <= limit else "REGRESSION"
        print(
            f"PERF_{status} devices={args.devices} metric={metric} "
            f"baseline={before:.2f} current={now:.2f} limit={limit:.2f}"
        )
        if now > limit:
            return 1

    return 0


if __name__ == "__main__":
    sys.exit(main())
