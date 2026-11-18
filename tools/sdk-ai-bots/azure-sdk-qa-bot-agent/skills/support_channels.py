"""Runtime channel-catalog injection for the ``support-channels`` skill."""

from __future__ import annotations

from collections.abc import Sequence
import logging
from pathlib import Path

from agent_framework import InlineSkill, Skill

from models.support_channel import get_support_channel_catalog

logger = logging.getLogger(__name__)

_SUPPORT_CHANNELS_SKILL_NAME = "support-channels"
_SUPPORT_CHANNELS_PLACEHOLDER = "{{support_channels}}"


def _extract_skill_body(skill: Skill) -> str:
    if isinstance(skill, InlineSkill):
        return skill.instructions
    skill_path = getattr(skill, "path", None)
    if skill_path:
        raw = (Path(skill_path) / "SKILL.md").read_text(encoding="utf-8")
        if raw.startswith("---"):
            parts = raw.split("---", 2)
            if len(parts) == 3:
                return parts[2].lstrip("\r\n")
        return raw
    content = getattr(skill, "content", "")
    return content if isinstance(content, str) else ""


def inject_support_channels(skills: Sequence[Skill]) -> list[Skill]:
    """Inject configured support channels into the file-based ``support-channels`` skill."""
    catalog = get_support_channel_catalog()
    channels_markdown = (
        catalog.format_markdown() if catalog and catalog.support_channels else ""
    )

    result: list[Skill] = []
    for skill in skills:
        if skill.frontmatter.name == _SUPPORT_CHANNELS_SKILL_NAME:
            if not channels_markdown:
                logger.info("Skipping support-channels skill: catalog is empty or unavailable")
                continue
            body = _extract_skill_body(skill)
            if _SUPPORT_CHANNELS_PLACEHOLDER in body:
                rendered = body.replace(_SUPPORT_CHANNELS_PLACEHOLDER, channels_markdown)
            else:
                rendered = f"{body}\n\n{channels_markdown}"
            result.append(
                InlineSkill(
                    frontmatter=skill.frontmatter,
                    instructions=rendered,
                )
            )
        else:
            result.append(skill)
    return result
