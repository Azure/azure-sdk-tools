"""Prepare an ephemeral per-scenario JSONL snapshot for the **online** (weekly) eval.

Unlike ``dataset.curate`` (which incrementally stages *new, deduped* candidates for
human review), this downloads the Q&A markdown uploaded in the last ``--days_before``
days and converts *all* of it — no dedup, no staging — into one JSONL per scenario,
ready to be fed straight into ``evals_run.py``.

Usage (CI):
    python -m dataset.online_snapshot --is_ci True --days_before 21 --dest online-tests

Usage (local, az login):
    python -m dataset.online_snapshot --is_ci False
"""

from __future__ import annotations

import argparse
import json
import logging
import sys
from datetime import datetime, timedelta
from pathlib import Path

from dotenv import load_dotenv

from .curate import parse_markdown
from ._storage import credential_for, download_md_blobs


def _reset_dir(path: Path, pattern: str) -> None:
    """Create ``path`` and remove any stale files matching ``pattern`` (fresh snapshot)."""
    path.mkdir(parents=True, exist_ok=True)
    for f in path.glob(pattern):
        f.unlink()


def download_recent_blobs(dest: Path, days_before: int, credential) -> int:
    """Download every ``.md`` blob stamped within the last ``days_before`` days into ``dest``."""
    _reset_dir(dest, "*.md")
    cutoff = datetime.today() - timedelta(days=days_before)
    return download_md_blobs(dest, credential, since=cutoff)


def convert_to_scenario_jsonl(md_dir: Path, dest_dir: Path) -> dict[str, int]:
    """Parse all md in ``md_dir`` into one ``<scenario>.jsonl`` per scenario in ``dest_dir``."""
    _reset_dir(dest_dir, "*.jsonl")

    by_scenario: dict[str, list[dict]] = {}
    for md in sorted(md_dir.glob("*.md")):
        for case in parse_markdown(md):
            by_scenario.setdefault(case.scenario, []).append(case.to_dict())

    counts: dict[str, int] = {}
    for scenario, cases in sorted(by_scenario.items()):
        out = dest_dir / f"{scenario}.jsonl"
        with out.open("w", encoding="utf-8") as fh:
            for obj in cases:
                fh.write(json.dumps(obj, ensure_ascii=False) + "\n")
        counts[scenario] = len(cases)
        logging.info("Wrote %d case(s) -> %s", len(cases), out)
    return counts


def main(argv: list[str] | None = None) -> int:
    logging.basicConfig(level=logging.INFO, stream=sys.stdout, format="%(asctime)s - %(levelname)s - %(message)s")
    parser = argparse.ArgumentParser(description="Snapshot recent storage md into per-scenario JSONL for online eval.")
    parser.add_argument("--days_before", type=int, default=21, help="Only md stamped within this many days.")
    parser.add_argument("--is_ci", type=str, default="True", help="Run in CI (True/False) — selects credential.")
    parser.add_argument("--md_folder", type=str, default="online-qa-tests", help="Where to download md.")
    parser.add_argument("--dest", type=str, default="online-tests", help="Where to write per-scenario JSONL.")
    args = parser.parse_args(argv)

    is_ci = args.is_ci.lower() in ("true", "1", "yes", "on")

    load_dotenv()
    script_dir = Path(__file__).resolve().parent.parent

    def _resolve(p: str) -> Path:
        path = Path(p)
        return path if path.is_absolute() else (script_dir / path)

    md_dir = _resolve(args.md_folder)
    dest_dir = _resolve(args.dest)

    try:
        n = download_recent_blobs(md_dir, args.days_before, credential_for(is_ci))
        logging.info("Downloaded %d md file(s) from the last %d day(s).", n, args.days_before)
    except Exception as exc:  # noqa: BLE001
        logging.exception("Blob download failed: %s", exc)
        return 1

    counts = convert_to_scenario_jsonl(md_dir, dest_dir)
    if not counts:
        logging.warning("No cases produced from the recent md window (%d day(s)).", args.days_before)
    else:
        logging.info("Snapshot ready: %s", ", ".join(f"{k}={v}" for k, v in counts.items()))
    return 0


if __name__ == "__main__":
    sys.exit(main())
