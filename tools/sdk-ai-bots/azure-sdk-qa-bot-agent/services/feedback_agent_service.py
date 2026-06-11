"""Service for running the hosted Feedback Agent jobs.

Object-oriented service: ``FeedbackAgentService`` owns the lifecycle of a
``FeedbackJob`` row in the Cosmos ``feedback-jobs`` container and the
in-process worker that calls the hosted Foundry agent.

Two public entry points:

* ``enqueue(job)`` — persist the job and spawn an in-process
  fire-and-forget task that runs the agent. Push-based; no DB poller.
* ``run_job(job_id, tenant_id)`` — read the persisted row, flip status
  to ``running``, call the Foundry Responses API on the hosted feedback
  agent, log the raw reply for triage, and write back a terminal
  ``done`` (or ``skipped`` on invocation failure) status.

The hosted agent does the entire analysis end-to-end through its own
tools (KB lookup, conversation/trace fetch, GitHub issue creation, ...).
The service intentionally **does not parse** the agent reply — there is
no structured output contract. Pydantic models are still used for the
inbound payload (``FeedbackAgentInput``) and the Foundry
``agent_reference`` (``FoundryAgentReference``).

The only ``dict[str, Any]`` boundaries are the Cosmos SDK and the
OpenAI Responses request payload — both are wrapped at the edge.

See ``docs/feedback-agent-design.md`` §3.1 & §5.3.
"""

from __future__ import annotations

import asyncio
import logging
from datetime import datetime, timezone

from azure.ai.projects.aio import AIProjectClient

from config.app_config import get as cfg
from models.feedback import (
    FeedbackAgentInput,
    FeedbackJob,
    FeedbackJobStatus,
    FoundryAgentReference,
    UserFeedbackInput,
)
from utils.azure_ai_foundry import get_project_client
from utils.azure_cosmosdb import (
    feedback_job_exists_for_response,
    read_feedback_job,
    upsert_feedback_job,
)
from utils.background_tasks import BackgroundTaskTracker

logger = logging.getLogger(__name__)


# Hard cap on agent wall-clock per job (defensive — the agent itself has
# per-turn caps in instructions).
_JOB_TIMEOUT_SECS = 600

# Cap the agent reply we echo into the log line so a chatty agent
# can't blow up log storage. The full reply is still emitted at DEBUG.
_AGENT_REPLY_LOG_PREVIEW_CHARS = 4000


class FeedbackAgentService:
    """Owns enqueue + run lifecycle for hosted feedback-agent jobs."""

    _FEEDBACK_AGENT_NAME_KEY = "AI_FOUNDRY_FEEDBACK_AGENT_NAME"
    _FEEDBACK_AGENT_VERSION_KEY = "AI_FOUNDRY_FEEDBACK_AGENT_VERSION"
    _DEFAULT_FEEDBACK_AGENT_NAME = "azure-sdk-feedback-agent"

    def __init__(
        self,
        *,
        project_client: AIProjectClient | None = None,
        task_tracker: BackgroundTaskTracker | None = None,
    ) -> None:
        self._project_client = project_client
        self._task_tracker = task_tracker or BackgroundTaskTracker.instance()

    # -- Configuration helpers --------------------------------------------

    def _agent_name(self) -> str:
        return cfg(self._FEEDBACK_AGENT_NAME_KEY, self._DEFAULT_FEEDBACK_AGENT_NAME)

    def _agent_version(self) -> str | None:
        v = cfg(self._FEEDBACK_AGENT_VERSION_KEY, "") or None
        return v or None

    def _get_project_client(self) -> AIProjectClient:
        if self._project_client is None:
            self._project_client = get_project_client()
        return self._project_client

    # -- Public: enqueue ---------------------------------------------------

    async def enqueue(self, job: FeedbackJob) -> bool:
        """Persist ``job`` and spawn the background worker.

        Dedup: if a job already exists for ``(tenant_id, response_id,
        trigger)`` the new job is dropped (returns ``False``). The Cosmos
        row id itself is timestamped so retries within the same trigger
        do not collide on insertion.

        Returns ``True`` when a new background analysis was scheduled.
        """
        if await self._already_enqueued(job):
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
            self._run_job_safe(job.id, job.tenant_id),
            name=f"feedback-agent:{job.id}",
        )
        self._task_tracker.track(task)
        logger.info(
            "Enqueued feedback-job %s (trigger=%s, tenant=%s)",
            job.id,
            job.trigger.value,
            job.tenant_id,
        )
        return True

    async def _already_enqueued(self, job: FeedbackJob) -> bool:
        try:
            return await feedback_job_exists_for_response(
                tenant_id=job.tenant_id,
                response_id=job.response_id,
                trigger=job.trigger.value,
            )
        except Exception:
            logger.exception(
                "Dedup check failed for response_id=%s; proceeding with enqueue",
                job.response_id,
            )
            return False

    # -- Public: run -------------------------------------------------------

    async def run_job(self, job_id: str, tenant_id: str) -> None:
        """Execute a single persisted feedback-job."""
        job = await self._load_job(job_id, tenant_id)
        if job is None:
            return

        # Idempotency: only run queued jobs.
        if job.status != FeedbackJobStatus.queued:
            logger.info(
                "Feedback job %s already in status=%s; skipping run",
                job.id,
                job.status.value,
            )
            return

        await self._transition(job, FeedbackJobStatus.running)

        payload = self._build_input(job)
        try:
            agent_text = await self._invoke_agent(payload)
        except Exception as exc:
            logger.exception("Feedback agent invocation failed for job %s", job.id)
            self._finalize_skipped(job, error=f"agent_invocation_failed: {exc}")
            await upsert_feedback_job(job.to_cosmos())
            return

        # Log the agent's reply verbatim — the agent owns the analysis
        # and any side effects (issue creation, etc.). The service only
        # records that the run completed.
        preview = (agent_text or "").strip()
        if len(preview) > _AGENT_REPLY_LOG_PREVIEW_CHARS:
            preview = preview[:_AGENT_REPLY_LOG_PREVIEW_CHARS] + " …[truncated]"
        logger.info("Feedback job %s agent reply (preview):\n%s", job.id, preview)
        if agent_text:
            logger.debug("Feedback job %s agent reply (full):\n%s", job.id, agent_text)

        self._finalize_done(job)
        await upsert_feedback_job(job.to_cosmos())
        logger.info("Feedback job %s completed status=%s", job.id, job.status.value)

    async def _run_job_safe(self, job_id: str, tenant_id: str) -> None:
        try:
            await asyncio.wait_for(
                self.run_job(job_id, tenant_id), timeout=_JOB_TIMEOUT_SECS
            )
        except asyncio.TimeoutError:
            logger.error(
                "Feedback job %s timed out after %ds", job_id, _JOB_TIMEOUT_SECS
            )
            await self._mark_skipped(job_id, tenant_id, error="timeout")
        except Exception:
            logger.exception("Feedback job %s crashed", job_id)
            await self._mark_skipped(job_id, tenant_id, error="worker_crash")

    # -- Cosmos helpers ----------------------------------------------------

    async def _load_job(self, job_id: str, tenant_id: str) -> FeedbackJob | None:
        doc = await read_feedback_job(job_id=job_id, tenant_id=tenant_id)
        if doc is None:
            logger.warning("Feedback job %s not found in tenant %s", job_id, tenant_id)
            return None
        return FeedbackJob.from_cosmos(doc)

    async def _transition(self, job: FeedbackJob, status: FeedbackJobStatus) -> None:
        job.status = status
        job.updated_at = _now()
        await upsert_feedback_job(job.to_cosmos())

    async def _mark_skipped(self, job_id: str, tenant_id: str, *, error: str) -> None:
        job = await self._load_job(job_id, tenant_id)
        if job is None:
            return
        self._finalize_skipped(job, error=error)
        try:
            await upsert_feedback_job(job.to_cosmos())
        except Exception:
            logger.exception("Failed to mark feedback job %s skipped", job.id)

    # -- Status finalization (owned by the service) ----------------------

    def _finalize_done(self, job: FeedbackJob) -> None:
        job.status = FeedbackJobStatus.done
        job.error = None
        job.updated_at = _now()

    def _finalize_skipped(self, job: FeedbackJob, *, error: str) -> None:
        job.status = FeedbackJobStatus.skipped
        job.error = error
        job.updated_at = _now()

    # -- Foundry invocation -----------------------------------------------

    def _build_input(self, job: FeedbackJob) -> FeedbackAgentInput:
        user_feedback: UserFeedbackInput | None = None
        if job.comment or job.reasons:
            user_feedback = UserFeedbackInput(
                comment=job.comment, reasons=list(job.reasons)
            )
        return FeedbackAgentInput(
            trigger=job.trigger,
            tenant_id=job.tenant_id,
            conversation_id=job.conversation_id,
            conversation_type=job.conversation_type,
            response_id=job.response_id,
            user_feedback=user_feedback,
        )

    async def _resolve_agent_reference(self) -> FoundryAgentReference:
        project_client = self._get_project_client()
        agent_name = self._agent_name()
        agent_version = self._agent_version()
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
        return FoundryAgentReference(name=agent.name, version=agent.version)

    async def _invoke_agent(self, payload: FeedbackAgentInput) -> str:
        """Call the hosted Feedback Agent and return raw output text."""
        project_client = self._get_project_client()
        openai_client = project_client.get_openai_client(agent_name=self._agent_name())
        agent_ref = await self._resolve_agent_reference()

        # OpenAI Responses request shape is contract-bound to the SDK and
        # accepts a dict here; everything *inside* the message is built
        # from the typed `FeedbackAgentInput` payload.
        input_items = [
            {
                "type": "message",
                "role": "user",
                "content": [{"type": "input_text", "text": payload.to_json()}],
            }
        ]

        raw_response = await openai_client.responses.with_raw_response.create(
            input=input_items,
            store=True,
            stream=False,
            extra_body={"agent_reference": agent_ref.to_extra_body()},
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
# Module-level helpers
# ---------------------------------------------------------------------------


def _now() -> datetime:
    return datetime.now(timezone.utc)


__all__ = ["FeedbackAgentService"]
