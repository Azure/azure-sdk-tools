"""Data models for the Teams channel backfill endpoints."""

from __future__ import annotations

from datetime import datetime
from enum import Enum

from pydantic import BaseModel, Field


class TeamsBackfillRequest(BaseModel):
    """Ask the server to import historical posts from the configured channels.

    Both fields narrow an otherwise complete run: ``channel_id`` restricts it
    to one channel of the collection allowlist, and ``start_time`` drops every
    thread whose post is older than that instant.
    """

    channel_id: str | None = Field(default=None, max_length=500)
    start_time: str | None = Field(default=None, max_length=100)


class TeamsBackfillStatus(str, Enum):
    """Lifecycle of a single backfill run."""

    running = "running"
    succeeded = "succeeded"
    failed = "failed"
    cancelled = "cancelled"


class TeamsBackfillJob(BaseModel):
    """State of one backfill run, from acceptance to outcome.

    ``summary`` carries the per-message counters the collection service
    reports, and is present only once the run has succeeded.
    """

    job_id: str
    status: TeamsBackfillStatus
    channel_id: str | None = None
    start_time: str | None = None
    started_at: datetime
    completed_at: datetime | None = None
    summary: dict[str, int] | None = None
    error: str | None = None
