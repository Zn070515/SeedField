#!/usr/bin/env python3
"""Coverage ratchet for the core assemblies.

The floors are deliberately below the current measured rates so the gate catches
regressions without blocking new code. Raise a floor only after the measured rate
has stayed above the new floor for a full release cycle.
"""

from __future__ import annotations

import sys
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

# assembly -> (minimum line rate, minimum branch rate)
FLOORS: dict[str, tuple[float, float]] = {
    "SeedField.Simulation": (0.80, 0.65),
    "SeedField.Runtime.Js": (0.70, 0.55),
    "SeedField.PackageHost": (0.70, 0.60),
}


def main() -> int:
    files = sorted(ROOT.glob("tests/**/TestResults/**/coverage.cobertura.xml"))
    if not files:
        print("NO_COVERAGE: run dotnet test with --collect:'XPlat Code Coverage'")
        return 1

    measured: dict[str, tuple[float, float]] = {}
    for path in files:
        try:
            root = ET.parse(path).getroot()
        except ET.ParseError as error:
            print(f"INVALID_COVERAGE {path}: {error}")
            return 1

        for package in root.findall(".//package"):
            name = package.get("name", "")
            for expected in FLOORS:
                if name == expected or name.endswith("." + expected):
                    line = float(package.get("line-rate", "0"))
                    branch = float(package.get("branch-rate", "0"))
                    previous = measured.get(expected, (0.0, 0.0))
                    measured[expected] = (
                        max(previous[0], line),
                        max(previous[1], branch),
                    )

    failures: list[str] = []
    for assembly, (line_floor, branch_floor) in FLOORS.items():
        if assembly not in measured:
            failures.append(f"{assembly}: no coverage package found")
            continue

        line, branch = measured[assembly]
        status = "OK" if line >= line_floor and branch >= branch_floor else "FAIL"
        print(
            f"COVERAGE {assembly} line={line:.2%} branch={branch:.2%} "
            f"floor={line_floor:.0%}/{branch_floor:.0%} {status}"
        )
        if status == "FAIL":
            failures.append(
                f"{assembly}: line {line:.2%} < {line_floor:.0%} or "
                f"branch {branch:.2%} < {branch_floor:.0%}"
            )

    if failures:
        for failure in failures:
            print(f"COVERAGE_FAILURE {failure}")
        return 1

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
