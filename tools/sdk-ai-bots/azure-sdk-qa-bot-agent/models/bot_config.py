"""Models for bot configuration lookups."""

from __future__ import annotations

from pydantic import BaseModel


class ChannelConfigResponse(BaseModel):
    channel_id: str
    tenant_id: str | None = None
