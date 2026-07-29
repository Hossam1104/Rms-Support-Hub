#!/usr/bin/env python3
"""Warn when always-used AI context files grow beyond the agreed budget."""

from __future__ import annotations

from pathlib import Path

LIMITS = {
    "AGENTS.md": (140, 12_000),
    "CLAUDE.md": (10, 1_000),
    "TASK.md": (120, 10_000),
    ".ai/STATE.md": (120, 10_000),
    ".ai/PROJECT.md": (160, 14_000),
    ".ai/DECISIONS.md": (160, 14_000),
    ".ai/HANDOFF.md": (40, 5_000),
}


def main() -> int:
    root = Path(__file__).resolve().parents[2]
    failed = False

    for relative, (line_limit, byte_limit) in LIMITS.items():
        path = root / relative
        if not path.exists():
            print(f"MISSING  {relative}")
            failed = True
            continue

        raw = path.read_bytes()
        lines = raw.decode("utf-8", errors="replace").splitlines()
        line_count = len(lines)
        byte_count = len(raw)
        okay = line_count <= line_limit and byte_count <= byte_limit
        label = "OK" if okay else "TOO LARGE"
        print(
            f"{label:9} {relative}: {line_count}/{line_limit} lines, "
            f"{byte_count}/{byte_limit} bytes"
        )
        failed = failed or not okay

    return 1 if failed else 0


if __name__ == "__main__":
    raise SystemExit(main())
