"""Data models for the feedback workflow."""

from __future__ import annotations

import re
from datetime import datetime
from enum import Enum
from typing import Any, ClassVar

from pydantic import BaseModel, ConfigDict, Field, field_validator, model_validator

from models.conversation import ConversationType


class Reaction(str, Enum):
    """User feedback reaction types."""

    good = "good"
    bad = "bad"
    unknown = "unknown"


class FeedbackRequest(BaseModel):
    """Incoming feedback payload from the Teams App."""

    channel_id: str | None = None
    tenant_id: str = "unknown"
    reaction: Reaction = Reaction.unknown
    comment: str | None = None
    reasons: list[str] = []
    link: str | None = None
    user_name: str | None = None


class FeedbackResponse(BaseModel):
    """Result of processing a feedback request."""

    saved: bool = False
    issue_url: str | None = None


# ---------------------------------------------------------------------------
# Hosted chatbot-evolution-agent I/O contract
# ---------------------------------------------------------------------------


class ChatbotEvolutionAgentMode(str, Enum):
    """Operation requested from the hosted Chatbot Evolution Agent."""

    analysis = "analysis"
    validation = "validation"


class ChatbotEvolutionAgentOutcome(str, Enum):
    """Structured outcomes returned by the hosted agent."""

    conversation_ongoing = "conversation_ongoing"
    no_issue = "no_issue"
    issue_created = "issue_created"
    remediation_failed = "remediation_failed"
    validation_passed = "validation_passed"
    validation_failed = "validation_failed"
    processing_failed = "processing_failed"


class RootCauseClassification(str, Enum):
    """Dominant root-cause categories used by the agent."""

    missing_content = "missing_content"
    outdated_content = "outdated_content"
    insufficient_content = "insufficient_content"
    retrieval_mismatch = "retrieval_mismatch"
    reasoning_gap = "reasoning_gap"
    out_of_scope = "out_of_scope"


class ChatbotEvolutionAgentInput(BaseModel):
    """Structured input sent to the hosted chatbot evolution agent.

    Serialized as JSON in a single `user` message — the agent's
    instruction.md spec calls out this exact schema. Feedback is scoped to a
    whole **conversation (QA thread)**, not a single bot reply, so the payload
    carries only the thread coordinates. The agent reconstructs the transcript
    with `fetch_conversation` and derives each bot turn's `trace_id` from it.
    """

    model_config = ConfigDict(extra="forbid")
    issue_url_pattern: ClassVar[re.Pattern[str]] = re.compile(
        r"https://github\.com/Azure/azure-sdk-pr/issues/[1-9]\d*",
        flags=re.IGNORECASE,
    )

    conversation_id: str
    conversation_type: ConversationType
    evaluation_time: datetime
    mode: ChatbotEvolutionAgentMode = ChatbotEvolutionAgentMode.analysis
    issue_url: str | None = None

    @field_validator("evaluation_time")
    @classmethod
    def validate_evaluation_time(cls, value: datetime) -> datetime:
        if value.tzinfo is None or value.utcoffset() is None:
            raise ValueError("evaluation_time must include a UTC offset")
        return value

    @field_validator("issue_url")
    @classmethod
    def validate_issue_url(cls, value: str | None) -> str | None:
        if value is not None and cls.issue_url_pattern.fullmatch(value) is None:
            raise ValueError("issue_url must identify an Azure/azure-sdk-pr issue")
        return value

    @model_validator(mode="after")
    def validate_mode(self) -> "ChatbotEvolutionAgentInput":
        if (
            self.mode == ChatbotEvolutionAgentMode.validation
            and not self.issue_url
        ):
            raise ValueError("issue_url is required in validation mode")
        return self

    def to_json(self) -> str:
        return self.model_dump_json(exclude_none=False)


class ChatbotEvolutionAgentResult(BaseModel):
    """Fixed-schema result returned by the hosted agent."""

    model_config = ConfigDict(extra="forbid")

    outcome: ChatbotEvolutionAgentOutcome
    reasoning: str = Field(min_length=1)
    confidence: float = Field(ge=0.0, le=1.0)
    classification: RootCauseClassification | None = None
    issue_url: str | None = None

    @field_validator("issue_url")
    @classmethod
    def validate_issue_url(cls, value: str | None) -> str | None:
        if (
            value is not None
            and ChatbotEvolutionAgentInput.issue_url_pattern.fullmatch(value) is None
        ):
            raise ValueError("issue_url must identify an Azure/azure-sdk-pr issue")
        return value

    @model_validator(mode="after")
    def validate_outcome(self) -> "ChatbotEvolutionAgentResult":
        if self.outcome == ChatbotEvolutionAgentOutcome.issue_created:
            if not self.issue_url or self.classification is None:
                raise ValueError(
                    "issue_created requires classification and issue_url"
                )
        elif self.outcome == ChatbotEvolutionAgentOutcome.remediation_failed:
            if self.issue_url is not None:
                raise ValueError(
                    "remediation_failed cannot include issue_url"
                )
        elif self.issue_url is not None or self.classification is not None:
            raise ValueError(
                "classification is only valid for issue_created or "
                "remediation_failed; issue_url is only valid for issue_created"
            )
        return self


class FoundryAgentReference(BaseModel):
    """`agent_reference` extra-body block for the Responses API."""

    name: str
    version: str
    type: str = "agent_reference"

    def to_extra_body(self) -> dict[str, Any]:
        return self.model_dump()
