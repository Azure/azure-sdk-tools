"""One-time backfill: freeze a stable ``dedup_key`` on every existing curated case.

The dataset dedup identity used to be recomputed from the (normalized) ``query`` on
the fly. That coupled the identity to the exact question text, so editing a question
(e.g. neutralising a stale PR link that makes the bot answer about the PR's *current*
merge state instead of the technical question) would change the hash and cause the
untouched source Q&A to be re-curated as a "new" candidate.

This script persists ``dedup_key = case_hash(scenario, query)`` on each row **while the
query still matches its source**, so the identity is frozen. After running it, questions
in ``evaluation_datasets/{basic,perf,_staging}/*.jsonl`` can be edited freely without
breaking incremental dedup (``curate``/``review`` prefer the stored ``dedup_key``).

Idempotent: rows that already carry a non-empty ``dedup_key`` are left untouched.

Usage:
    python -m dataset.backfill_dedup_key            # all three sets
    python -m dataset.backfill_dedup_key --set perf # one set only
"""

from __future__ import annotations

import argparse
import json
import logging
import sys
from pathlib import Path

from .curate import scenario_from_filename
from .schema import case_hash, iter_jsonl

SETS = ("basic", "perf", "_staging")


def backfill_file(path: Path) -> tuple[int, int]:
    """Add ``dedup_key`` to rows in ``path`` that lack it. Returns (added, total)."""
    rows: list[dict] = []
    added = 0
    for _ln, obj in iter_jsonl(path):
        if not obj.get("dedup_key"):
            scenario = obj.get("scenario") or scenario_from_filename(path.name)
            obj["dedup_key"] = case_hash(scenario, obj.get("query", ""))
            added += 1
        rows.append(obj)
    if added:
        with path.open("w", encoding="utf-8") as fh:
            for obj in rows:
                fh.write(json.dumps(obj, ensure_ascii=False) + "\n")
    return added, len(rows)


def backfill(datasets_dir: Path, only_set: str | None) -> dict[str, int]:
    counts: dict[str, int] = {}
    for name in SETS:
        if only_set and name != only_set:
            continue
        folder = datasets_dir / name
        if not folder.exists():
            continue
        for f in sorted(folder.glob("*.jsonl")):
            added, total = backfill_file(f)
            counts[str(f.relative_to(datasets_dir))] = added
            logging.info("%s: backfilled %d/%d rows", f.relative_to(datasets_dir), added, total)
    return counts


def main(argv: list[str] | None = None) -> int:
    logging.basicConfig(level=logging.INFO, stream=sys.stdout, format="%(asctime)s - %(levelname)s - %(message)s")
    parser = argparse.ArgumentParser(description="Backfill stable dedup_key on existing curated cases.")
    parser.add_argument("--set", dest="only_set", choices=SETS, default=None, help="Only this dataset set.")
    args = parser.parse_args(argv)

    script_dir = Path(__file__).resolve().parent.parent
    datasets_dir = script_dir / "evaluation_datasets"
    if not datasets_dir.exists():
        logging.error("No datasets dir at %s", datasets_dir)
        return 1

    total_added = sum(backfill(datasets_dir, args.only_set).values())
    logging.info("Done. Added dedup_key to %d row(s) total.", total_added)
    return 0


if __name__ == "__main__":
    sys.exit(main())
