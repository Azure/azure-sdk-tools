#!/usr/bin/env python3
# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""
Checks that all Python source files contain the Microsoft copyright header.

Usage:
    python scripts/check_copyright_headers.py          # Check all .py files
    python scripts/check_copyright_headers.py --fix    # Add missing headers automatically
"""

import argparse
import os
import sys

_SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
_PACKAGE_ROOT = os.path.abspath(os.path.join(_SCRIPT_DIR, ".."))

_HEADER = (
    "# -------------------------------------------------------------------------\n"
    "# Copyright (c) Microsoft Corporation. All rights reserved.\n"
    "# Licensed under the MIT License. See License.txt in the project root for\n"
    "# license information.\n"
    "# --------------------------------------------------------------------------\n"
)

_COPYRIGHT_LINE = "# Copyright (c) Microsoft Corporation. All rights reserved."

# Directories to scan (relative to package root)
_SCAN_DIRS = ["src", "scripts", "tests", "evals"]

# Also check these root-level files
_ROOT_FILES = ["app.py", "cli.py", "setup.py"]


def _find_python_files():
    """Yield all .py files that should have the copyright header."""
    for root_file in _ROOT_FILES:
        path = os.path.join(_PACKAGE_ROOT, root_file)
        if os.path.isfile(path):
            yield path

    for scan_dir in _SCAN_DIRS:
        dir_path = os.path.join(_PACKAGE_ROOT, scan_dir)
        if not os.path.isdir(dir_path):
            continue
        for dirpath, _, filenames in os.walk(dir_path):
            # Skip __pycache__ directories
            if "__pycache__" in dirpath:
                continue
            for filename in sorted(filenames):
                if filename.endswith(".py"):
                    yield os.path.join(dirpath, filename)


def _has_copyright(filepath):
    """Check if a file contains the Microsoft copyright line."""
    try:
        with open(filepath, "r", encoding="utf-8") as f:
            # Check first 10 lines for the copyright notice
            for _ in range(10):
                line = f.readline()
                if not line:
                    break
                if _COPYRIGHT_LINE in line:
                    return True
    except (OSError, UnicodeDecodeError):
        return True  # Skip files we can't read
    return False


def _add_header(filepath):
    """Prepend the copyright header to a file."""
    with open(filepath, "r", encoding="utf-8") as f:
        content = f.read()

    with open(filepath, "w", encoding="utf-8") as f:
        if content.strip():
            f.write(_HEADER + "\n" + content)
        else:
            f.write(_HEADER)


def main():
    parser = argparse.ArgumentParser(description="Check Python files for Microsoft copyright headers.")
    parser.add_argument(
        "--fix",
        action="store_true",
        help="Automatically add missing headers instead of just reporting.",
    )
    args = parser.parse_args()

    missing = []
    for filepath in _find_python_files():
        if not _has_copyright(filepath):
            rel = os.path.relpath(filepath, _PACKAGE_ROOT)
            missing.append((filepath, rel))

    if not missing:
        print("All Python source files have the Microsoft copyright header.")
        return 0

    if args.fix:
        for filepath, rel in missing:
            _add_header(filepath)
            print(f"  Fixed: {rel}")
        print(f"\nAdded copyright header to {len(missing)} file(s).")
        return 0

    print("The following files are missing the Microsoft copyright header:\n")
    for _, rel in missing:
        print(f"  {rel}")
    print(f"\n{len(missing)} file(s) missing headers.")
    print("Run with --fix to add them automatically:")
    print("  python scripts/check_copyright_headers.py --fix")
    return 1


if __name__ == "__main__":
    sys.exit(main())
