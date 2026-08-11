"""Models for the QA record dashboard."""

from datetime import datetime
from enum import Enum

from pydantic import BaseModel, Field

from models.conversation import Role
from models.qa_record import QARecord


class FeedbackStatusFilter(str, Enum):
    """Feedback lifecycle values exposed by the dashboard filter."""

    not_started = "not_started"
    created = "created"
    running = "running"
    pending_validation = "pending_validation"
    done = "done"
    failed = "failed"


class DashboardChannel(BaseModel):
    """A channel available in the dashboard filter."""

    id: str
    name: str


class QADashboardRecord(QARecord):
    """A QA record enriched with presentation-only list fields."""

    conversation_title: str
    channel_name: str


class DashboardUserFeedback(BaseModel):
    """Explicit feedback attached to a conversation message."""

    reaction: str
    comment: str | None = None
    reasons: list[str] = Field(default_factory=list)
    user_name: str | None = None


class DashboardConversationMessage(BaseModel):
    """One message shown in the conversation timeline."""

    id: str
    role: Role
    sender_name: str
    content: str
    created_at: datetime
    message_link: str | None = None
    trace_id: str | None = None
    user_feedback: list[DashboardUserFeedback] = Field(default_factory=list)


class DashboardIssueSections(BaseModel):
    """Structured sections extracted from an Evolution Agent issue."""

    description: str | None = None
    feedback: str | None = None
    root_cause: str | None = None
    suggested_fix: str | None = None
    validation: str | None = None
    expected_behavior: str | None = None


class DashboardIssue(BaseModel):
    """Live remediation issue metadata."""

    url: str
    title: str
    state: str
    labels: list[str] = Field(default_factory=list)
    created_at: datetime
    updated_at: datetime
    closed_at: datetime | None = None
    sections: DashboardIssueSections
    body: str | None = None


class QADashboardDetail(BaseModel):
    """Complete on-demand view of one conversation and evolution run."""

    record: QADashboardRecord
    messages: list[DashboardConversationMessage]
    issue: DashboardIssue | None = None
    issue_lookup_error: str | None = None


class QARecordPage(BaseModel):
    """One server-paginated dashboard result."""

    items: list[QADashboardRecord]
    total: int = Field(ge=0)
    page: int = Field(ge=1)
    page_size: int = Field(ge=1)
    channels: list[DashboardChannel]


__all__ = [
    "DashboardChannel",
    "DashboardConversationMessage",
    "DashboardIssue",
    "DashboardIssueSections",
    "DashboardUserFeedback",
    "FeedbackStatusFilter",
    "QADashboardDetail",
    "QADashboardRecord",
    "QARecordPage",
]
