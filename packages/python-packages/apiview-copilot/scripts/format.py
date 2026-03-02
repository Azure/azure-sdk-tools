#!/usr/bin/env python3
# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""
Runs Black formatter on all Python source files in the project.

Usage:
    python scripts/format.py          # Format all files in place
    python scripts/format.py --check  # Check only (exit 1 if changes needed)
"""

import subprocess
import sys

_TARGETS = ["src", "scripts", "tests", "evals", "app.py", "cli.py", "setup.py"]


def main():
    args = ["black"]
    if "--check" in sys.argv:
        args.append("--check")
    args.extend(_TARGETS)

    result = subprocess.run(args, check=False)
    sys.exit(result.returncode)


if __name__ == "__main__":
    main()
