"""Answering-model evidence assessments."""

from __future__ import annotations

from typing import Literal

from pydantic import BaseModel, ConfigDict, Field, field_validator, model_validator

AspectValue = Literal["pass", "limited", "fail", "not_applicable"]
CURRENT_RUBRIC_VERSION: Literal["evidence-v3"] = "evidence-v3"
CURRENT_SCHEMA_VERSION: Literal["confidence-runtime-v3"] = "confidence-runtime-v3"


class StrictAssessmentModel(BaseModel):
    model_config = ConfigDict(extra="forbid", frozen=True)


class AspectResult(StrictAssessmentModel):
    value: AspectValue
    reason: str = Field(
        min_length=1,
        description="One short, single-line sentence identifying the evidence or gap behind this value.",
    )

    @field_validator("reason")
    @classmethod
    def validate_reason(cls, value: str) -> str:
        reason = value.strip()
        if not reason or len(reason.splitlines()) != 1:
            raise ValueError("Each aspect reason must be nonempty and single-line.")
        return reason


class EvidenceAspects(StrictAssessmentModel):
    support: AspectResult
    fit: AspectResult
    source_quality: AspectResult
    consistency: AspectResult
    coverage: AspectResult

    def items(self) -> tuple[tuple[str, AspectResult], ...]:
        return (
            ("support", self.support),
            ("fit", self.fit),
            ("source_quality", self.source_quality),
            ("consistency", self.consistency),
            ("coverage", self.coverage),
        )


class GeneratedAssessmentAnswer(StrictAssessmentModel):
    schema_version: Literal["confidence-runtime-v3"]
    rubric_version: Literal["evidence-v3"]
    answer: str = Field(min_length=1)
    status: Literal["valid", "abstained"]
    aspects: EvidenceAspects | None

    @model_validator(mode="after")
    def validate_status(self) -> GeneratedAssessmentAnswer:
        if (self.status == "valid") != (self.aspects is not None):
            raise ValueError("Only valid assessments have aspect values and reasons.")
        if not self.answer.strip():
            raise ValueError("The generated answer must not be blank.")
        return self


class EvidenceAssessment(StrictAssessmentModel):
    """Assessment metadata returned alongside the processed chat answer."""

    schema_version: Literal["confidence-runtime-v3"]
    rubric_version: Literal["evidence-v3"]
    status: Literal["valid", "abstained"]
    aspects: EvidenceAspects | None
    provisional_band: Literal["High", "Medium", "Low"] | None

    @model_validator(mode="after")
    def validate_status(self) -> EvidenceAssessment:
        if (self.status == "valid") != (self.aspects is not None):
            raise ValueError("Only valid assessments have aspect values and reasons.")
        if self.status == "abstained" and self.provisional_band is not None:
            raise ValueError("Abstained assessments cannot have an evidence band.")
        return self
