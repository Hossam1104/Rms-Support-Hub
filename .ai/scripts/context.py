#!/usr/bin/env python3
"""Print a bounded Git context summary for coding-agent startup."""

from __future__ import annotations

import subprocess
import sys
from pathlib import Path

MAX_CHANGED_FILES = 30


def git(*args: str) -> str:
    result = subprocess.run(
        ["git", *args],
        text=True,
        capture_output=True,
        check=False,
    )
    if result.returncode != 0:
        message = result.stderr.strip() or result.stdout.strip()
        raise RuntimeError(message or f"git {' '.join(args)} failed")
    return result.stdout.strip()


def main() -> int:
    try:
        root = Path(git("rev-parse", "--show-toplevel"))
        branch = git("branch", "--show-current") or "DETACHED"
        head = git("log", "-1", "--pretty=%h %s")
        status_lines = [line for line in git("status", "--short").splitlines() if line]
        diff_stat = git("diff", "--stat")
        staged_stat = git("diff", "--cached", "--stat")
    except RuntimeError as exc:
        print(f"Context error: {exc}", file=sys.stderr)
        return 1

    print(f"Repository: {root}")
    print(f"Branch: {branch}")
    print(f"HEAD: {head}")
    print(f"Changed files: {len(status_lines)}")

    for line in status_lines[:MAX_CHANGED_FILES]:
        print(f"  {line}")
    if len(status_lines) > MAX_CHANGED_FILES:
        print(f"  ... {len(status_lines) - MAX_CHANGED_FILES} more omitted")

    print("\nUnstaged diff summary:")
    print(diff_stat or "  None")
    print("\nStaged diff summary:")
    print(staged_stat or "  None")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
