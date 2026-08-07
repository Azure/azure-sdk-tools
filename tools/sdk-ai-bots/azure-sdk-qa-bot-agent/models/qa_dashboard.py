"""Models for the QA record dashboard."""

from enum import Enum

from pydantic import BaseModel, Field

from models.qa_record import QARecord


class FeedbackStatusFilter(str, Enum):
    """Feedback lifecycle values exposed by the dashboard filter."""

    not_started = "not_started"
    created = "created"
    running = "running"
    pending_validation = "pending_validation"
    done = "done"
    failed = "failed"


class QARecordPage(BaseModel):
    """One server-paginated dashboard result."""

    items: list[QARecord]
    total: int = Field(ge=0)
    page: int = Field(ge=1)
    page_size: int = Field(ge=1)
    tenants: list[str]


__all__ = ["FeedbackStatusFilter", "QARecordPage"]
