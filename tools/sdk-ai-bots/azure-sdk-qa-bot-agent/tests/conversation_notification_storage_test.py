"""Hermetic notification-attempt tests with Cosmos ETag enforcement."""

from __future__ import annotations

import asyncio
from copy import deepcopy
from datetime import datetime, timezone
from pathlib import Path
import sys
from unittest.mock import AsyncMock

import pytest
from azure.core import MatchConditions
from azure.core.exceptions import AzureError
from azure.cosmos.exceptions import CosmosHttpResponseError

_PROJECT_ROOT = str(Path(__file__).resolve().parent.parent)
if _PROJECT_ROOT not in sys.path:
    sys.path.insert(0, _PROJECT_ROOT)

import services.conversation_service as service_module
from models.conversation import (
    ConversationMessage,
    ConversationMessageItem,
    ConversationType,
    ExpertNotificationAttempt,
)
from services.conversation_service import ConversationService

CONVERSATION = "channel;messageid=root"
TYPE = ConversationType.teams_channel
PARTITION = f"{TYPE.value}:{CONVERSATION}"


class CosmosError(CosmosHttpResponseError):
    def __init__(self, status_code):
        super().__init__(status_code=status_code, message="Test storage failure")


def message(message_id="root"):
    return ConversationMessage(
        id=message_id,
        sender_role="user",
        sender_id="author",
        sender_name="Author",
        content="Question",
        created_at=datetime(2026, 9, 18, tzinfo=timezone.utc),
        conversation_id=CONVERSATION,
        conversation_type=TYPE,
    )


def document(message_id):
    return ConversationMessageItem(
        **message(message_id).model_dump(),
        conversation_partition=PARTITION,
    ).model_dump(mode="json")


class Container:
    """Enforce ETags and interleave independent workers without contacting Azure."""

    def __init__(self):
        self.items = {}
        self.version = 0
        self.writes = 0
        self.conflicts = 0
        self.before_replace = None
        self.before_create = None

    def store(self, body):
        self.version += 1
        stored = deepcopy(body)
        stored["_etag"] = str(self.version)
        self.items[stored["id"]] = stored
        self.writes += 1
        return deepcopy(stored)

    async def read_item(self, *, item, partition_key):
        assert partition_key == PARTITION
        await asyncio.sleep(0)
        if item not in self.items:
            raise CosmosError(404)
        return deepcopy(self.items[item])

    async def create_item(self, *, body):
        assert body["conversation_partition"] == PARTITION
        assert body["document_type"] == "conversation_message"
        await asyncio.sleep(0)
        if self.before_create:
            hook, self.before_create = self.before_create, None
            hook(body)
        if body["id"] in self.items:
            self.conflicts += 1
            raise CosmosError(409)
        return self.store(body)

    async def replace_item(self, *, item, body, etag, match_condition):
        assert match_condition == MatchConditions.IfNotModified
        assert body["id"] == item
        assert body["conversation_partition"] == PARTITION
        assert body["document_type"] == "conversation_message"
        await asyncio.sleep(0)
        if self.before_replace:
            hook, self.before_replace = self.before_replace, None
            hook(body)
        if item not in self.items:
            raise CosmosError(404)
        if self.items[item]["_etag"] != etag:
            self.conflicts += 1
            raise CosmosError(412)
        return self.store(body)


@pytest.fixture
def container(monkeypatch):
    result = Container()
    result.store(document("root"))
    result.store(document("trigger"))

    async def get_container():
        return result

    monkeypatch.setattr(service_module, "get_conversation_message_container", get_container)
    return result


async def reserve(response_id="response"):
    return await ConversationService().reserve_expert_notification(
        CONVERSATION, TYPE, response_id
    )


OPERATIONS = [
    pytest.param(lambda service: service.save_conversation(message()), id="save"),
    pytest.param(
        lambda service: service.record_should_reply("root", CONVERSATION, TYPE, True),
        id="should-reply",
    ),
    pytest.param(
        lambda service: service.reserve_expert_notification(CONVERSATION, TYPE, "response"),
        id="reservation",
    ),
]


@pytest.mark.asyncio
async def test_concurrent_reservations_grant_exactly_one_attempt_on_root(container):
    responses = ["one", "two", "three"]
    grants = await asyncio.gather(*(reserve(response) for response in responses))
    assert sum(grants) == 1
    assert container.conflicts == 2
    attempt = container.items["root"]["expert_notification"]
    assert set(attempt) == {"response_id", "reserved_at"}
    assert attempt["response_id"] == responses[grants.index(True)]
    assert datetime.fromisoformat(attempt["reserved_at"]).tzinfo is not None
    assert container.items["trigger"]["expert_notification"] is None
    assert set(container.items) == {"root", "trigger"}


@pytest.mark.asyncio
async def test_attempt_never_expires_or_grants_again_even_same_response(container):
    assert await reserve()
    container.items["root"]["expert_notification"]["reserved_at"] = "2000-01-01T00:00:00Z"
    original = deepcopy(container.items["root"])
    assert not await reserve()
    assert not await reserve("different-response")
    assert container.items["root"] == original


@pytest.mark.asyncio
async def test_missing_root_fails_closed_without_placeholder(container, caplog):
    container.items.pop("root")
    writes = container.writes
    assert not await reserve()
    assert container.writes == writes
    assert "root" not in container.items
    assert "not found" in caplog.text


@pytest.mark.asyncio
@pytest.mark.parametrize("conversation", [
    "channel", ";messageid=root", "channel;messageid=", "channel;messageid=root;other=x",
])
async def test_invalid_root_identifier_fails_closed(container, conversation, caplog):
    writes = container.writes
    assert not await ConversationService().reserve_expert_notification(
        conversation, TYPE, "response"
    )
    assert container.writes == writes
    assert "Cannot identify root" in caplog.text


@pytest.mark.asyncio
@pytest.mark.parametrize("response", ["", "   "])
async def test_empty_response_cannot_consume_attempt(container, response):
    writes = container.writes
    assert not await reserve(response)
    assert container.writes == writes


@pytest.mark.asyncio
async def test_reingestion_and_should_reply_race_preserve_attempt_and_metadata(container):
    original_time = container.items["root"]["created_at"]
    container.items["root"]["future_metadata"] = {"unknown": True}
    replay = message()
    replay.created_at = datetime(2026, 9, 20, tzinfo=timezone.utc)
    results = await asyncio.gather(
        ConversationService().save_conversation(replay),
        ConversationService().record_should_reply("root", CONVERSATION, TYPE, True),
        reserve(),
    )
    assert results == [True, None, True]
    raw = container.items["root"]
    assert raw["future_metadata"] == {"unknown": True}
    assert raw["expert_notification"]["response_id"] == "response"
    assert raw["should_reply"] is True
    assert raw["created_at"] == original_time
    raw["expert_notification"]["future_field"] = "keep"
    await ConversationService().save_conversation(replay)
    await ConversationService().record_should_reply("root", CONVERSATION, TYPE, False)
    assert container.items["root"]["expert_notification"]["future_field"] == "keep"
    assert not await reserve("replay")


@pytest.mark.asyncio
async def test_ingress_cannot_forge_or_erase_server_attempt(container):
    assert await reserve()
    forged = ConversationMessageItem(
        **message().model_dump(),
        conversation_partition=PARTITION,
        expert_notification=ExpertNotificationAttempt(
            response_id="forged", reserved_at=datetime.now(timezone.utc)
        ),
    )
    await ConversationService().save_conversation(forged)
    assert container.items["root"]["expert_notification"]["response_id"] == "response"
    container.items.pop("root")
    await ConversationService().save_conversation(forged)
    assert container.items["root"]["expert_notification"] is None
    assert "expert_notification" not in ConversationMessage.model_fields
    inbound = ConversationMessage.model_validate(forged.model_dump())
    assert "expert_notification" not in inbound.model_dump()


@pytest.mark.asyncio
async def test_create_conflict_preserves_winners_attempt_timestamp_and_metadata(container):
    container.items.pop("root")
    original = document("root")
    original["future_field"] = "keep"
    original["should_reply"] = True
    original["expert_notification"] = {
        "response_id": "winner",
        "reserved_at": "2026-09-18T00:00:00Z",
    }
    container.before_create = lambda _: container.store(original)
    replay = message()
    replay.created_at = datetime(2026, 9, 20, tzinfo=timezone.utc)
    await ConversationService().save_conversation(replay)
    raw = container.items["root"]
    assert container.conflicts == 1
    assert raw["future_field"] == "keep"
    assert raw["should_reply"] is True
    assert raw["created_at"] == original["created_at"]
    assert raw["expert_notification"] == original["expert_notification"]


@pytest.mark.asyncio
async def test_ambiguous_reservation_never_grants_again(container, caplog):
    def committed_but_timed_out(body):
        container.store(body)
        raise TimeoutError("Response lost after commit")

    container.before_replace = committed_but_timed_out
    assert not await reserve()
    assert "Message storage operation failed" in caplog.text
    assert container.items["root"]["expert_notification"]["response_id"] == "response"
    assert not await reserve()
    assert not await reserve("retry")


@pytest.mark.asyncio
async def test_etag_conflict_rechecks_attempt_before_granting(container):
    def other_worker_reserved(_):
        raw = deepcopy(container.items["root"])
        raw["expert_notification"] = {
            "response_id": "other",
            "reserved_at": "2026-09-18T00:00:00Z",
        }
        container.store(raw)

    container.before_replace = other_worker_reserved
    assert not await reserve()
    assert container.conflicts == 1
    assert container.items["root"]["expert_notification"]["response_id"] == "other"


@pytest.mark.asyncio
@pytest.mark.parametrize("status", [404, 412])
async def test_exhausted_contention_skips_update_after_three_attempts(container, monkeypatch, caplog, status):
    original = deepcopy(container.items["root"])
    conflict = AsyncMock(side_effect=CosmosError(status))
    monkeypatch.setattr(container, "replace_item", conflict)
    assert not await ConversationService()._write_message_document("root", PARTITION, lambda raw: True)
    assert conflict.await_count == 3
    assert "Concurrent updates exhausted after 3 attempts" in caplog.text
    assert container.items["root"] == original


@pytest.mark.asyncio
@pytest.mark.parametrize("existing", [{}, False, {"future": "format"}])
async def test_unrecognized_existing_attempt_fails_closed(container, existing):
    container.items["root"]["expert_notification"] = existing
    assert not await reserve()


@pytest.mark.asyncio
async def test_non_message_document_fails_closed(container):
    container.items["root"]["document_type"] = "conversation_mapping"
    assert not await reserve()


@pytest.mark.asyncio
@pytest.mark.parametrize("operation", OPERATIONS)
async def test_missing_etag_logs_and_skips_update(container, caplog, operation):
    container.items["root"].pop("_etag")
    original = deepcopy(container.items["root"])
    writes = container.writes
    assert not await operation(ConversationService())
    assert container.items["root"] == original
    assert container.writes == writes
    assert "Missing ETag; skipping message update" in caplog.text


@pytest.mark.asyncio
async def test_deletion_during_reservation_does_not_recreate_root(container, caplog):
    writes = container.writes
    container.before_replace = lambda _: container.items.pop("root")
    assert not await reserve()
    assert "root" not in container.items
    assert container.writes == writes
    assert "not found" in caplog.text


@pytest.mark.asyncio
async def test_repeated_ingress_preserves_creation_time_and_server_decision(container):
    original = container.items["root"]["created_at"]
    container.items["root"]["should_reply"] = True
    replay = message()
    replay.created_at = datetime(2026, 9, 20, tzinfo=timezone.utc)
    replay.content = "Edited question"
    replay.should_reply = False
    await ConversationService().save_conversation(replay)
    assert container.items["root"]["created_at"] == original
    assert container.items["root"]["content"] == "Edited question"
    assert container.items["root"]["should_reply"] is True


@pytest.mark.asyncio
@pytest.mark.parametrize("stage", ["container", "read_item", "replace_item"])
@pytest.mark.parametrize("failure", [CosmosError(503), AzureError("Storage unavailable"), TimeoutError("Timed out")])
async def test_storage_failure_logs_and_skips_without_retry(container, monkeypatch, caplog, stage, failure):
    original = deepcopy(container.items["root"])
    fail = AsyncMock(side_effect=failure)
    if stage == "container":
        monkeypatch.setattr(service_module, "get_conversation_message_container", fail)
    else:
        monkeypatch.setattr(container, stage, fail)
    assert not await ConversationService()._write_message_document("root", PARTITION, lambda raw: True)
    fail.assert_awaited_once()
    assert container.items["root"] == original
    assert "skipping update" in caplog.text


@pytest.mark.asyncio
@pytest.mark.parametrize("failure", [CosmosError(503), TimeoutError("Timed out")])
async def test_create_failure_logs_and_skips_without_retry(container, monkeypatch, caplog, failure):
    container.items.pop("root")
    fail = AsyncMock(side_effect=failure)
    monkeypatch.setattr(container, "create_item", fail)
    assert not await ConversationService().save_conversation(message())
    fail.assert_awaited_once()
    assert "root" not in container.items
    assert "Message storage operation failed" in caplog.text


@pytest.mark.asyncio
async def test_create_conflicts_stop_after_three_attempts(container, monkeypatch, caplog):
    container.items.pop("root")
    conflict = AsyncMock(side_effect=CosmosError(409))
    monkeypatch.setattr(container, "create_item", conflict)
    assert not await ConversationService().save_conversation(message())
    assert conflict.await_count == 3
    assert "Concurrent updates exhausted after 3 attempts" in caplog.text
    assert "root" not in container.items


@pytest.mark.asyncio
async def test_storage_initialization_error_logs_and_skips_update(monkeypatch, caplog):
    fail = AsyncMock(side_effect=RuntimeError("Storage is not configured"))
    monkeypatch.setattr(service_module, "get_conversation_message_container", fail)
    assert not await ConversationService()._write_message_document("root", PARTITION, lambda raw: True)
    fail.assert_awaited_once()
    assert "Cannot access message storage" in caplog.text


@pytest.mark.asyncio
async def test_callback_programming_error_is_not_suppressed(container):
    def invalid_update(raw):
        raise ValueError("Invalid callback")

    with pytest.raises(ValueError, match="Invalid callback"):
        await ConversationService()._write_message_document("root", PARTITION, invalid_update)
