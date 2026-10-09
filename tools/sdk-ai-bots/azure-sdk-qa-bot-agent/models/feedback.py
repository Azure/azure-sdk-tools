"""Data models for the feedback workflow."""

from __future__ import annotations

import re
from datetime import datetime
from enum import Enum
from typing import Any
from urllib.parse import unquote, urlparse

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
    conversation_id: str | None = Field(
        default=None, description="Exact conversation/thread ID used to store messages"
    )
    conversation_type: ConversationType | None = None
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
    issue_reused = "issue_reused"
    remediation_failed = "remediation_failed"
    validation_passed = "validation_passed"
    validation_failed = "validation_failed"
    validation_skipped = "validation_skipped"
    processing_failed = "processing_failed"


class RootCauseClassification(str, Enum):
    """Dominant root-cause categories used by the agent."""

    missing_content = "missing_content"
    outdated_content = "outdated_content"
    insufficient_content = "insufficient_content"
    retrieval_mismatch = "retrieval_mismatch"
    reasoning_gap = "reasoning_gap"
    out_of_scope = "out_of_scope"


class GitHubIssueReference(BaseModel):
    """Canonical identity for a GitHub issue."""

    owner: str
    repository: str
    number: int = Field(gt=0)


class AzureDevOpsIssueReference(BaseModel):
    """Canonical identity for an Azure Boards work item."""

    organization: str
    project: str
    work_item_id: int = Field(gt=0)


_GITHUB_ISSUE_PATH = re.compile(
    r"^/(?P<owner>[^/]+)/(?P<repo>[^/]+)/issues/(?P<number>\d+)/?$",
    flags=re.IGNORECASE,
)
_ADO_WORK_ITEM_PATH = re.compile(
    r"^/(?P<organization>[^/]+)/(?P<project>[^/]+)/_workitems/edit/"
    r"(?P<number>\d+)/?$",
    flags=re.IGNORECASE,
)
_KB_CLASSIFICATIONS = frozenset(
    {
        RootCauseClassification.missing_content,
        RootCauseClassification.outdated_content,
        RootCauseClassification.insufficient_content,
    }
)

def parse_issue_reference(
    value: str,
    *,
    canonical: bool = True,
) -> GitHubIssueReference | AzureDevOpsIssueReference:
    """Parse a supported issue URL into a provider identity."""
    parsed = urlparse(value)
    if (
        parsed.scheme != "https"
        or (canonical and value != value.strip())
        or (canonical and (parsed.query or parsed.fragment))
    ):
        raise ValueError("issue_url must be a canonical HTTPS issue URL")

    if parsed.netloc.lower() == "github.com":
        match = _GITHUB_ISSUE_PATH.fullmatch(parsed.path)
        if match is not None:
            number_text = match.group("number")
            number = int(number_text)
            if number <= 0 or (canonical and number_text != str(number)):
                raise ValueError("issue_url must use a canonical positive issue number")
            return GitHubIssueReference(
                owner=match.group("owner"),
                repository=match.group("repo"),
                number=number,
            )

    if parsed.netloc.lower() == "dev.azure.com":
        match = _ADO_WORK_ITEM_PATH.fullmatch(parsed.path)
        if match is not None:
            number_text = match.group("number")
            number = int(number_text)
            if number <= 0 or (canonical and number_text != str(number)):
                raise ValueError(
                    "issue_url must use a canonical positive work-item number"
                )
            return AzureDevOpsIssueReference(
                organization=unquote(match.group("organization")),
                project=unquote(match.group("project")),
                work_item_id=number,
            )

    raise ValueError("issue_url must identify a GitHub issue or ADO work item")


def is_fallback_issue_reference(
    issue: GitHubIssueReference | AzureDevOpsIssueReference,
) -> bool:
    """Return whether an issue belongs to the evolution fallback repository."""
    return (
        isinstance(issue, GitHubIssueReference)
        and issue.owner.casefold() == "azure"
        and issue.repository.casefold() == "azure-sdk-pr"
    )


class ChatbotEvolutionAgentInput(BaseModel):
    """Structured input sent to the hosted chatbot evolution agent.

    Serialized as JSON in a single `user` message — the agent's
    instruction.md spec calls out this exact schema. Feedback is scoped to a
    whole **conversation (QA thread)**, not a single bot reply, so the payload
    carries only the thread coordinates. The agent reconstructs the transcript
    with `fetch_conversation` and derives each bot turn's `trace_id` from it.
    """

    model_config = ConfigDict(extra="forbid")

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
        if value is not None:
            parse_issue_reference(value)
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
    source_id: str | None = Field(default=None, min_length=1)
    source_url: str | None = None
    has_expert_interaction: bool | None = Field(default=None, strict=True)
    expert_interaction_reason: str | None = Field(
        default=None, min_length=1, max_length=500
    )

    @field_validator("issue_url")
    @classmethod
    def validate_issue_url(cls, value: str | None) -> str | None:
        if value is not None:
            parse_issue_reference(value)
        return value

    @model_validator(mode="after")
    def validate_outcome(self) -> "ChatbotEvolutionAgentResult":
        if self.outcome in (
            ChatbotEvolutionAgentOutcome.issue_created,
            ChatbotEvolutionAgentOutcome.issue_reused,
        ):
            if not self.issue_url or self.classification is None:
                raise ValueError(
                    "issue_created and issue_reused require classification, "
                    "and issue_url"
                )
            issue = parse_issue_reference(self.issue_url)
            if self.classification in _KB_CLASSIFICATIONS:
                if not self.source_id:
                    raise ValueError("KB issue outcomes require source_id")
            else:
                if self.source_id is not None or self.source_url is not None:
                    raise ValueError(
                        "System issue outcomes cannot include source_id or source_url"
                    )
                if not is_fallback_issue_reference(issue):
                    raise ValueError(
                        "System issue outcomes require an Azure/azure-sdk-pr issue"
                    )
        elif self.outcome == ChatbotEvolutionAgentOutcome.remediation_failed:
            if (
                self.issue_url is not None
                or self.source_id is not None
                or self.source_url is not None
            ):
                raise ValueError(
                    "remediation_failed cannot include issue_url, source_id, "
                    "or source_url"
                )
        elif (
            self.issue_url is not None
            or self.source_id is not None
            or self.source_url is not None
            or self.classification is not None
        ):
            raise ValueError(
                "classification is only valid for issue_created, issue_reused, "
                "or remediation_failed; issue_url is only valid for issue_created "
                "or issue_reused; source_id and source_url are only valid for "
                "issue_created or issue_reused"
            )
        return self


class FoundryAgentReference(BaseModel):
    """`agent_reference` extra-body block for the Responses API."""

    name: str
    version: str
    type: str = "agent_reference"

    def to_extra_body(self) -> dict[str, Any]:
        return self.model_dump()
