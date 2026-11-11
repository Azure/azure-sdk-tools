"""Canonical dataset schema + validator for QA bot evaluation datasets.

A dataset file is JSONL: one JSON object (one ``CanonicalCase``) per line.
"""

from __future__ import annotations

import hashlib
import json
import re
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
CASE_ID_PATTERN = r"^(\d{3})(\d{3})-([A-Za-z][A-Za-z0-9]*)-([a-z0-9]+(?:-[a-z0-9]+)*)$"
_CASE_ID_RE = re.compile(CASE_ID_PATTERN)


def normalize_query(query: str) -> str:
    """Stable normalization for dedup: lowercase, collapse whitespace."""
    return re.sub(r"\s+", " ", query.strip().lower())


def case_hash(scenario: str, query: str) -> str:
    """Content hash of a case, computed from its (normalized) scenario + query.

    This is the *legacy* dedup identity. New cases also persist it as an explicit
    ``dedup_key`` so the question text can later be edited (e.g. neutralising a PR
    link) without changing the case's identity — see ``resolve_dedup_key``.
    """
    return hashlib.sha256(f"{scenario}\x00{normalize_query(query)}".encode("utf-8")).hexdigest()


def resolve_dedup_key(obj: dict[str, Any], scenario: str | None = None) -> str:
    """Return a case's stable dedup identity.

    Prefers the persisted ``dedup_key`` (frozen at curation time from the original
    query) so an edited ``query`` still dedups against the untouched source. Falls
    back to ``case_hash(scenario, query)`` for legacy rows that predate the field.
    """
    key = obj.get("dedup_key")
    if key:
        return str(key)
    return case_hash(scenario or obj.get("scenario", ""), obj.get("query", ""))


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
    "allOf": [
        {
            "if": {"properties": {"reviewed": {"enum": [REVIEW_STATUS_PASS, True]}}},
            "then": {"properties": {"testcase": {"pattern": CASE_ID_PATTERN}}},
        }
    ],
    "additionalProperties": True,
    "required": ["testcase", "query", "ground_truth", "scenario", "reviewed"],
    "properties": {
        "testcase": {"type": "string", "minLength": 1},
        "tags": {"type": "array", "items": {"enum": ["core"]}, "uniqueItems": True},
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
        "dedup_key": {"type": "string"},
    },
}


REQUIRED_STR_FIELDS = ("query", "ground_truth", "scenario")
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
    dedup_key: str = ""
    tags: list[str] = field(default_factory=list)

    def to_dict(self) -> dict[str, Any]:
        result = {
            "testcase": self.testcase,
            "query": self.query,
            "ground_truth": self.ground_truth,
            "expected_references": self.expected_references,
            "expected_knowledges": self.expected_knowledges,
            "scenario": self.scenario,
            "tenant": self.tenant,
            "source": self.source,
            "reviewed": self.reviewed,
            "dedup_key": self.dedup_key or case_hash(self.scenario, self.query),
            "tags": list(self.tags),
        }
        return result

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
            dedup_key=obj.get("dedup_key", "") or "",
            tags=list(obj.get("tags", [])),
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


def validate_case(obj: Any, where: str = "case", *, require_benchmark: bool = False) -> None:
    """Validate a single decoded JSON object against the canonical schema.

    Raises ``ValidationError`` with a descriptive message on the first problem.
    """
    if not isinstance(obj, dict):
        raise ValidationError(f"{where}: expected an object, got {type(obj).__name__}")

    if "testcase" not in obj:
        raise ValidationError(f"{where}: missing required field 'testcase'")
    if require_benchmark and "tags" not in obj:
        raise ValidationError(f"{where}: missing required benchmark field 'tags'")
    testcase = obj["testcase"]
    require_testcase_id = require_benchmark or obj.get("reviewed") in (REVIEW_STATUS_PASS, True)
    if not isinstance(testcase, str) or not testcase.strip():
        raise ValidationError(f"{where}: 'testcase' must be a non-empty string")
    if require_testcase_id and not _CASE_ID_RE.fullmatch(testcase):
        raise ValidationError(f"{where}: 'testcase' must use CCCSSS-category-situation format")
    if "tags" in obj:
        tags = obj["tags"]
        if not isinstance(tags, list) or any(tag != "core" for tag in tags) or len(tags) > 1:
            raise ValidationError(f"{where}: 'tags' must be [] or ['core']")

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

    if "dedup_key" in obj and not isinstance(obj["dedup_key"], str):
        raise ValidationError(f"{where}: 'dedup_key' must be a string")

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


def _validate_rows(
    rows: Iterable[tuple[int, dict[str, Any]]],
    *,
    source: str,
    require_reviewed: bool,
    require_benchmark: bool,
    scenario: str | None = None,
) -> int:
    count = 0
    seen_keys: set[str] = set()
    seen_numbers: set[str] = set()
    categories: dict[str, str] = {}
    category_numbers: dict[str, str] = {}
    for line_no, obj in rows:
        where = f"{source}:{line_no}"
        validate_case(obj, where=where, require_benchmark=require_benchmark)
        if require_reviewed and normalize_review_status(obj.get("reviewed", REVIEW_STATUS_TODO)) != REVIEW_STATUS_PASS:
            raise ValidationError(f"{where}: case is not passed (reviewed != 'pass')")
        if scenario is not None and obj["scenario"] != scenario:
            raise ValidationError(f"{where}: expected scenario {scenario!r}, got {obj['scenario']!r}")
        testcase = obj.get("testcase")
        match = _CASE_ID_RE.fullmatch(testcase) if isinstance(testcase, str) else None
        # Structured IDs distinguish preserved benchmark cases. Staging titles
        # continue to use their stable query-based dedup identity.
        key = testcase if match else resolve_dedup_key(obj)
        if key in seen_keys:
            raise ValidationError(f"{where}: duplicate case identity within file: {key}")
        seen_keys.add(key)
        if match:
            category_number, situation_number, category, _ = match.groups()
            number = category_number + situation_number
            if number in seen_numbers:
                raise ValidationError(f"{where}: duplicate numeric case ID {number}")
            seen_numbers.add(number)
            if categories.setdefault(category_number, category) != category:
                raise ValidationError(f"{where}: inconsistent category for {category_number}")
            if category_numbers.setdefault(category, category_number) != category_number:
                raise ValidationError(f"{where}: inconsistent number for category {category}")
        count += 1
    return count


def validate_records(
    records: list[dict[str, Any]],
    *,
    require_reviewed: bool = True,
    scenario: str | None = None,
) -> int:
    """Validate identified benchmark cases without accessing Azure."""
    return _validate_rows(
        enumerate(records, 1),
        source="cases",
        require_reviewed=require_reviewed,
        require_benchmark=True,
        scenario=scenario,
    )


def select_cases(records: list[dict[str, Any]], scope: str, *, scenario: str) -> list[dict[str, Any]]:
    """Select a deterministic benchmark scope, rejecting invalid or empty input."""
    if scope not in ("core", "full"):
        raise ValidationError(f"Unknown evaluation scope: {scope!r}")
    validate_records(records, scenario=scenario)
    selected = [record for record in records if scope == "full" or "core" in record["tags"]]
    if not selected:
        raise ValidationError(f"No {scope} cases selected for scenario {scenario!r}")
    return selected


def validate_file(path: str | Path, *, require_reviewed: bool = False) -> int:
    """Validate JSONL; canonical case libraries also require IDs and tags."""
    p = Path(path)
    benchmark = p.parent.name == "perf"
    return _validate_rows(
        iter_jsonl(p),
        source=str(p),
        require_reviewed=require_reviewed,
        require_benchmark=benchmark,
        scenario=p.stem if benchmark else None,
    )


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
