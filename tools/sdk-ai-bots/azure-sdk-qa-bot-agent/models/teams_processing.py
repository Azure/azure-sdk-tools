"""Validated output produced for one archived Teams thread."""

from typing import Literal

from pydantic import BaseModel, ConfigDict, Field, field_validator, model_validator


class ResourceEnrichment(BaseModel):
    model_config = ConfigDict(extra="forbid")

    url: str
    access_status: Literal["accessed", "unavailable"]
    summary: str = Field(min_length=1)

    @field_validator("url")
    @classmethod
    def validate_url(cls, value: str) -> str:
        if not value.startswith(("http://", "https://")):
            raise ValueError("Resource enrichment URLs must use HTTP or HTTPS.")
        return value


class ThreadQASummary(BaseModel):
    model_config = ConfigDict(extra="forbid")

    title: str = Field(min_length=1)
    question: str = Field(min_length=1)
    answer: str = Field(min_length=1)


class ThreadProcessingDecision(BaseModel):
    model_config = ConfigDict(extra="forbid")

    status: Literal["included", "excluded"]
    exclusion_reason: str | None = None
    qa: ThreadQASummary | None = None
    resources: list[ResourceEnrichment] = Field(default_factory=list)

    @model_validator(mode="after")
    def validate_decision(self):
        if self.status == "included":
            if self.qa is None or self.exclusion_reason is not None:
                raise ValueError("Included threads require qa and no exclusion_reason.")
        elif self.qa is not None or not self.exclusion_reason:
            raise ValueError("Excluded threads require exclusion_reason and no qa.")
        return self
