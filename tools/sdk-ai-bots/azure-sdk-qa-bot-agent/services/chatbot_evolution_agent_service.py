"""Service for running the hosted Chatbot Evolution Agent jobs.

Object-oriented service: ``ChatbotEvolutionAgentService`` owns the feedback
lifecycle embedded in a ``QARecord`` row in the Cosmos ``qa-records``
container and the worker that calls the hosted Foundry agent.

One public entry point:

* ``run_job(record_id, tenant_id)`` — read the persisted QA record, flip
  its feedback status to ``running``, call the Foundry Responses API on the
  hosted chatbot evolution agent, log the raw reply for triage, and write
  back a terminal ``done`` (or ``failed`` on invocation failure) status.

The hosted agent does the entire analysis end-to-end through its own
tools (KB lookup, conversation/trace fetch, GitHub issue creation, ...).
The service intentionally **does not parse** the agent reply — there is
no structured output contract. Pydantic models are still used for the
inbound payload (``ChatbotEvolutionAgentInput``) and the Foundry
``agent_reference`` (``FoundryAgentReference``).

The only ``dict[str, Any]`` boundaries are the Cosmos SDK and the
OpenAI Responses request payload — both are wrapped at the edge.

See ``docs/chatbot-evolution-agent-design.md``.
"""

from __future__ import annotations

import asyncio
import logging
from datetime import datetime, timezone

from azure.ai.projects.aio import AIProjectClient

from config.app_config import get as cfg
from models.feedback import (
    ChatbotEvolutionAgentInput,
    FoundryAgentReference,
)
from models.qa_record import FeedbackState, FeedbackStatus, QARecord
from utils.azure_ai_foundry import get_project_client
from utils.azure_cosmosdb import (
    read_qa_record,
    upsert_qa_record,
)

logger = logging.getLogger(__name__)


# Hard cap on agent wall-clock per job (defensive — the agent itself has
# per-turn caps in instructions).
_JOB_TIMEOUT_SECS = 600

# Cap the agent reply we echo into the log line so a chatty agent
# can't blow up log storage. The full reply is still emitted at DEBUG.
_AGENT_REPLY_LOG_PREVIEW_CHARS = 4000


class ChatbotEvolutionAgentService:
    """Owns the run lifecycle for hosted chatbot-evolution-agent jobs."""

    _AGENT_NAME_KEY = "AI_FOUNDRY_CHATBOT_EVOLUTION_AGENT_NAME"
    _AGENT_VERSION_KEY = "AI_FOUNDRY_CHATBOT_EVOLUTION_AGENT_VERSION"
    _DEFAULT_AGENT_NAME = "azure-sdk-chatbot-evolution-agent"

    def __init__(
        self,
        *,
        project_client: AIProjectClient | None = None,
    ) -> None:
        self._project_client = project_client

    # -- Configuration helpers --------------------------------------------

    def _agent_name(self) -> str:
        return cfg(self._AGENT_NAME_KEY, self._DEFAULT_AGENT_NAME)

    def _agent_version(self) -> str | None:
        v = cfg(self._AGENT_VERSION_KEY, "") or None
        return v or None

    def _get_project_client(self) -> AIProjectClient:
        if self._project_client is None:
            self._project_client = get_project_client()
        return self._project_client

    # -- Public: run -------------------------------------------------------

    async def run_job(self, record_id: str, tenant_id: str) -> None:
        """Execute the feedback analysis for a single persisted QA record."""
        record = await self._load_job(record_id, tenant_id)
        if record is None:
            return

        # Idempotency: only run when feedback hasn't already started.
        if record.feedback is not None and record.feedback.status in (
            FeedbackStatus.running,
            FeedbackStatus.done,
        ):
            logger.info(
                "Feedback for QA record %s already in status=%s; skipping run",
                record.id,
                record.feedback.status.value,
            )
            return

        await self._transition(record, FeedbackStatus.running)

        payload = self._build_input(record)
        try:
            agent_text = await asyncio.wait_for(
                self._invoke_agent(payload), timeout=_JOB_TIMEOUT_SECS
            )
        except Exception as exc:
            logger.exception(
                "Chatbot evolution agent invocation failed for job %s", record.id
            )
            self._finalize_failed(record, error=f"agent_invocation_failed: {exc}")
            await upsert_qa_record(record.to_cosmos())
            return

        # Log the agent's reply verbatim — the agent owns the analysis
        # and any side effects (issue creation, etc.). The service only
        # records that the run completed.
        preview = (agent_text or "").strip()
        if len(preview) > _AGENT_REPLY_LOG_PREVIEW_CHARS:
            preview = preview[:_AGENT_REPLY_LOG_PREVIEW_CHARS] + " …[truncated]"
        logger.info("Feedback job %s agent reply (preview):\n%s", record.id, preview)
        if agent_text:
            logger.debug("Feedback job %s agent reply (full):\n%s", record.id, agent_text)

        self._finalize_done(record)
        await upsert_qa_record(record.to_cosmos())
        logger.info(
            "Feedback job %s completed status=%s",
            record.id,
            record.feedback.status.value,
        )

    # -- Cosmos helpers ----------------------------------------------------

    async def _load_job(self, record_id: str, tenant_id: str) -> QARecord | None:
        doc = await read_qa_record(record_id=record_id, tenant_id=tenant_id)
        if doc is None:
            logger.warning(
                "Feedback job %s not found in tenant %s", record_id, tenant_id
            )
            return None
        return QARecord.from_cosmos(doc)

    async def _transition(self, record: QARecord, status: FeedbackStatus) -> None:
        if record.feedback is None:
            record.feedback = FeedbackState(created_at=_now())
        record.feedback.status = status
        record.feedback.updated_at = _now()
        record.updated_at = _now()
        await upsert_qa_record(record.to_cosmos())

    # -- Status finalization (owned by the service) ----------------------

    def _finalize_done(self, record: QARecord) -> None:
        assert record.feedback is not None
        record.feedback.status = FeedbackStatus.done
        record.feedback.error = None
        record.feedback.updated_at = _now()
        record.updated_at = _now()

    def _finalize_failed(self, record: QARecord, *, error: str) -> None:
        if record.feedback is None:
            record.feedback = FeedbackState(created_at=_now())
        record.feedback.status = FeedbackStatus.failed
        record.feedback.error = error
        record.feedback.updated_at = _now()
        record.updated_at = _now()

    # -- Foundry invocation -----------------------------------------------

    def _build_input(self, record: QARecord) -> ChatbotEvolutionAgentInput:
        return ChatbotEvolutionAgentInput(
            tenant_id=record.tenant_id,
            conversation_id=record.conversation_id,
            conversation_type=record.conversation_type,
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
                f"Chatbot evolution agent '{agent_name}' "
                f"(version={agent_version or 'latest'}) not found in AI Foundry."
            )
        return FoundryAgentReference(name=agent.name, version=agent.version)

    async def _invoke_agent(self, payload: ChatbotEvolutionAgentInput) -> str:
        """Call the hosted Chatbot Evolution Agent and return raw output text."""
        project_client = self._get_project_client()
        openai_client = project_client.get_openai_client(agent_name=self._agent_name())
        agent_ref = await self._resolve_agent_reference()

        # OpenAI Responses request shape is contract-bound to the SDK and
        # accepts a dict here; everything *inside* the message is built
        # from the typed `ChatbotEvolutionAgentInput` payload.
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
                "Chatbot evolution agent response not completed: id=%s status=%s error=%s",
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


__all__ = ["ChatbotEvolutionAgentService"]
