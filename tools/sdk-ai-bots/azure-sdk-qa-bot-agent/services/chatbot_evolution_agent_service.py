"""Run Chatbot Evolution Agent analysis and validation jobs.

The service invokes the hosted agent synchronously, parses its fixed JSON
result, and owns the corresponding QA-record lifecycle transitions.
"""

from __future__ import annotations

import asyncio
import logging
from datetime import datetime, timezone
from typing import cast

from azure.ai.projects.aio import AIProjectClient
from openai.types.responses.response_input_item_param import ResponseInputItemParam

from config.app_config import get as cfg
from models.conversation import BotAnswerVerdict
from models.feedback import (
    ChatbotEvolutionAgentInput,
    ChatbotEvolutionAgentMode,
    ChatbotEvolutionAgentOutcome,
    ChatbotEvolutionAgentResult,
    FoundryAgentReference,
)
from models.qa_record import FeedbackState, FeedbackStatus, QARecord, QAStatus
from utils.azure_ai_foundry import get_project_client
from utils.azure_cosmosdb import read_qa_record, upsert_qa_record

logger = logging.getLogger(__name__)

_ALLOWED_OUTCOMES = {
    ChatbotEvolutionAgentMode.analysis: frozenset(
        {
            ChatbotEvolutionAgentOutcome.conversation_ongoing,
            ChatbotEvolutionAgentOutcome.no_issue,
            ChatbotEvolutionAgentOutcome.issue_created,
            ChatbotEvolutionAgentOutcome.remediation_failed,
            ChatbotEvolutionAgentOutcome.processing_failed,
        }
    ),
    ChatbotEvolutionAgentMode.validation: frozenset(
        {
            ChatbotEvolutionAgentOutcome.validation_passed,
            ChatbotEvolutionAgentOutcome.validation_failed,
            ChatbotEvolutionAgentOutcome.processing_failed,
        }
    ),
}

# Hard cap on Agent wall-clock time for one synchronous pipeline job.
_JOB_TIMEOUT_SECS = 600

# Keep informational logs bounded; the structured result is persisted in
# Cosmos, so logs need only a diagnostic preview.
_AGENT_REPLY_LOG_PREVIEW_CHARS = 4000


class ChatbotEvolutionAgentService:
    """Own the synchronous lifecycle of hosted evolution-agent jobs."""

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
        return cfg(self._AGENT_VERSION_KEY, "") or None

    def _get_project_client(self) -> AIProjectClient:
        if self._project_client is None:
            self._project_client = get_project_client()
        return self._project_client

    # -- Public job lifecycle ----------------------------------------------

    async def run_job(
        self,
        record_id: str,
        tenant_id: str,
        *,
        mode: ChatbotEvolutionAgentMode = ChatbotEvolutionAgentMode.analysis,
    ) -> ChatbotEvolutionAgentResult | None:
        """Run analysis or validation for one persisted QA record."""
        record = await self._load_job(record_id, tenant_id)
        if record is None or not self._can_run(record, mode):
            return None

        await self._transition(record, FeedbackStatus.created)
        await self._transition(record, FeedbackStatus.running)
        payload = self._build_input(record, mode)

        try:
            agent_text = await asyncio.wait_for(
                self._invoke_agent(payload), timeout=_JOB_TIMEOUT_SECS
            )
            result = ChatbotEvolutionAgentResult.model_validate_json(agent_text)
            if result.outcome not in _ALLOWED_OUTCOMES[mode]:
                raise ValueError(
                    f"Agent returned outcome={result.outcome.value} "
                    f"for mode={mode.value}"
                )
            self._apply_result(record, result, mode=mode)
        except Exception as exc:
            logger.exception(
                "Chatbot evolution agent %s failed for job %s",
                mode.value,
                record.id,
            )
            persisted = await self._load_job(record.id, record.tenant_id)
            if persisted is not None:
                record = persisted
            self._finalize_failed(
                record,
                mode=mode,
                error=f"agent_{mode.value}_failed: {exc}",
            )
            await upsert_qa_record(record.to_cosmos())
            return None

        preview = agent_text.strip()
        if len(preview) > _AGENT_REPLY_LOG_PREVIEW_CHARS:
            preview = preview[:_AGENT_REPLY_LOG_PREVIEW_CHARS] + " ...[truncated]"
        logger.info("Evolution job %s agent result: %s", record.id, preview)
        await upsert_qa_record(record.to_cosmos())
        return result

    # -- Cosmos helpers ----------------------------------------------------

    async def _load_job(self, record_id: str, tenant_id: str) -> QARecord | None:
        doc = await read_qa_record(record_id=record_id, tenant_id=tenant_id)
        if doc is None:
            logger.warning("QA record %s not found in tenant %s", record_id, tenant_id)
            return None
        return QARecord.from_cosmos(doc)

    # -- Eligibility and transitions --------------------------------------

    @staticmethod
    def _can_run(
        record: QARecord,
        mode: ChatbotEvolutionAgentMode,
    ) -> bool:
        feedback_status = record.feedback.status if record.feedback else None
        if mode == ChatbotEvolutionAgentMode.analysis:
            can_run = (
                record.qa_status == QAStatus.ongoing
                and feedback_status is None
            ) or (
                feedback_status == FeedbackStatus.failed
                and not bool(record.feedback and record.feedback.issue_url)
            )
        else:
            can_run = (
                feedback_status in (
                    FeedbackStatus.pending_validation,
                    FeedbackStatus.failed,
                )
                and bool(record.feedback and record.feedback.issue_url)
            )
        if not can_run:
            logger.info(
                "QA record %s is not eligible for %s (qa=%s, feedback=%s)",
                record.id,
                mode.value,
                record.qa_status.value,
                feedback_status.value if feedback_status else None,
            )
        return can_run

    async def _transition(
        self,
        record: QARecord,
        status: FeedbackStatus,
    ) -> None:
        feedback = record.feedback
        if feedback is None:
            feedback = FeedbackState(created_at=_now())
            record.feedback = feedback
        feedback.status = status
        feedback.error = None
        feedback.updated_at = _now()
        record.updated_at = _now()
        await upsert_qa_record(record.to_cosmos())

    # -- Structured outcome mapping ---------------------------------------

    def _apply_result(
        self,
        record: QARecord,
        result: ChatbotEvolutionAgentResult,
        *,
        mode: ChatbotEvolutionAgentMode = ChatbotEvolutionAgentMode.analysis,
    ) -> None:
        now = _now()
        record.updated_at = now

        if mode == ChatbotEvolutionAgentMode.analysis:
            record.reasoning = result.reasoning
            record.confidence = result.confidence
            record.evaluated_at = now

            if result.outcome == ChatbotEvolutionAgentOutcome.conversation_ongoing:
                record.qa_status = QAStatus.ongoing
                record.verdict = BotAnswerVerdict.Unknown
                record.feedback = None
                return

            assert record.feedback is not None
            record.feedback.updated_at = now
            record.feedback.error = None

            if result.outcome == ChatbotEvolutionAgentOutcome.processing_failed:
                record.qa_status = QAStatus.failed
                record.verdict = BotAnswerVerdict.Unknown
                record.feedback.status = FeedbackStatus.failed
                record.feedback.error = "agent_processing_failed"
                return

            if result.outcome == ChatbotEvolutionAgentOutcome.remediation_failed:
                record.qa_status = QAStatus.failed
                record.verdict = BotAnswerVerdict.Incorrect
                record.feedback.status = FeedbackStatus.failed
                record.feedback.error = "agent_remediation_failed"
                record.feedback.classification = result.classification
                return

            if result.outcome == ChatbotEvolutionAgentOutcome.no_issue:
                record.qa_status = QAStatus.finished
                record.verdict = BotAnswerVerdict.Correct
                record.feedback.status = FeedbackStatus.done
                return

            if result.outcome == ChatbotEvolutionAgentOutcome.issue_created:
                record.qa_status = QAStatus.failed
                record.verdict = BotAnswerVerdict.Incorrect
                record.feedback.status = FeedbackStatus.pending_validation
                record.feedback.issue_url = result.issue_url
                record.feedback.classification = result.classification
                return

            raise ValueError(
                f"Unsupported analysis outcome: {result.outcome.value}"
            )

        assert record.feedback is not None
        record.feedback.updated_at = now
        record.feedback.validation_reasoning = result.reasoning
        record.feedback.validated_at = now
        record.feedback.error = None

        if result.outcome == ChatbotEvolutionAgentOutcome.processing_failed:
            record.feedback.status = FeedbackStatus.failed
            record.feedback.error = "agent_processing_failed"
            return

        if result.outcome == ChatbotEvolutionAgentOutcome.validation_passed:
            record.feedback.status = FeedbackStatus.done
        elif result.outcome == ChatbotEvolutionAgentOutcome.validation_failed:
            record.feedback.status = FeedbackStatus.failed
            record.feedback.error = "validation_failed"
        else:
            raise ValueError(
                f"Unsupported validation outcome: {result.outcome.value}"
            )

    def _finalize_failed(
        self,
        record: QARecord,
        *,
        mode: ChatbotEvolutionAgentMode,
        error: str,
    ) -> None:
        now = _now()
        feedback = record.feedback
        if feedback is None:
            feedback = FeedbackState(created_at=now)
            record.feedback = feedback
        feedback.status = FeedbackStatus.failed
        feedback.error = error
        feedback.updated_at = now
        if mode == ChatbotEvolutionAgentMode.analysis:
            record.qa_status = QAStatus.failed
            record.verdict = BotAnswerVerdict.Unknown
            record.evaluated_at = now
        else:
            feedback.validation_reasoning = error
            feedback.validated_at = now
        record.updated_at = now

    # -- Foundry invocation ------------------------------------------------

    def _build_input(
        self,
        record: QARecord,
        mode: ChatbotEvolutionAgentMode,
    ) -> ChatbotEvolutionAgentInput:
        return ChatbotEvolutionAgentInput(
            conversation_id=record.conversation_id,
            conversation_type=record.conversation_type,
            evaluation_time=_now(),
            mode=mode,
            issue_url=record.feedback.issue_url if record.feedback else None,
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
        """Call the hosted Chatbot Evolution Agent and return its JSON text."""
        project_client = self._get_project_client()
        openai_client = project_client.get_openai_client(agent_name=self._agent_name())
        agent_ref = await self._resolve_agent_reference()

        # The Responses SDK accepts an untyped mapping at this boundary; the
        # message content itself comes from the validated Pydantic input model.
        input_items = [
            cast(
                ResponseInputItemParam,
                {
                    "type": "message",
                    "role": "user",
                    "content": [
                        {"type": "input_text", "text": payload.to_json()}
                    ],
                },
            )
        ]
        raw_response = await openai_client.responses.with_raw_response.create(
            input=input_items,
            store=True,
            stream=False,
            extra_body={"agent_reference": agent_ref.to_extra_body()},
        )
        response = raw_response.parse()
        if response.status != "completed":
            raise RuntimeError(
                f"Agent response {response.id} ended with status={response.status}: "
                f"{response.error}"
            )
        if not response.output_text:
            raise RuntimeError(f"Agent response {response.id} returned no output")
        return response.output_text


# ---------------------------------------------------------------------------
# Module-level helpers
# ---------------------------------------------------------------------------


def _now() -> datetime:
    return datetime.now(timezone.utc)


__all__ = ["ChatbotEvolutionAgentService"]
