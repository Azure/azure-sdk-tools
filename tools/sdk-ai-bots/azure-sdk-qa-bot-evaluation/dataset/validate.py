"""CLI entry for canonical dataset schema validation.

Usage:
    python -m dataset.validate <path-or-folder> [<path-or-folder> ...] [--require-reviewed]

Exits non-zero on the first invalid row so it can gate CI and pre-commit.
"""

from __future__ import annotations

import argparse
import logging
import sys

from .schema import ValidationError, validate_paths


def main(argv: list[str] | None = None) -> int:
    logging.basicConfig(level=logging.INFO, format="%(levelname)s: %(message)s")
    parser = argparse.ArgumentParser(description="Validate canonical evaluation dataset JSONL files.")
    parser.add_argument("paths", nargs="+", help="JSONL files or folders to validate")
    parser.add_argument(
        "--require-reviewed",
        action="store_true",
        help="Require every row to have reviewed=='pass' (gate for official datasets).",
    )
    args = parser.parse_args(argv)

    try:
        files, total = validate_paths(args.paths, require_reviewed=args.require_reviewed)
    except ValidationError as exc:
        logging.error("Validation failed: %s", exc)
        return 1

    logging.info("✅ Validated %d case(s) across %d file(s).", total, files)
    return 0


if __name__ == "__main__":
    sys.exit(main())
