"""Tests for ThreadMemoryService — episode extraction from thread messages."""

from __future__ import annotations

import json
import sys
from datetime import datetime, timezone
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import AsyncMock, patch

import pytest

_PROJECT_ROOT = str(Path(__file__).resolve().parent.parent)
if _PROJECT_ROOT not in sys.path:
    sys.path.insert(0, _PROJECT_ROOT)

from models.conversation import (
    ConversationMessage,
    ConversationMessageItem,
    ConversationType,
    Role,
)
from services.thread_memory_service import ThreadMemoryService


def _msg(
    mid: str,
    content: str,
    sender_role: Role = Role.User,
    sender_id: str = "user-1",
    sender_name: str = "Alice",
    tenant_id: str | None = "azure_sdk_onboarding",
    conversation_id: str = "thread-1",
    channel_id: str = "channel-1",
    created_at: datetime | None = None,
) -> ConversationMessage:
    return ConversationMessage(
        id=mid,
        channel_id=channel_id,
        sender_role=sender_role,
        sender_id=sender_id,
        sender_name=sender_name,
        content=content,
        created_at=created_at or datetime.now(timezone.utc),
        conversation_id=conversation_id,
        conversation_type=ConversationType.teams_channel,
        tenant_id=tenant_id,
    )


def _thread_item(
    mid: str,
    content: str,
    sender_role: Role = Role.User,
    sender_id: str = "user-1",
    sender_name: str = "Alice",
    tenant_id: str | None = "azure_sdk_onboarding",
    conversation_id: str = "thread-1",
    channel_id: str = "channel-1",
    created_at: datetime | None = None,
) -> ConversationMessageItem:
    return ConversationMessageItem(
        id=mid,
        channel_id=channel_id,
        sender_role=sender_role,
        sender_id=sender_id,
        sender_name=sender_name,
        content=content,
        created_at=created_at or datetime.now(timezone.utc),
        conversation_id=conversation_id,
        conversation_type=ConversationType.teams_channel,
        tenant_id=tenant_id,
        conversation_partition=f"teams_channel:{conversation_id}",
    )


# ---------------------------------------------------------------------------
# Quality gate — _qualifies_for_episode(message, thread_messages)
# ---------------------------------------------------------------------------


def test_qualifies_rejects_no_human_messages() -> None:
    """Threads with no human messages don't qualify."""
    service = ThreadMemoryService()
    message = _msg("m2", "Bot reply", sender_role=Role.System, sender_id="bot")
    thread = [
        {"sender_role": "system", "sender_id": "bot", "content": "Bot msg 1"},
        {"sender_role": "system", "sender_id": "bot", "content": "Bot msg 2"},
    ]
    assert service._qualifies_for_episode(message, thread) is False


def test_qualifies_accepts_expert_message() -> None:
    """Thread qualifies when latest message is from an expert (not bot, not poster)."""
    service = ThreadMemoryService()
    message = _msg("m3", "Try this workaround", sender_id="expert-1")
    thread = [
        {"sender_role": "user", "sender_id": "user-1", "content": "How do I fix CI?"},
        {"sender_role": "system", "sender_id": "bot", "content": "Bot reply"},
        {"sender_role": "user", "sender_id": "expert-1", "content": "Try this workaround"},
    ]
    assert service._qualifies_for_episode(message, thread) is True


def test_qualifies_rejects_when_latest_is_poster() -> None:
    """Reject when latest message is from the original poster."""
    service = ThreadMemoryService()
    message = _msg("m4", "Thanks!", sender_id="user-1")
    thread = [
        {"sender_role": "user", "sender_id": "user-1", "content": "How do I fix CI?"},
        {"sender_role": "user", "sender_id": "expert-1", "content": "Try this workaround"},
        {"sender_role": "user", "sender_id": "user-1", "content": "Thanks!"},
    ]
    assert service._qualifies_for_episode(message, thread) is False


def test_qualifies_rejects_when_latest_is_bot() -> None:
    """Reject when latest message is from bot."""
    service = ThreadMemoryService()
    message = _msg("m4", "Auto-reply", sender_role=Role.System, sender_id="bot")
    thread = [
        {"sender_role": "user", "sender_id": "user-1", "content": "Q?"},
        {"sender_role": "user", "sender_id": "expert-1", "content": "A."},
        {"sender_role": "system", "sender_id": "bot", "content": "Auto-reply"},
    ]
    assert service._qualifies_for_episode(message, thread) is False


# ---------------------------------------------------------------------------
# Sender check via process_thread_update (integration)
# ---------------------------------------------------------------------------


@pytest.mark.asyncio
async def test_skips_extraction_when_sender_is_bot() -> None:
    """Episode extraction is skipped when the latest message is from the bot."""
    service = ThreadMemoryService()
    message = _msg("m4", "Bot auto-reply", sender_role=Role.System, sender_id="bot")
    thread = [
        _thread_item("m1", "Question", sender_id="user-1"),
        _thread_item("m2", "Bot reply", sender_role=Role.System, sender_id="bot"),
        _thread_item("m3", "Expert reply", sender_id="expert-1"),
        _thread_item("m4", "Bot auto-reply", sender_role=Role.System, sender_id="bot"),
    ]

    with patch.object(service, "_call_llm", new_callable=AsyncMock) as mock_llm:
        await service.process_thread_update(message, thread)
        mock_llm.assert_not_called()


@pytest.mark.asyncio
async def test_skips_extraction_when_sender_is_poster() -> None:
    """Episode extraction is skipped when latest message is from original poster."""
    service = ThreadMemoryService()
    message = _msg("m4", "Thanks!", sender_id="user-1")
    thread = [
        _thread_item("m1", "Question", sender_id="user-1"),
        _thread_item("m2", "Bot reply", sender_role=Role.System, sender_id="bot"),
        _thread_item("m3", "Expert reply", sender_id="expert-1"),
        _thread_item("m4", "Thanks!", sender_id="user-1"),
    ]

    with patch.object(service, "_call_llm", new_callable=AsyncMock) as mock_llm:
        await service.process_thread_update(message, thread)
        mock_llm.assert_not_called()


# ---------------------------------------------------------------------------
# Format thread
# ---------------------------------------------------------------------------


def test_format_thread_labels_correctly() -> None:
    """Thread formatter labels bot vs human messages correctly."""
    service = ThreadMemoryService()
    thread = [
        {"sender_role": "user", "sender_name": "Alice", "content": "Question?"},
        {"sender_role": "system", "sender_name": "Azure SDK Q&A Bot", "content": "Bot response"},
        {"sender_role": "user", "sender_name": "Bob", "content": "Expert answer"},
    ]
    result = service._format_thread(thread)
    assert "[Alice]" in result
    assert "[Bot: Azure SDK Q&A Bot]" in result
    assert "[Bob]" in result


# ---------------------------------------------------------------------------
# Parse episode
# ---------------------------------------------------------------------------


def test_parse_episode_valid_json() -> None:
    """Valid JSON with all required fields produces an Episode."""
    import json
    raw = json.dumps({
        "domain": "SDK",
        "trigger": "CI fails",
        "symptoms": ["red pipeline"],
        "reasoning_chain": ["check logs", "find error"],
        "resolution": "fix config",
        "key_insight": "always check config",
        "confidence": 0.9,
    })
    result = ThreadMemoryService._parse_episode(raw)
    assert result is not None
    assert result.trigger == "CI fails"
    assert result.confidence == 0.9


def test_parse_episode_null_json() -> None:
    """JSON 'null' returns None."""
    result = ThreadMemoryService._parse_episode("null")
    assert result is None


def test_parse_episode_invalid_json() -> None:
    """Invalid JSON returns None."""
    result = ThreadMemoryService._parse_episode("not json at all")
    assert result is None


def test_parse_episode_wrapped_in_key() -> None:
    """LLM sometimes wraps the episode in an 'episode' key."""
    import json
    raw = json.dumps({
        "episode": {
            "domain": "SDK",
            "trigger": "Build error",
            "symptoms": ["compile failure"],
            "reasoning_chain": ["step 1", "step 2"],
            "resolution": "update dependency",
            "key_insight": "keep deps current",
            "confidence": 0.8,
        }
    })
    result = ThreadMemoryService._parse_episode(raw)
    assert result is not None
    assert result.trigger == "Build error"
