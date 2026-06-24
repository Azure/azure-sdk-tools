"""Curate raw storage Q&A markdown into canonical staging JSONL.

This is the first step of *dataset preparation* (independent of evaluation runs).
It scans **all** blob content (no time window), parses each ``# Title / ## Question
/ ## Answer`` markdown file into canonical cases, extracts inline links into
``expected_references``, performs **incremental** dedup against already-curated and
already-staged cases, and writes only *new* candidates to
``evaluation_datasets/_staging/<scenario>.jsonl`` with ``reviewed="todo"`` for human review.

Usage (blob, all content):
    python -m dataset.curate

Usage (local md folder, e.g. an existing download):
    python -m dataset.curate --source_md_path online-qa-tests
"""

from __future__ import annotations

import argparse
import hashlib
import json
import logging
import re
import sys
from pathlib import Path

from dotenv import load_dotenv

from .schema import CanonicalCase, REVIEW_STATUS_TODO, iter_jsonl
from ._storage import credential_for, download_md_blobs

# Markdown inline link: [text](url)
_MD_LINK_RE = re.compile(r"\[([^\]]+)\]\((https?://[^)\s]+)\)")


def _repo_paths(script_dir: Path) -> dict[str, Path]:
    return {
        "staging": script_dir / "evaluation_datasets" / "_staging",
        "basic": script_dir / "evaluation_datasets" / "basic",
        "perf": script_dir / "evaluation_datasets" / "perf",
        "md_download": script_dir / "online-qa-tests",
    }


def scenario_from_filename(filename: str) -> str:
    """Leading token before the first underscore, e.g. ``apispec_2026_06_12.md`` -> ``apispec``."""
    base = re.split(r"[\\/]", filename)[-1]
    match = re.match(r"^([^_]+)_", base)
    return match.group(1) if match else Path(base).stem


def normalize_query(query: str) -> str:
    """Stable normalization for dedup: lowercase, collapse whitespace."""
    return re.sub(r"\s+", " ", query.strip().lower())


def case_hash(scenario: str, query: str) -> str:
    return hashlib.sha256(f"{scenario}\x00{normalize_query(query)}".encode("utf-8")).hexdigest()


def extract_links(text: str) -> list[dict[str, str]]:
    """Extract markdown links from text as ``[{title, link}]`` (dedup by link)."""
    refs: list[dict[str, str]] = []
    seen: set[str] = set()
    for match in _MD_LINK_RE.finditer(text):
        title, link = match.group(1).strip(), match.group(2).strip()
        if link in seen:
            continue
        seen.add(link)
        refs.append({"title": title, "link": link})
    return refs


def parse_markdown(file_path: Path) -> list[CanonicalCase]:
    """Parse a Q&A markdown file into canonical cases (one per ``# Title`` section)."""
    scenario = scenario_from_filename(file_path.name)
    with file_path.open("r", encoding="utf-8") as fh:
        lines = fh.readlines()

    # Split into sections starting at each top-level "# " heading, ignoring code blocks.
    sections: list[list[str]] = []
    current: list[str] = []
    in_code_block = False
    for line in lines:
        if line.strip().startswith("```"):
            in_code_block = not in_code_block
            current.append(line)
            continue
        if not in_code_block and line.strip().startswith("# ") and current:
            sections.append(current)
            current = []
        current.append(line)
    if current:
        sections.append(current)

    cases: list[CanonicalCase] = []
    for section in sections:
        title: str | None = None
        question_lines: list[str] = []
        answer_lines: list[str] = []
        in_q = in_a = False
        in_code_block = False

        for raw in section:
            if raw.strip().startswith("```"):
                in_code_block = not in_code_block

            line = raw.rstrip("\n")
            stripped = line.strip()

            if not in_code_block and stripped.startswith("# ") and title is None:
                title = stripped[2:].strip()
                continue
            if not in_code_block and stripped.lower().startswith("## question"):
                in_q, in_a = True, False
                continue
            if not in_code_block and stripped.lower().startswith("## answer"):
                in_q, in_a = False, True
                continue

            if in_q:
                question_lines.append(line)
            elif in_a:
                answer_lines.append(line)

        if not title:
            continue
        question_text = "\n".join(question_lines).strip()
        answer_text = "\n".join(answer_lines).strip()
        if not answer_text:
            continue

        full_question = title
        if question_text:
            full_question = f"title: {title}\n\nquestion: {question_text}"

        cases.append(
            CanonicalCase(
                testcase=title,
                query=full_question,
                ground_truth=answer_text,
                scenario=scenario,
                reviewed=REVIEW_STATUS_TODO,
                tenant=None,  # resolved per-scenario in the eval run template (O6)
                source=file_path.name,
                expected_references=extract_links(answer_text),
                expected_knowledges=[],
            )
        )
    return cases


def load_existing_hashes(paths: dict[str, Path]) -> set[str]:
    """Collect case hashes already curated (basic+perf) or staged, for incremental dedup."""
    hashes: set[str] = set()
    for key in ("basic", "perf", "staging"):
        folder = paths[key]
        if not folder.exists():
            continue
        for f in folder.glob("*.jsonl"):
            for _line_no, obj in iter_jsonl(f):
                scenario = obj.get("scenario") or scenario_from_filename(f.name)
                query = obj.get("query", "")
                if query:
                    hashes.add(case_hash(scenario, query))
    return hashes


def download_all_blobs(dest: Path) -> None:
    """Download every ``.md`` blob from the configured container into ``dest`` (no time filter).

    Dataset preparation runs locally only and authenticates via ``az login``.
    """
    download_md_blobs(dest, credential_for(False))


def curate(source_md_dir: Path, paths: dict[str, Path]) -> dict[str, int]:
    """Parse all md files, dedup against existing, append new candidates to staging.

    Returns a per-scenario count of newly staged cases.
    """
    paths["staging"].mkdir(parents=True, exist_ok=True)
    existing = load_existing_hashes(paths)
    logging.info("Loaded %d existing case hashes for incremental dedup.", len(existing))

    md_files = sorted(source_md_dir.glob("*.md"))
    logging.info("Found %d markdown file(s) in %s.", len(md_files), source_md_dir)

    new_by_scenario: dict[str, list[CanonicalCase]] = {}
    batch_seen: set[str] = set()
    for md in md_files:
        for case in parse_markdown(md):
            h = case_hash(case.scenario, case.query)
            if h in existing or h in batch_seen:
                continue
            batch_seen.add(h)
            new_by_scenario.setdefault(case.scenario, []).append(case)

    counts: dict[str, int] = {}
    for scenario, cases in sorted(new_by_scenario.items()):
        out = paths["staging"] / f"{scenario}.jsonl"
        with out.open("a", encoding="utf-8") as fh:
            for case in cases:
                fh.write(json.dumps(case.to_dict(), ensure_ascii=False) + "\n")
        counts[scenario] = len(cases)
        logging.info("Staged %d new case(s) -> %s", len(cases), out)

    if not counts:
        logging.info("No new cases to stage (all already curated/staged).")
    return counts


def main(argv: list[str] | None = None) -> int:
    logging.basicConfig(level=logging.INFO, stream=sys.stdout, format="%(asctime)s - %(levelname)s - %(message)s")
    parser = argparse.ArgumentParser(description="Curate raw storage Q&A markdown into staging JSONL.")
    parser.add_argument(
        "--source_md_path",
        type=str,
        default=None,
        help="Local folder of md files. If omitted, downloads ALL blobs first.",
    )
    args = parser.parse_args(argv)

    load_dotenv()
    script_dir = Path(__file__).resolve().parent.parent
    paths = _repo_paths(script_dir)

    if args.source_md_path:
        source_dir = Path(args.source_md_path)
        if not source_dir.is_absolute():
            source_dir = script_dir / source_dir
    else:
        source_dir = paths["md_download"]
        try:
            download_all_blobs(source_dir)
        except Exception as exc:  # noqa: BLE001
            logging.exception("Blob download failed: %s", exc)
            return 1

    if not source_dir.exists():
        logging.error("Source md folder does not exist: %s", source_dir)
        return 1

    curate(source_dir, paths)
    return 0


if __name__ == "__main__":
    sys.exit(main())
