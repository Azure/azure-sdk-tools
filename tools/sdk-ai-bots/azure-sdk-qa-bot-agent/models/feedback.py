"""Data models for the feedback workflow."""

from __future__ import annotations

from pydantic import BaseModel


class FeedbackRequest(BaseModel):
    """Incoming feedback payload from the Teams App."""

    channel_id: str | None = None
    tenant_id: str = "unknown"
    reaction: str = "unknown"  # "good" or "bad"
    comment: str | None = None
    reasons: list[str] = []
    link: str | None = None
    user_name: str | None = None


class FeedbackResponse(BaseModel):
    """Result of processing a feedback request."""

    saved: bool = False
    issue_url: str | None = None
