"""Local assessment artifacts, kept separate from Foundry grading inputs."""

from __future__ import annotations

import copy
import html
import json
import logging
import math
import os
import re
import sys
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path
from typing import Any
from uuid import uuid4

logger = logging.getLogger(__name__)
_ASPECTS = ("support", "fit", "source_quality", "consistency", "coverage")


def _cell(value: Any) -> str:
    text = html.escape(str(value), quote=True)
    return re.sub(r"([\\`*_{}\[\]()#+.!|>~-])", r"\\\1", text).replace("\r", "").replace("\n", "<br>")


def _block(value: str) -> str:
    longest = max((len(match) for match in re.findall(r"`+", value)), default=0)
    fence = "`" * max(3, longest + 1)
    return f"{fence}text\n{value}\n{fence}\n"


def _assessment(response: dict[str, Any]) -> tuple[str, dict[str, Any] | None, str | None]:
    if response.get("id") == "content-filter":
        return "synthetic", None, "Content filtering produced a synthetic response, not a model assessment."
    if "assessment" not in response:
        return "legacy", None, "The backend did not return assessment metadata."
    raw = response["assessment"]
    if raw is None:
        return "absent", None, "The backend returned null assessment metadata."
    if not isinstance(raw, dict):
        return "malformed", None, "Assessment metadata must be an object."

    # The shared model imports only typing and Pydantic, not backend configuration.
    agent_dir = str(Path(__file__).resolve().parent.parent / "azure-sdk-qa-bot-agent")
    if agent_dir not in sys.path:
        sys.path.insert(0, agent_dir)
    from pydantic import ValidationError
    from models.evidence_assessment import (
        CURRENT_RUBRIC_VERSION,
        CURRENT_SCHEMA_VERSION,
        EvidenceAssessment,
    )
    from services.evidence_assessment_service import provisional_band

    if (
        raw.get("schema_version") not in (None, CURRENT_SCHEMA_VERSION)
        or raw.get("rubric_version") not in (None, CURRENT_RUBRIC_VERSION)
    ):
        return "unsupported_version", None, "The assessment schema or rubric version is not supported."
    try:
        validated = EvidenceAssessment.model_validate(raw)
    except ValidationError:
        # Validation errors can contain arbitrary input and must not enter artifacts.
        return "malformed", None, "Assessment metadata failed strict backend schema validation."
    if validated.provisional_band != provisional_band(validated.aspects):
        return "malformed", None, "The reported band does not match the applicable aspect values."
    return validated.status, validated.model_dump(mode="json"), None


class AssessmentReport:
    """Checkpoint an ordered input ledger and render safe, complete-answer reports."""

    def __init__(self, cache_dir: Path, *, dataset: str, run_context: str) -> None:
        self._cache_dir = cache_dir
        self._invocation = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S") + "-" + uuid4().hex
        self.directory = cache_dir / "assessment" / "unresolved" / self._invocation
        revision = os.environ.get("BUILD_SOURCEVERSION", "")
        self.data: dict[str, Any] = {
            "report_format_version": 1,
            "invocation_id": self._invocation,
            "dataset": dataset,
            "scenario": "unresolved",
            "run_context": run_context,
            "source_revision": revision if re.fullmatch(r"[a-fA-F0-9]{7,64}", revision) else None,
            "evaluation_id": None,
            "evaluation_run_id": None,
            "started_at": datetime.now(timezone.utc).isoformat(),
            "finished_at": None,
            "phase": "startup",
            "outcome": "running",
            "verification_result": None,
            "cases": [],
        }
        self.checkpoint()

    @staticmethod
    def case_id(index: int) -> str:
        return f"case-{index + 1:06d}"

    def bind_inputs(self, records: list[dict[str, Any]], scenario: str) -> None:
        component = re.sub(r"[^A-Za-z0-9_-]", "_", scenario)[:100] or "unresolved"
        target = self._cache_dir / "assessment" / component / self._invocation
        if target != self.directory:
            target.parent.mkdir(parents=True, exist_ok=True)
            self.directory.rename(target)
            self.directory = target
        self.data["scenario"] = scenario
        self.data["cases"] = [
            {
                "case_id": self.case_id(index),
                "testcase": record.get("testcase", "unknown"),
                "question": record["query"],
                "answer": "",
                "response_id": "",
                "state": "pending",
                "assessment": None,
                "diagnostic": None,
                "retrieval_status": "pending",
                "grading_status": "pending",
                "evaluation": {},
            }
            for index, record in enumerate(records)
        ]
        self.checkpoint()

    def phase(self, phase: str) -> None:
        self.data["phase"] = phase
        self.checkpoint()

    def evaluation(self, eval_id: str, run_id: str | None = None) -> None:
        self.data["evaluation_id"] = eval_id
        self.data["evaluation_run_id"] = run_id
        self.checkpoint()

    def verification(self, result: str) -> None:
        self.data["verification_result"] = result
        self.checkpoint()

    def collected(self, index: int, response: dict[str, Any]) -> None:
        case = self.data["cases"][index]
        state, assessment, diagnostic = _assessment(response)
        case.update(
            answer=response.get("answer", ""),
            response_id=response.get("id", "") or "",
            state=state,
            assessment=assessment,
            diagnostic=diagnostic,
        )
        if state not in ("valid", "abstained"):
            logger.warning("Assessment unavailable: case_id=%s, state=%s", case["case_id"], state)
        self.checkpoint()

    def collection_failed(self, index: int) -> None:
        self.data["cases"][index].update(
            state="collection_failed", diagnostic="The completion request failed; assessment was not recovered.",
            retrieval_status="not_applicable",
        )
        self.checkpoint()

    def retrieval(self, case_id: str, status: str) -> None:
        self._case(case_id)["retrieval_status"] = status
        self.checkpoint()

    def _case(self, case_id: str) -> dict[str, Any]:
        cases: list[dict[str, Any]] = self.data["cases"]
        for case in cases:
            if case["case_id"] == case_id:
                return case
        raise ValueError("Evaluation output has an unknown assessment case ID.")

    def attach_row(self, case_id: str, row: dict[str, Any]) -> None:
        case = self._case(case_id)
        if (
            row["inputs.query"] != case["question"]
            or row["inputs.execution"]["response_id"] != case["response_id"]
            or row["inputs.response"] != case["answer"]
        ):
            raise ValueError("Evaluation output does not match its assessment case.")
        if case["grading_status"] == "completed":
            raise ValueError("Evaluation output repeats an assessment case ID.")
        case["evaluation"] = {
            key.removeprefix("outputs."): (
                None if isinstance(value, float) and not math.isfinite(value) else value
            )
            for key, value in row.items() if key.startswith("outputs.")
        }
        if any(
            isinstance(value, float) and not math.isfinite(value)
            for key, value in row.items() if key.startswith("outputs.")
        ):
            logger.warning("Non-finite evaluator score omitted from report: case_id=%s", case_id)
        case["grading_status"] = "completed"
        row["inputs.execution"]["assessment_report"] = copy.deepcopy({
            key: case[key] for key in ("case_id", "state", "assessment", "diagnostic", "retrieval_status")
        })
        self.checkpoint()

    def finish(self, exit_code: int) -> None:
        self.data["outcome"] = "passed" if exit_code == 0 else "failed"
        self.data["finished_at"] = datetime.now(timezone.utc).isoformat()
        for case in self.data["cases"]:
            if case["state"] == "pending":
                case.update(state="collection_failed", diagnostic="Collection did not complete.")
            if case["grading_status"] == "pending":
                case["grading_status"] = "unavailable"
        self.checkpoint()

    def checkpoint(self) -> None:
        self.data["counts"] = dict(Counter(case["state"] for case in self.data["cases"]))
        self.data["retrieval_counts"] = dict(Counter(case["retrieval_status"] for case in self.data["cases"]))
        self.data["grading_counts"] = dict(Counter(case["grading_status"] for case in self.data["cases"]))
        self.directory.mkdir(parents=True, exist_ok=True)
        for name, content in (
            ("results.json", json.dumps(self.data, ensure_ascii=False, indent=2, allow_nan=False)),
            ("report.md", self.markdown()),
        ):
            path = self.directory / name
            temporary = path.with_suffix(path.suffix + ".tmp")
            temporary.write_text(content, encoding="utf-8")
            temporary.replace(path)

    def markdown(self) -> str:
        lines = [
            "# Answering-model assessment report\n",
            "These are answering-model evidence assessments, not correctness probabilities or external evaluation grades.\n",
            "| Run field | Value |", "| --- | --- |",
        ]
        for field in (
            "invocation_id", "dataset", "scenario", "run_context", "source_revision",
            "evaluation_id", "evaluation_run_id", "started_at", "finished_at", "phase", "outcome", "verification_result",
        ):
            lines.append(f"| {_cell(field)} | {_cell(self.data[field] or 'Unavailable')} |")
        for heading, field in (
            ("Assessment availability", "counts"),
            ("Existing tool-call retrieval", "retrieval_counts"),
            ("External grading availability", "grading_counts"),
        ):
            lines.extend((f"\n## {heading}\n", "| State | Cases |", "| --- | --- |"))
            for state, count in sorted(self.data[field].items()):
                lines.append(f"| {_cell(state)} | {count} |")
        for case in self.data["cases"]:
            lines.extend((
                f"\n## {case['case_id']}\n",
                "**Test case**\n", _block(str(case["testcase"])),
                "**Question**\n", _block(case["question"]),
                "**Complete backend answer**\n", _block(case["answer"]),
                "| Metadata | Value |", "| --- | --- |",
            ))
            assessment = case["assessment"]
            for key in ("state", "response_id", "retrieval_status", "grading_status", "diagnostic"):
                lines.append(f"| {_cell(key)} | {_cell(case[key] or 'Unavailable')} |")
            for key in ("schema_version", "rubric_version", "status", "provisional_band"):
                value = assessment.get(key) if assessment else None
                lines.append(f"| {_cell(key)} | {_cell(value if value is not None else 'Unavailable')} |")
            lines.extend(("\n### Evidence aspects\n", "| Aspect | Value | Reason |", "| --- | --- | --- |"))
            aspects = assessment.get("aspects") if assessment else None
            for aspect in _ASPECTS:
                result = aspects.get(aspect) if aspects else None
                value = result["value"] if result else "Unavailable"
                reason = result["reason"] if result else "No aspect assessment was available."
                lines.append(f"| {_cell(aspect)} | {_cell(value)} | {_cell(reason)} |")
            lines.extend((
                "\n### Existing external evaluator results\n",
                "These scores and pass/fail results are independent of the answering model's assessment.\n",
                "| Metric | Result |", "| --- | --- |",
            ))
            if case["evaluation"]:
                for key, value in case["evaluation"].items():
                    lines.append(f"| {_cell(key)} | {_cell(value if value is not None else 'Unavailable')} |")
            else:
                lines.append("| Unavailable | Grading did not produce results for this case. |")
        return "\n".join(lines) + "\n"
