"""Deterministic evidence-band rules."""

from __future__ import annotations

from typing import Literal

from models.evidence_assessment import EvidenceAspects


def provisional_band(aspects: EvidenceAspects | None) -> Literal["High", "Medium", "Low"] | None:
    if aspects is None:
        return None
    applicable = [result.value for _, result in aspects.items() if result.value != "not_applicable"]
    if not applicable:
        return None
    if "fail" in applicable:
        return "Low"
    if "limited" in applicable:
        return "Medium"
    return "High"
