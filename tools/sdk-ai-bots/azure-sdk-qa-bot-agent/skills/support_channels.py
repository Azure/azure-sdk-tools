"""Dynamic ``InlineSkill`` builder for the ``support-channels`` skill."""

from __future__ import annotations

import logging

from agent_framework import InlineSkill, SkillFrontmatter

from models.support_channel import get_support_channel_catalog

logger = logging.getLogger(__name__)

_SUPPORT_CHANNELS_SKILL_NAME = "support-channels"
_SUPPORT_CHANNELS_DESCRIPTION = (
    "Look up the right Teams support channel key when a question belongs to "
    "another channel's domain so users know where domain experts can help "
    "further. DO NOT USE FOR: questions that belong to the current channel."
)
_SUPPORT_CHANNELS_BODY = """# Support Channels

Load this skill when the user's question belongs to a different domain/channel than the current channel, so the bot can suggest where domain experts can provide a deeper dive.

- Do not refer the user back to the current channel (see `current_channel` in `[tenant_context]`, or `[tenant_scope]` if omitted).
- For a Teams channel handoff, append `{{{{channel:<key>}}}}` at the end of your response using a key below, and do not write a "Not resolved? Please re-post..." footer yourself. Keep keys internal.

## Channels

{channels}"""


def create_support_channels_skill() -> InlineSkill | None:
    """Build the ``support-channels`` ``InlineSkill`` from ``support-channel.yaml``."""
    catalog = get_support_channel_catalog()
    channels_markdown = (
        catalog.format_markdown() if catalog and catalog.support_channels else ""
    )
    if not channels_markdown:
        logger.info("Skipping support-channels skill: catalog is empty or unavailable")
        return None

    return InlineSkill(
        frontmatter=SkillFrontmatter(
            name=_SUPPORT_CHANNELS_SKILL_NAME,
            description=_SUPPORT_CHANNELS_DESCRIPTION,
        ),
        instructions=_SUPPORT_CHANNELS_BODY.format(channels=channels_markdown),
    )
