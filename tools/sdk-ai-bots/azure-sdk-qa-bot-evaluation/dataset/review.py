"""Promote reviewed staging cases into curated per-scenario datasets.

Second step of *dataset preparation*. A human edits the staged files
(``datasets/_staging/<scenario>.jsonl``), fixes ground_truth/links, and sets
``reviewed: true`` on the cases worth keeping. ``review.py`` then **appends** only
the ``reviewed==true`` rows into the committed, curated locations:

    datasets/basic/<scenario>.jsonl     (PR-gate / online curated sets)
    datasets/perf/<scenario>.jsonl      (perf set; grows over time, no target size)

Appending (not overwriting) gives incremental, time-ordered growth. Cases already
present in the target (by scenario+query hash) are skipped, and promoted rows are
removed from staging so each case is reviewed once.

Usage:
    python -m dataset.review --target basic
    python -m dataset.review --target perf --scenario typespec
"""

from __future__ import annotations

import argparse
import json
import logging
import sys
from pathlib import Path

from .curate import case_hash
from .schema import iter_jsonl, validate_case


def _target_hashes(target_dir: Path) -> set[str]:
    hashes: set[str] = set()
    if not target_dir.exists():
        return hashes
    for f in target_dir.glob("*.jsonl"):
        for _ln, obj in iter_jsonl(f):
            if obj.get("query"):
                hashes.add(case_hash(obj.get("scenario", f.stem), obj["query"]))
    return hashes


def review(staging_dir: Path, target_dir: Path, scenario_filter: str | None) -> dict[str, int]:
    """Promote reviewed staging rows into ``target_dir``. Returns per-scenario promoted counts."""
    target_dir.mkdir(parents=True, exist_ok=True)
    existing = _target_hashes(target_dir)

    staging_files = sorted(staging_dir.glob("*.jsonl"))
    if scenario_filter:
        staging_files = [f for f in staging_files if f.stem == scenario_filter]

    promoted: dict[str, int] = {}
    for sf in staging_files:
        scenario = sf.stem
        keep_in_staging: list[dict] = []
        to_promote: list[dict] = []

        for _ln, obj in iter_jsonl(sf):
            if not obj.get("reviewed", False):
                keep_in_staging.append(obj)
                continue
            validate_case(obj, where=f"{sf}")
            h = case_hash(obj.get("scenario", scenario), obj.get("query", ""))
            if h in existing:
                continue  # already curated; drop the reviewed duplicate
            existing.add(h)
            to_promote.append(obj)

        if to_promote:
            target = target_dir / f"{scenario}.jsonl"
            with target.open("a", encoding="utf-8") as fh:
                for obj in to_promote:
                    fh.write(json.dumps(obj, ensure_ascii=False) + "\n")
            promoted[scenario] = len(to_promote)
            logging.info("Promoted %d case(s) -> %s", len(to_promote), target)

        # Rewrite staging without the promoted rows.
        if to_promote:
            if keep_in_staging:
                with sf.open("w", encoding="utf-8") as fh:
                    for obj in keep_in_staging:
                        fh.write(json.dumps(obj, ensure_ascii=False) + "\n")
            else:
                sf.unlink()

    if not promoted:
        logging.info("No reviewed cases to promote.")
    return promoted


def main(argv: list[str] | None = None) -> int:
    logging.basicConfig(level=logging.INFO, stream=sys.stdout, format="%(asctime)s - %(levelname)s - %(message)s")
    parser = argparse.ArgumentParser(description="Promote reviewed staging cases into curated datasets.")
    parser.add_argument("--target", choices=["basic", "perf"], required=True, help="Curated target set.")
    parser.add_argument("--scenario", type=str, default=None, help="Only this scenario (file stem).")
    args = parser.parse_args(argv)

    script_dir = Path(__file__).resolve().parent.parent
    staging_dir = script_dir / "datasets" / "_staging"
    target_dir = script_dir / "datasets" / args.target

    if not staging_dir.exists():
        logging.error("No staging folder at %s", staging_dir)
        return 1

    review(staging_dir, target_dir, args.scenario)
    return 0


if __name__ == "__main__":
    sys.exit(main())
