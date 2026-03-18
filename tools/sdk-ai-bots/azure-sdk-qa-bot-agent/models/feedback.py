"""Data models for the feedback workflow."""

from __future__ import annotations

from pydantic import BaseModel


class FeedbackRequest(BaseModel):
    """Incoming feedback payload from the Teams App."""

    tenant_id: str = "unknown"
    messages: list[dict] = []
    reaction: str = "unknown"  # "good" or "bad"
    comment: str | None = None
    reasons: list[str] = []


class FeedbackResponse(BaseModel):
    """Result of processing a feedback request."""

    saved: bool = False
    issue_url: str | None = None
