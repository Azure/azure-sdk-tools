"""Models for bot configuration lookups."""

from __future__ import annotations

from typing import Literal

from pydantic import BaseModel, Field, StrictBool


ConfidenceLevel = Literal["high", "medium", "low"]


class ExpertIdentity(BaseModel):
    id: str = Field(min_length=1)
    name: str = Field(min_length=1)


class BotSettings(BaseModel):
    show_confidence_label: StrictBool = False
    allow_replies_after_humans: StrictBool = False
    allow_notify_experts: StrictBool = False
    expert_help_threshold: ConfidenceLevel = "high"
    experts: list[ExpertIdentity] = Field(default_factory=list)

    @property
    def requires_confidence(self) -> bool:
        """Whether answer rendering or expert notifications need an assessment."""
        return self.show_confidence_label or self.allow_notify_experts

    @property
    def enhanced_intention_rules_enabled(self) -> bool:
        """Use added participation rules; False keeps the original reply rules."""
        return (
            self.show_confidence_label
            or self.allow_replies_after_humans
            or self.allow_notify_experts
        )


class ChannelConfigResponse(BaseModel):
    channel_id: str
    tenant_id: str | None = None
    bot_settings: BotSettings = Field(default_factory=BotSettings)
