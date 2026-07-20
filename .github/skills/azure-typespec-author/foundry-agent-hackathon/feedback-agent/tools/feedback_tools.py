"""Telemetry-collection tools for the TypeSpec Authoring Skill Feedback Agent.

These functions are registered with the agent (via ``agent_framework``) and
implement the concrete, side-effecting actions behind the feedback agent's sole
job: **collect user telemetry** from real ``azure-typespec-author`` sessions and
persist it so the Self-Evolving Agent can later mine it for skill improvements.

Telemetry is written to the project's Application Insights as OpenTelemetry
custom events (log records with a ``session.telemetry`` event name) plus counter
metrics. Both are queryable from Log Analytics (``customEvents`` / ``traces``)
and feed directly into the four-step self-evolution design.

Storage is intentionally the same Application Insights already connected to the
Foundry project — no extra datastore to provision. If Application Insights is not
configured the tools degrade gracefully and still return a structured receipt.
"""

from __future__ import annotations

import json
import logging
import uuid
from datetime import datetime, timezone
from typing import Annotated, Optional

logger = logging.getLogger(__name__)

# Structured logger used as the OpenTelemetry sink. ResponsesHostServer /
# configure_azure_monitor auto-instruments the root logger, so records emitted
# here are exported to Application Insights as `traces` (with our custom
# dimensions attached via `extra`).
_telemetry_logger = logging.getLogger("typespec.skill.telemetry")
_telemetry_logger.setLevel(logging.INFO)

_TELEMETRY_EVENT_NAME = "session.telemetry"

# Optional OpenTelemetry metrics — best effort; import lazily so the tools work
# even if the metrics SDK isn't wired up in a given environment.
try:  # pragma: no cover - depends on runtime deps
    from opentelemetry import metrics as _otel_metrics

    _meter = _otel_metrics.get_meter("typespec-skill-feedback-agent")
    _session_counter = _meter.create_counter(
        name="skill_session_telemetry",
        description="Count of azure-typespec-author session telemetry records collected",
        unit="{record}",
    )
except Exception:  # pragma: no cover
    _session_counter = None


def _normalize_outcome(outcome: str) -> str:
    value = (outcome or "").strip().lower()
    if value in {"success", "succeeded", "pass", "passed", "ok"}:
        return "success"
    if value in {"failure", "failed", "fail", "error"}:
        return "failure"
    if value in {"partial", "mixed"}:
        return "partial"
    return value or "unknown"


def record_session_telemetry(
    user_prompt: Annotated[
        str,
        "The user's original prompt / request from the azure-typespec-author session (anonymized).",
    ],
    outcome: Annotated[
        str,
        "Final session outcome: 'success', 'failure', or 'partial'.",
    ],
    skill_triggered: Annotated[
        bool,
        "Whether the azure-typespec-author skill actually triggered for this prompt.",
    ] = True,
    asked_clarifying_questions: Annotated[
        bool,
        "Whether the skill/agent asked the user clarifying questions before acting.",
    ] = False,
    tool_call_errors: Annotated[
        int,
        "Number of tool-call errors observed during the session.",
    ] = 0,
    retries: Annotated[
        int,
        "Number of retries the user or agent had to make.",
    ] = 0,
    feedback: Annotated[
        Optional[str],
        "Optional free-text feedback the user gave about the experience.",
    ] = None,
    session_id: Annotated[
        Optional[str],
        "Optional upstream session id to correlate with other telemetry.",
    ] = None,
) -> str:
    """Record one anonymized telemetry record from an azure-typespec-author session.

    This is the feedback agent's primary action. Call it once per session the
    user reports on. The record is persisted to Application Insights (custom
    event + counter metric) so the Self-Evolving Agent can cluster failure
    themes and seed new benchmark cases from real usage.

    Returns a JSON receipt with the assigned ``telemetry_id`` and the normalized
    record that was stored.
    """
    record = {
        "telemetry_id": str(uuid.uuid4()),
        "event": _TELEMETRY_EVENT_NAME,
        "timestamp": datetime.now(timezone.utc).isoformat(),
        "session_id": session_id,
        "user_prompt": (user_prompt or "").strip(),
        "outcome": _normalize_outcome(outcome),
        "skill_triggered": bool(skill_triggered),
        "asked_clarifying_questions": bool(asked_clarifying_questions),
        "tool_call_errors": max(0, int(tool_call_errors)),
        "retries": max(0, int(retries)),
        "feedback": (feedback or "").strip() or None,
    }

    # Emit to Application Insights as a structured trace with custom dimensions.
    _telemetry_logger.info(
        "session.telemetry recorded",
        extra={"telemetry": json.dumps(record, ensure_ascii=False), **record},
    )

    if _session_counter is not None:
        try:
            _session_counter.add(
                1,
                {
                    "outcome": record["outcome"],
                    "skill_triggered": record["skill_triggered"],
                },
            )
        except Exception:  # pragma: no cover
            logger.debug("failed to record telemetry counter", exc_info=True)

    return json.dumps({"status": "recorded", **record}, ensure_ascii=False)


def acknowledge_feedback(
    telemetry_id: Annotated[
        str,
        "The telemetry_id returned by record_session_telemetry.",
    ],
) -> str:
    """Return a short, friendly acknowledgement for a recorded telemetry item.

    Use after ``record_session_telemetry`` to confirm to the user that their
    feedback was captured and will be used to improve the skill.
    """
    tid = (telemetry_id or "").strip() or "unknown"
    logger.info("telemetry acknowledged: %s", tid)
    return json.dumps(
        {
            "status": "acknowledged",
            "telemetry_id": tid,
            "message": (
                "Thanks — your feedback was recorded and will help improve the "
                "azure-typespec-author skill."
            ),
        },
        ensure_ascii=False,
    )
