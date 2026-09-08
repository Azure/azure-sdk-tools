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


class DashboardConversationMessage(BaseModel):
    """One message shown in the conversation timeline."""

    id: str
    role: Role
    sender_name: str
    content: str
    created_at: datetime
    message_link: str | None = None
    trace_id: str | None = None


class QADashboardDetail(BaseModel):
    """Complete on-demand view of one conversation and evolution run."""

    record: QADashboardRecord
    messages: list[DashboardConversationMessage]


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
    "FeedbackStatusFilter",
    "QADashboardDetail",
    "QADashboardRecord",
    "QARecordPage",
]
