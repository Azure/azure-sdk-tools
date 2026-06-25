"""Canonical dataset schema + validator for QA bot evaluation datasets.

A dataset file is JSONL: one JSON object (one ``CanonicalCase``) per line.
"""

from __future__ import annotations

import json
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Iterable, Iterator


# Review status of a curated case (the ``reviewed`` field).
#   todo      - newly curated, awaiting human review
#   pass      - reviewed and accepted (promoted into official datasets)
#   abandoned - reviewed (or left as todo at promote time) and dropped
REVIEW_STATUS_TODO = "todo"
REVIEW_STATUS_PASS = "pass"
REVIEW_STATUS_ABANDONED = "abandoned"
REVIEW_STATUSES = (REVIEW_STATUS_TODO, REVIEW_STATUS_PASS, REVIEW_STATUS_ABANDONED)


def normalize_review_status(value: Any) -> str:
    """Coerce a ``reviewed`` value into a canonical status string.

    Accepts legacy booleans (``True`` -> ``pass``, ``False`` -> ``todo``) for
    backward compatibility, and validates string values against ``REVIEW_STATUSES``.
    """
    if isinstance(value, bool):
        return REVIEW_STATUS_PASS if value else REVIEW_STATUS_TODO
    if isinstance(value, str) and value in REVIEW_STATUSES:
        return value
    raise ValidationError(f"'reviewed' must be one of {REVIEW_STATUSES} (got {value!r})")


# Machine-readable contract (JSON Schema draft-07 shape). Kept in sync with
# ``validate_case`` below; useful for docs/tooling and optional jsonschema use.
JSON_SCHEMA: dict[str, Any] = {
    "$schema": "http://json-schema.org/draft-07/schema#",
    "title": "QA bot evaluation canonical case",
    "type": "object",
    "additionalProperties": True,
    "required": ["testcase", "query", "ground_truth", "scenario", "reviewed"],
    "properties": {
        "testcase": {"type": "string", "minLength": 1},
        "query": {"type": "string", "minLength": 1},
        "ground_truth": {"type": "string", "minLength": 1},
        "expected_references": {
            "type": "array",
            "items": {
                "type": "object",
                "required": ["title", "link"],
                "properties": {
                    "title": {"type": "string"},
                    "link": {"type": "string"},
                },
            },
        },
        "expected_knowledges": {
            "type": "array",
            "items": {
                "type": "object",
                "required": ["title", "link"],
                "properties": {
                    "title": {"type": "string"},
                    "link": {"type": "string"},
                },
            },
        },
        "scenario": {"type": "string", "minLength": 1},
        "tenant": {"type": ["string", "null"]},
        "source": {"type": "string"},
        "reviewed": {"type": "string", "enum": list(REVIEW_STATUSES)},
    },
}


REQUIRED_STR_FIELDS = ("testcase", "query", "ground_truth", "scenario")
REFERENCE_LIST_FIELDS = ("expected_references", "expected_knowledges")


class ValidationError(Exception):
    """Raised when a case or file fails canonical-schema validation."""


@dataclass
class CanonicalCase:
    """A single curated evaluation case (inputs + expectations only)."""

    testcase: str
    query: str
    ground_truth: str
    scenario: str
    reviewed: str = REVIEW_STATUS_TODO
    tenant: str | None = None
    source: str = ""
    expected_references: list[dict[str, str]] = field(default_factory=list)
    expected_knowledges: list[dict[str, str]] = field(default_factory=list)

    def to_dict(self) -> dict[str, Any]:
        return {
            "testcase": self.testcase,
            "query": self.query,
            "ground_truth": self.ground_truth,
            "expected_references": self.expected_references,
            "expected_knowledges": self.expected_knowledges,
            "scenario": self.scenario,
            "tenant": self.tenant,
            "source": self.source,
            "reviewed": self.reviewed,
        }

    @classmethod
    def from_dict(cls, obj: dict[str, Any]) -> "CanonicalCase":
        validate_case(obj)
        return cls(
            testcase=obj["testcase"],
            query=obj["query"],
            ground_truth=obj["ground_truth"],
            scenario=obj["scenario"],
            reviewed=normalize_review_status(obj.get("reviewed", REVIEW_STATUS_TODO)),
            tenant=obj.get("tenant"),
            source=obj.get("source", ""),
            expected_references=list(obj.get("expected_references", []) or []),
            expected_knowledges=list(obj.get("expected_knowledges", []) or []),
        )


def _validate_reference_list(value: Any, field_name: str, where: str) -> None:
    if value is None:
        return
    if not isinstance(value, list):
        raise ValidationError(f"{where}: '{field_name}' must be a list, got {type(value).__name__}")
    for i, item in enumerate(value):
        if not isinstance(item, dict):
            raise ValidationError(f"{where}: '{field_name}[{i}]' must be an object")
        for key in ("title", "link"):
            if key not in item:
                raise ValidationError(f"{where}: '{field_name}[{i}]' missing '{key}'")
            if not isinstance(item[key], str):
                raise ValidationError(f"{where}: '{field_name}[{i}].{key}' must be a string")


def validate_case(obj: Any, where: str = "case") -> None:
    """Validate a single decoded JSON object against the canonical schema.

    Raises ``ValidationError`` with a descriptive message on the first problem.
    """
    if not isinstance(obj, dict):
        raise ValidationError(f"{where}: expected an object, got {type(obj).__name__}")

    for f in REQUIRED_STR_FIELDS:
        if f not in obj:
            raise ValidationError(f"{where}: missing required field '{f}'")
        if not isinstance(obj[f], str) or not obj[f].strip():
            raise ValidationError(f"{where}: '{f}' must be a non-empty string")

    if "reviewed" not in obj:
        raise ValidationError(f"{where}: missing required field 'reviewed'")
    rv = obj["reviewed"]
    if not isinstance(rv, bool) and not (isinstance(rv, str) and rv in REVIEW_STATUSES):
        raise ValidationError(f"{where}: 'reviewed' must be one of {REVIEW_STATUSES} (got {rv!r})")

    if "tenant" in obj and obj["tenant"] is not None and not isinstance(obj["tenant"], str):
        raise ValidationError(f"{where}: 'tenant' must be a string or null")

    if "source" in obj and not isinstance(obj["source"], str):
        raise ValidationError(f"{where}: 'source' must be a string")

    for f in REFERENCE_LIST_FIELDS:
        if f in obj:
            _validate_reference_list(obj[f], f, where)


def iter_jsonl(path: str | Path) -> Iterator[tuple[int, dict[str, Any]]]:
    """Yield ``(line_number, decoded_obj)`` for each non-empty line of a JSONL file."""
    p = Path(path)
    with p.open("r", encoding="utf-8") as fh:
        for line_no, line in enumerate(fh, start=1):
            stripped = line.strip()
            if not stripped:
                continue
            try:
                obj = json.loads(stripped)
            except json.JSONDecodeError as exc:
                raise ValidationError(f"{p}:{line_no}: invalid JSON: {exc}") from exc
            yield line_no, obj


def validate_file(path: str | Path, *, require_reviewed: bool = False) -> int:
    """Validate every line of a JSONL dataset file.

    Args:
        path: dataset JSONL file.
        require_reviewed: when True, also require every row to have ``reviewed=='pass'``
            (used to gate official datasets).

    Returns:
        The number of valid cases.

    Raises:
        ValidationError: on the first invalid row.
    """
    p = Path(path)
    count = 0
    seen_queries: set[str] = set()
    for line_no, obj in iter_jsonl(p):
        where = f"{p}:{line_no}"
        validate_case(obj, where=where)
        if require_reviewed and normalize_review_status(obj.get("reviewed", REVIEW_STATUS_TODO)) != REVIEW_STATUS_PASS:
            raise ValidationError(f"{where}: case is not passed (reviewed != 'pass')")
        # The canonical dedup key is the (normalized) query, applied at curation time;
        # testcase titles may legitimately repeat (e.g. the placeholder "Untitled").
        q = " ".join(obj.get("query", "").split()).lower()
        if q in seen_queries:
            raise ValidationError(f"{where}: duplicate query within file")
        seen_queries.add(q)
        count += 1
    return count


def validate_paths(paths: Iterable[str | Path], *, require_reviewed: bool = False) -> tuple[int, int]:
    """Validate multiple files/folders. Returns ``(files_checked, total_cases)``.

    Folders are expanded to their ``*.jsonl`` files (non-recursive).
    """
    files: list[Path] = []
    for raw in paths:
        p = Path(raw)
        if p.is_dir():
            files.extend(sorted(p.glob("*.jsonl")))
        else:
            files.append(p)

    total = 0
    for f in files:
        total += validate_file(f, require_reviewed=require_reviewed)
    return len(files), total
