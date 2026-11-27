"""Unit tests for ConversationService.record_should_reply.

Hermetic: the Cosmos container is stubbed, so no real Cosmos DB is required.
"""

from __future__ import annotations

import sys
from datetime import datetime, timezone
from pathlib import Path

import pytest
from azure.core import MatchConditions
from azure.cosmos.exceptions import CosmosResourceNotFoundError

_PROJECT_ROOT = str(Path(__file__).resolve().parent.parent)
if _PROJECT_ROOT not in sys.path:
    sys.path.insert(0, _PROJECT_ROOT)

import services.conversation_service as conversation_service_module
from models.conversation import (
    ConversationMessageItem,
    ConversationType,
    Role,
)
from services.conversation_service import ConversationService


def _make_message_item(should_reply: bool | None = None) -> ConversationMessageItem:
    return ConversationMessageItem(
        id="msg-1",
        sender_role=Role.User,
        sender_id="user-1",
        sender_name="Asker",
        content="Why does my TypeSpec build fail?",
        created_at=datetime.now(timezone.utc),
        conversation_id="conv-1",
        conversation_type=ConversationType.teams_channel,
        conversation_partition="teams_channel:conv-1",
        should_reply=should_reply,
    )


class _ContainerStub:
    def __init__(self, item: dict | None) -> None:
        self._item = item
        if self._item:
            self._item["_etag"] = "v1"
        self.replaced: dict | None = None

    async def read_item(self, item: str, partition_key: str):
        if self._item is None:
            raise CosmosResourceNotFoundError(status_code=404, message="Missing message")
        return self._item

    async def replace_item(self, *, item, body, etag, match_condition):
        assert item == self._item["id"]
        assert etag == "v1"
        assert match_condition == MatchConditions.IfNotModified
        self.replaced = body
        return body


@pytest.mark.asyncio
async def test_record_should_reply_updates_existing_message(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    """The flag is written onto the existing saved message using its ETag."""
    container = _ContainerStub(_make_message_item().model_dump(mode="json"))

    async def fake_container():
        return container

    monkeypatch.setattr(
        conversation_service_module,
        "get_conversation_message_container",
        fake_container,
    )

    await ConversationService().record_should_reply(
        "msg-1",
        "conv-1",
        ConversationType.teams_channel,
        True,
    )

    assert container.replaced is not None
    assert container.replaced["should_reply"] is True
    assert container.replaced["id"] == "msg-1"


@pytest.mark.asyncio
async def test_record_should_reply_missing_message_is_noop(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    """A 404 (message not found) does not raise and performs no write."""
    container = _ContainerStub(None)

    async def fake_container():
        return container

    monkeypatch.setattr(
        conversation_service_module,
        "get_conversation_message_container",
        fake_container,
    )

    await ConversationService().record_should_reply(
        "missing",
        "conv-1",
        ConversationType.teams_channel,
        False,
    )

    assert container.replaced is None
