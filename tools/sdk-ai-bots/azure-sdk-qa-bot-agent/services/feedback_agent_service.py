"""Service for running the hosted Feedback Agent jobs.

Two entry points:

* ``enqueue_feedback_analysis(job)`` — persist the job in Cosmos
  (``feedback-jobs``) and spawn an in-process fire-and-forget task that
  runs the agent. Used by the ``server.py`` `/agent/feedback` and the
  ``ConversationService`` expert-reply paths. Push-based — there is no
  DB poller.

* ``run_job(job_id, tenant_id)`` — read the persisted row, flip status
  to ``running``, call the Foundry Responses API on the hosted
  feedback agent, parse the structured result, write back classification
  / issue_url / summaries and a final ``done`` (or ``skipped``) status.

See ``docs/feedback-agent-design.md`` §3.1 & §5.3.
"""

from __future__ import annotations

import asyncio
import json
import logging
import re
from datetime import datetime, timezone
from typing import Any

from azure.ai.projects.aio import AIProjectClient

from config.app_config import get as cfg
from models.feedback import (
    FeedbackClassification,
    FeedbackJob,
    FeedbackJobStatus,
)
from utils.azure_ai_foundry import get_project_client
from utils.azure_cosmosdb import (
    feedback_job_exists_for_response,
    read_feedback_job,
    upsert_feedback_job,
)
from utils.background_tasks import BackgroundTaskTracker

logger = logging.getLogger(__name__)


_FEEDBACK_AGENT_NAME_KEY = "AI_FOUNDRY_FEEDBACK_AGENT_NAME"
_FEEDBACK_AGENT_VERSION_KEY = "AI_FOUNDRY_FEEDBACK_AGENT_VERSION"
_DEFAULT_FEEDBACK_AGENT_NAME = "azure-sdk-feedback-agent"

# Hard cap on agent wall-clock per job (defensive — the agent has its own
# per-turn caps in instructions).
_JOB_TIMEOUT_SECS = 600


def _now() -> datetime:
    return datetime.now(timezone.utc)


# ---------------------------------------------------------------------------
# Enqueue
# ---------------------------------------------------------------------------


async def enqueue_feedback_analysis(job: FeedbackJob) -> bool:
    """Persist ``job`` and spawn the background worker.

    Dedup: if a job already exists for ``(tenant_id, response_id, trigger)``
    the new job is dropped (returns ``False``). The Cosmos row itself uses a
    timestamped id so retries don't collide.

    Returns ``True`` when a new background analysis was scheduled.
    """
    try:
        already = await feedback_job_exists_for_response(
            tenant_id=job.tenant_id,
            response_id=job.response_id,
            trigger=job.trigger.value,
        )
    except Exception:
        logger.exception(
            "Dedup check failed for response_id=%s; proceeding with enqueue",
            job.response_id,
        )
        already = False

    if already:
        logger.info(
            "Skipping feedback enqueue — job already exists for "
            "response_id=%s trigger=%s",
            job.response_id,
            job.trigger.value,
        )
        return False

    try:
        await upsert_feedback_job(job.to_cosmos())
    except Exception:
        logger.exception(
            "Failed to persist feedback-job %s; skipping background analysis",
            job.id,
        )
        return False

    task = asyncio.create_task(
        _run_job_safe(job.id, job.tenant_id),
        name=f"feedback-agent:{job.id}",
    )
    BackgroundTaskTracker.instance().track(task)
    logger.info(
        "Enqueued feedback-job %s (trigger=%s, tenant=%s)",
        job.id,
        job.trigger.value,
        job.tenant_id,
    )
    return True


async def _run_job_safe(job_id: str, tenant_id: str) -> None:
    try:
        await asyncio.wait_for(run_job(job_id, tenant_id), timeout=_JOB_TIMEOUT_SECS)
    except asyncio.TimeoutError:
        logger.error("Feedback job %s timed out after %ds", job_id, _JOB_TIMEOUT_SECS)
        await _mark_skipped(job_id, tenant_id, error="timeout")
    except Exception:
        logger.exception("Feedback job %s crashed", job_id)
        await _mark_skipped(job_id, tenant_id, error="worker_crash")


# ---------------------------------------------------------------------------
# Run
# ---------------------------------------------------------------------------


async def run_job(job_id: str, tenant_id: str) -> None:
    """Execute a single persisted feedback-job."""
    doc = await read_feedback_job(job_id=job_id, tenant_id=tenant_id)
    if doc is None:
        logger.warning("Feedback job %s not found in tenant %s", job_id, tenant_id)
        return

    job = FeedbackJob.from_cosmos(doc)

    # Idempotency: only run queued jobs.
    if job.status != FeedbackJobStatus.queued:
        logger.info(
            "Feedback job %s already in status=%s; skipping run",
            job.id,
            job.status.value,
        )
        return

    job.status = FeedbackJobStatus.running
    job.updated_at = _now()
    await upsert_feedback_job(job.to_cosmos())

    payload = _build_agent_payload(job)
    try:
        agent_text = await _invoke_feedback_agent(payload)
    except Exception as exc:
        logger.exception("Feedback agent invocation failed for job %s", job.id)
        job.status = FeedbackJobStatus.skipped
        job.error = f"agent_invocation_failed: {exc}"
        job.updated_at = _now()
        await upsert_feedback_job(job.to_cosmos())
        return

    parsed = _parse_agent_output(agent_text)
    _apply_parsed_result(job, parsed)
    job.updated_at = _now()
    await upsert_feedback_job(job.to_cosmos())
    logger.info(
        "Feedback job %s completed status=%s classification=%s",
        job.id,
        job.status.value,
        job.classification.value if job.classification else None,
    )


def _build_agent_payload(job: FeedbackJob) -> dict[str, Any]:
    user_feedback: dict[str, Any] | None = None
    if job.comment or job.reasons:
        user_feedback = {"comment": job.comment, "reasons": job.reasons}
    return {
        "trigger": job.trigger.value,
        "tenant_id": job.tenant_id,
        "conversation_id": job.conversation_id,
        "conversation_type": job.conversation_type.value,
        "response_id": job.response_id,
        "user_feedback": user_feedback,
    }


# ---------------------------------------------------------------------------
# Foundry Responses API invocation
# ---------------------------------------------------------------------------


def _feedback_agent_name() -> str:
    return cfg(_FEEDBACK_AGENT_NAME_KEY, _DEFAULT_FEEDBACK_AGENT_NAME)


def _feedback_agent_version() -> str | None:
    v = cfg(_FEEDBACK_AGENT_VERSION_KEY, "") or None
    return v or None


async def _resolve_agent_reference(project_client: AIProjectClient) -> dict[str, Any]:
    agent_name = _feedback_agent_name()
    agent_version = _feedback_agent_version()
    if agent_version:
        agent = await project_client.agents.get_version(agent_name, agent_version)
    else:
        details = await project_client.agents.get(agent_name)
        agent = details.versions.latest if details else None
    if agent is None:
        raise RuntimeError(
            f"Feedback agent '{agent_name}' (version={agent_version or 'latest'}) "
            "not found in AI Foundry."
        )
    return {
        "name": agent.name,
        "version": agent.version,
        "type": "agent_reference",
    }


async def _invoke_feedback_agent(payload: dict[str, Any]) -> str:
    """Call the hosted Feedback Agent and return raw output text."""
    project_client = get_project_client()
    openai_client = project_client.get_openai_client(agent_name=_feedback_agent_name())
    agent_ref = await _resolve_agent_reference(project_client)

    input_items = [
        {
            "type": "message",
            "role": "user",
            "content": [
                {
                    "type": "input_text",
                    "text": json.dumps(payload, ensure_ascii=False),
                }
            ],
        }
    ]

    raw_response = await openai_client.responses.with_raw_response.create(
        input=input_items,
        store=True,
        stream=False,
        extra_body={"agent_reference": agent_ref},
    )
    response = raw_response.parse()

    if response.status != "completed":
        logger.warning(
            "Feedback agent response not completed: id=%s status=%s error=%s",
            response.id,
            response.status,
            response.error,
        )
    return response.output_text or ""


# ---------------------------------------------------------------------------
# Output parsing
# ---------------------------------------------------------------------------


# The instruction tells the agent to emit a JSON object as the final block
# (optionally fenced). We try a fenced ```json block first, then fall back
# to the last balanced { ... } in the text.
_FENCED_JSON_RE = re.compile(
    r"```(?:json)?\s*(\{.*?\})\s*```", re.DOTALL | re.IGNORECASE
)


def _parse_agent_output(text: str) -> dict[str, Any]:
    if not text:
        return {}

    candidates: list[str] = []
    candidates.extend(_FENCED_JSON_RE.findall(text))
    # Heuristic: last balanced top-level JSON object.
    last_obj = _extract_last_json_object(text)
    if last_obj:
        candidates.append(last_obj)

    for blob in reversed(candidates):  # prefer the last one emitted
        try:
            obj = json.loads(blob)
            if isinstance(obj, dict):
                return obj
        except json.JSONDecodeError:
            continue
    logger.warning("Feedback agent output did not contain parseable JSON")
    return {}


def _extract_last_json_object(text: str) -> str | None:
    end = text.rfind("}")
    if end == -1:
        return None
    depth = 0
    for i in range(end, -1, -1):
        ch = text[i]
        if ch == "}":
            depth += 1
        elif ch == "{":
            depth -= 1
            if depth == 0:
                return text[i : end + 1]
    return None


def _apply_parsed_result(job: FeedbackJob, parsed: dict[str, Any]) -> None:
    """Populate the job from the agent's JSON output. Mutates ``job``."""
    classification_raw = parsed.get("classification")
    if classification_raw:
        try:
            job.classification = FeedbackClassification(classification_raw)
        except ValueError:
            logger.warning(
                "Feedback job %s: unknown classification %r",
                job.id,
                classification_raw,
            )

    def _coerce_str(v: Any) -> str | None:
        if v is None:
            return None
        if isinstance(v, str):
            return v.strip() or None
        return str(v)

    job.user_intent_summary = _coerce_str(parsed.get("user_intent_summary"))
    job.suggested_fix_summary = _coerce_str(parsed.get("suggested_fix_summary"))
    job.corrected_answer = _coerce_str(parsed.get("corrected_answer"))
    job.issue_url = _coerce_str(parsed.get("issue_url"))

    status_raw = parsed.get("status")
    if status_raw:
        try:
            job.status = FeedbackJobStatus(status_raw)
        except ValueError:
            job.status = FeedbackJobStatus.done
    else:
        job.status = FeedbackJobStatus.done

    err = _coerce_str(parsed.get("error"))
    if err:
        job.error = err
        # An explicit error from the agent (e.g. trace_unavailable) → skipped.
        if job.status == FeedbackJobStatus.done:
            job.status = FeedbackJobStatus.skipped


async def _mark_skipped(job_id: str, tenant_id: str, *, error: str) -> None:
    doc = await read_feedback_job(job_id=job_id, tenant_id=tenant_id)
    if doc is None:
        return
    job = FeedbackJob.from_cosmos(doc)
    job.status = FeedbackJobStatus.skipped
    job.error = error
    job.updated_at = _now()
    try:
        await upsert_feedback_job(job.to_cosmos())
    except Exception:
        logger.exception("Failed to mark feedback job %s skipped", job.id)
