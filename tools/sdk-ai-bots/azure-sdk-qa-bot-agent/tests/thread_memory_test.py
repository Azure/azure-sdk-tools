"""Tests for ThreadMemoryService — tenant memory updates from thread messages."""

from __future__ import annotations

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
    UserRole,
)
from services.thread_memory_service import ThreadMemoryService


def _msg(
    mid: str,
    content: str,
    sender_role: UserRole = UserRole.user,
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
    sender_role: UserRole = UserRole.user,
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


def _patch_thread_service(monkeypatch, tenant_store="tenant-store", delay=60):
    monkeypatch.setattr(
        "services.thread_memory_service.get_tenant_store_name",
        lambda: tenant_store,
    )
    monkeypatch.setattr(
        "services.thread_memory_service.get_memory_update_delay",
        lambda: delay,
    )


# ---------------------------------------------------------------------------
# Role filtering
# ---------------------------------------------------------------------------

def test_build_memory_items_role_mapping() -> None:
    """Only human messages are included; bot (system) messages are filtered out."""
    service = ThreadMemoryService()
    thread = [
        _thread_item("m1", "How do I fix CI?", sender_role=UserRole.user),
        _thread_item("m2", "It's a known bug.", sender_role=UserRole.system),
        _thread_item("m3", "Use the workaround in docs.", sender_role=UserRole.user),
    ]

    items = service._build_memory_items(thread)

    assert len(items) == 2
    assert items[0] == {"type": "message", "role": "user", "content": "How do I fix CI?"}
    assert items[1] == {"type": "message", "role": "user", "content": "Use the workaround in docs."}


# ---------------------------------------------------------------------------
# Successful update
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_process_thread_update_calls_begin_update(monkeypatch) -> None:
    """Full thread is submitted to tenant store with correct scope and items."""
    _patch_thread_service(monkeypatch)

    fake_poller = SimpleNamespace(update_id="update-xyz")
    fake_memory_stores = AsyncMock()
    fake_memory_stores.begin_update_memories.return_value = fake_poller
    fake_project_client = SimpleNamespace(
        beta=SimpleNamespace(memory_stores=fake_memory_stores)
    )

    with patch(
        "services.thread_memory_service.get_project_client",
        return_value=fake_project_client,
    ):
        service = ThreadMemoryService()
        message = _msg("m3", "Use workaround", tenant_id="azure_sdk_onboarding")
        thread = [
            _thread_item("m1", "CI is failing", sender_role=UserRole.user),
            _thread_item("m2", "Known bug, fix in progress", sender_role=UserRole.system),
            _thread_item("m3", "Use workaround", sender_role=UserRole.user),
        ]

        await service.process_thread_update(message, thread)

    fake_memory_stores.begin_update_memories.assert_called_once()
    call_kwargs = fake_memory_stores.begin_update_memories.call_args.kwargs
    assert call_kwargs["name"] == "tenant-store"
    assert call_kwargs["scope"] == "azure_sdk_onboarding"
    assert call_kwargs["update_delay"] == 60
    assert len(call_kwargs["items"]) == 2  # bot message filtered out


# ---------------------------------------------------------------------------
# Incremental tracking (previous_update_id)
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_incremental_update_tracking(monkeypatch) -> None:
    """Second update for same conversation passes previous_update_id."""
    _patch_thread_service(monkeypatch)

    call_count = 0
    fake_memory_stores = AsyncMock()

    async def fake_begin_update(**kwargs):
        nonlocal call_count
        call_count += 1
        return SimpleNamespace(update_id=f"update-{call_count}")

    fake_memory_stores.begin_update_memories.side_effect = fake_begin_update
    fake_project_client = SimpleNamespace(
        beta=SimpleNamespace(memory_stores=fake_memory_stores)
    )

    with patch(
        "services.thread_memory_service.get_project_client",
        return_value=fake_project_client,
    ):
        service = ThreadMemoryService()
        message = _msg("m2", "reply", conversation_id="thread-1")
        thread = [
            _thread_item("m1", "question", conversation_id="thread-1"),
            _thread_item("m2", "reply", conversation_id="thread-1"),
        ]

        # First call — no previous_update_id
        await service.process_thread_update(message, thread)
        first_call = fake_memory_stores.begin_update_memories.call_args_list[0].kwargs
        assert "previous_update_id" not in first_call

        # Second call — should pass previous_update_id
        thread.append(_thread_item("m3", "another reply", conversation_id="thread-1"))
        message2 = _msg("m3", "another reply", conversation_id="thread-1")
        await service.process_thread_update(message2, thread)
        second_call = fake_memory_stores.begin_update_memories.call_args_list[1].kwargs
        assert second_call["previous_update_id"] == "update-1"

        assert service._update_ids.get("thread-1") == "update-2"
