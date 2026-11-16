#!/usr/bin/env python3
"""Validate SeedField commit subjects.

Usage:
    python tools/check_commit_message.py
    python tools/check_commit_message.py --range origin/main..HEAD
"""

from __future__ import annotations

import argparse
import re
import subprocess
import sys

TYPES = {
    "feat",
    "fix",
    "docs",
    "test",
    "refactor",
    "perf",
    "build",
    "ci",
    "chore",
    "revert",
}

SUBJECT = re.compile(
    r"^(?P<type>[a-z]+)(?:\((?P<scope>[a-z0-9-]+)\))?: (?P<summary>.{1,72})$"
)
EXEMPT_PREFIXES = ("Merge ", "Revert ", "fixup!", "squash!")


def check(subject: str) -> str | None:
    if subject.startswith(EXEMPT_PREFIXES):
        return None

    match = SUBJECT.match(subject)
    if match is None:
        return "expected '<type>(<scope>): <summary>' with a 1-72 character summary"

    if match.group("type") not in TYPES:
        return f"unknown type '{match.group('type')}'; allowed: {', '.join(sorted(TYPES))}"

    return None


def subjects_for_range(revision_range: str) -> list[str]:
    completed = subprocess.run(
        ["git", "log", "--format=%s", revision_range],
        check=True,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
    )
    return [line for line in completed.stdout.splitlines() if line.strip()]


def main() -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")

    parser = argparse.ArgumentParser()
    parser.add_argument("--range", dest="revision_range")
    args = parser.parse_args()

    if args.revision_range:
        subjects = subjects_for_range(args.revision_range)
    else:
        subjects = subjects_for_range("HEAD")

    if not subjects:
        print("no commit subjects to check")
        return 0

    failures = 0
    for subject in subjects:
        error = check(subject)
        if error is None:
            continue

        failures += 1
        print(f"INVALID: {subject}", file=sys.stderr)
        print(f"         {error}", file=sys.stderr)

    if failures:
        print(f"\n{failures} commit subject(s) failed the convention.", file=sys.stderr)
        return 1

    print(f"commit messages ok: {len(subjects)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
