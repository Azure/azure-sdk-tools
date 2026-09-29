"""Models for the QA record dashboard."""

from datetime import datetime
from enum import Enum

from pydantic import BaseModel, Field, computed_field

from models.conversation import Role
from models.feedback import RootCauseClassification
from models.qa_record import QARecord


class FeedbackStatusFilter(str, Enum):
    """Feedback lifecycle values exposed by the dashboard filter."""

    not_started = "not_started"
    created = "created"
    running = "running"
    pending_validation = "pending_validation"
    validation_passed = "validation_passed"
    validation_failed = "validation_failed"
    validation_skipped = "validation_skipped"
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


class OverviewMetric(BaseModel):
    """A percentage with its underlying numerator and denominator."""

    numerator: int = Field(ge=0)
    denominator: int = Field(ge=0)
    rate: float | None


def _metric(numerator: int, denominator: int) -> OverviewMetric:
    return OverviewMetric(
        numerator=numerator,
        denominator=denominator,
        rate=100 * numerator / denominator if denominator else None,
    )


class OverviewCounts(BaseModel):
    """Additive counts used to calculate channel metrics and weighted totals."""

    conversations: int = Field(default=0, ge=0)
    correct: int = Field(default=0, ge=0)
    incorrect: int = Field(default=0, ge=0)
    accuracy_excluded: int = Field(default=0, ge=0)
    expert_yes: int = Field(default=0, ge=0)
    questions: int = Field(default=0, ge=0)
    answered_questions: int = Field(default=0, ge=0)
    findings: int = Field(default=0, ge=0)
    issue_cases: int = Field(default=0, ge=0)
    resolved_cases: int = Field(default=0, ge=0)
    pending_validation_cases: int = Field(default=0, ge=0)
    validation_failed_cases: int = Field(default=0, ge=0)
    validation_skipped_cases: int = Field(default=0, ge=0)
    processing_error_cases: int = Field(default=0, ge=0)
    other_issue_cases: int = Field(default=0, ge=0)


class OverviewRow(OverviewCounts):
    """Channel or total counts with serialized, derived metrics."""

    channel_id: str | None = None
    channel_name: str

    # Distinct issue counts are not additive across channels.
    tracked_issues: int = Field(default=0, ge=0)
    root_causes: dict[RootCauseClassification, int] = Field(default_factory=dict)

    @computed_field
    @property
    def unresolved_cases(self) -> int:
        """Issue-linked cases that are neither validated resolved nor skipped."""
        return self.issue_cases - self.resolved_cases - self.validation_skipped_cases

    @computed_field
    @property
    def resolved_rate(self) -> OverviewMetric:
        eligible = self.issue_cases - self.validation_skipped_cases
        return _metric(self.resolved_cases, eligible)

    @computed_field
    @property
    def accuracy(self) -> OverviewMetric:
        eligible = self.conversations - self.accuracy_excluded
        return _metric(eligible - self.incorrect, eligible)

    @computed_field
    @property
    def expert_interaction(self) -> OverviewMetric:
        return _metric(self.expert_yes, self.conversations)

    @computed_field
    @property
    def answer_rate(self) -> OverviewMetric:
        return _metric(self.answered_questions, self.questions)


class QAOverview(BaseModel):
    """Full report for a half-open date range, optionally scoped to one channel."""

    start: datetime
    end: datetime
    generated_at: datetime
    channel_id: str | None = None
    rows: list[OverviewRow]
    totals: OverviewRow


__all__ = [
    "DashboardChannel",
    "DashboardConversationMessage",
    "FeedbackStatusFilter",
    "OverviewCounts",
    "OverviewMetric",
    "OverviewRow",
    "QADashboardDetail",
    "QADashboardRecord",
    "QAOverview",
    "QARecordPage",
]
