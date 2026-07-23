"""Unit tests for remote Agent Skills loading."""

from __future__ import annotations

import sys
from pathlib import Path
from unittest.mock import AsyncMock, patch

import pytest

# Ensure the project root is on sys.path so ``skills`` resolves.
_PROJECT_ROOT = str(Path(__file__).resolve().parent.parent)
if _PROJECT_ROOT not in sys.path:
    sys.path.insert(0, _PROJECT_ROOT)

from skills.remote_skills import (
    RemoteAgentSkillsSource,
    _parse_reference_descriptions,
    _split_frontmatter,
    load_remote_skills,
)

_SKILL_MD = """---
name: sample-skill
description: "A sample skill for tests."
metadata:
  version: "1.0.0"
---

# Sample Skill

Body text.

| Reference | Rule Area | Rule IDs |
| --- | --- | --- |
| [secret-detection.md](references/secret-detection.md) | Secret detection | SEC-1 |
| [naming.md](references/naming.md) | Naming conventions | -- |
"""


def test_split_frontmatter_parses_name_and_body() -> None:
    fm, body = _split_frontmatter(_SKILL_MD)
    assert fm["name"] == "sample-skill"
    assert fm["description"] == "A sample skill for tests."
    assert body.startswith("# Sample Skill")


def test_split_frontmatter_without_frontmatter() -> None:
    fm, body = _split_frontmatter("# No frontmatter\n\ntext")
    assert fm == {}
    assert body == "# No frontmatter\n\ntext"


def test_parse_reference_descriptions() -> None:
    _, body = _split_frontmatter(_SKILL_MD)
    descriptions = _parse_reference_descriptions(body)
    assert descriptions["references/secret-detection.md"] == "Secret detection"
    assert descriptions["references/naming.md"] == "Naming conventions"


def _fake_get_text(url: str) -> str:
    if url.endswith("SKILL.md"):
        return _SKILL_MD
    return f"content of {url}"


@pytest.mark.asyncio
async def test_get_skills_builds_inline_skill_with_resources() -> None:
    source = RemoteAgentSkillsSource(raw_base_url="https://example.test/skill/")
    with patch.object(
        RemoteAgentSkillsSource, "_get_text", new=AsyncMock(side_effect=_fake_get_text)
    ):
        skills = await source.get_skills()

    assert len(skills) == 1
    skill = skills[0]
    assert skill.frontmatter.name == "sample-skill"
    assert skill.frontmatter.description == "A sample skill for tests."
    resource_names = {r.name for r in skill.resources}
    assert resource_names == {
        "references/secret-detection.md",
        "references/naming.md",
    }
    # The loaded skill content advertises the reference resources.
    assert "<resources>" in skill.content


@pytest.mark.asyncio
async def test_get_skills_base_url_normalized_and_resource_fetched_lazily() -> None:
    calls: list[str] = []

    async def _record(self: RemoteAgentSkillsSource, url: str) -> str:  # noqa: ANN001
        calls.append(url)
        return _fake_get_text(url)

    # Base URL without trailing slash must be normalized.
    source = RemoteAgentSkillsSource(raw_base_url="https://example.test/skill")
    with patch.object(RemoteAgentSkillsSource, "_get_text", new=_record):
        skills = await source.get_skills()
        # Only SKILL.md fetched during discovery; references are lazy.
        assert calls == ["https://example.test/skill/SKILL.md"]

        resource = next(
            r for r in skills[0].resources if r.name == "references/naming.md"
        )
        content = await resource.read()
        assert content == "content of https://example.test/skill/references/naming.md"
        # Reading again is cached — no second network call.
        await resource.read()
        assert calls.count(
            "https://example.test/skill/references/naming.md"
        ) == 1


@pytest.mark.asyncio
async def test_get_skills_returns_empty_on_fetch_failure() -> None:
    source = RemoteAgentSkillsSource(raw_base_url="https://example.test/skill/")
    with patch.object(
        RemoteAgentSkillsSource,
        "_get_text",
        new=AsyncMock(side_effect=RuntimeError("boom")),
    ):
        skills = await source.get_skills()
    assert skills == []


@pytest.mark.asyncio
async def test_resource_read_returns_error_string_on_failure() -> None:
    async def _side_effect(self: RemoteAgentSkillsSource, url: str) -> str:  # noqa: ANN001
        if url.endswith("SKILL.md"):
            return _SKILL_MD
        raise RuntimeError("unreachable")

    source = RemoteAgentSkillsSource(raw_base_url="https://example.test/skill/")
    with patch.object(RemoteAgentSkillsSource, "_get_text", new=_side_effect):
        skills = await source.get_skills()
        result = await skills[0].resources[0].read()
    assert result.startswith("Error: unable to fetch resource")


@pytest.mark.asyncio
async def test_load_remote_skills_disabled_returns_empty() -> None:
    with patch.object(
        RemoteAgentSkillsSource, "_get_text", new=AsyncMock()
    ) as mock_get:
        skills = await load_remote_skills(enabled=False)
    assert skills == []
    mock_get.assert_not_called()
