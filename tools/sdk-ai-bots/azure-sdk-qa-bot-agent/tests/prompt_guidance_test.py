from __future__ import annotations

from pathlib import Path


_PROJECT_ROOT = Path(__file__).resolve().parent.parent


def test_onboarding_release_plan_guidance_preserves_agent_dashboard_boundary() -> None:
    prompt = (
        _PROJECT_ROOT / "prompts" / "tenants" / "azure_sdk_onboarding.md"
    ).read_text(encoding="utf-8")

    assert "Release Plan Dashboard" in prompt
    assert "view release lifecycle status" in prompt
    assert "identify required actions" in prompt
    assert "obtain recommended prompts" in prompt
    assert "Azure SDK Tools Agent" in prompt
    assert "create or update release plans" in prompt
    assert "perform the required actions" in prompt
    assert "Public Preview and eligible GA release plans may be auto-created" in prompt
    assert "after the spec PR merges" in prompt
    assert "Private Preview release plans are created through the Agent" in prompt
    assert "Azure SDK teams own platform/tooling and review support" in prompt
