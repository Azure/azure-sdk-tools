"""Hermetic mapping storage and lifetime-pinned response-format tests."""

import asyncio
from copy import deepcopy
from types import SimpleNamespace
from unittest.mock import AsyncMock, MagicMock

import httpx
import pytest
from azure.core.exceptions import AzureError
from azure.cosmos.exceptions import CosmosResourceExistsError, CosmosResourceNotFoundError
from openai import NotFoundError

from confidence_chat_test import CONFIDENCE_PROMPT, setup_chat
from models.bot_config import BotSettings
from models.conversation import ConversationMappingItem, ConversationType
from services.chat_service import ChatService
from services.bot_config_service import BotConfigService
from services.conversation_service import ConversationService
from utils.azure_ai_foundry_agent import ConversationBrokenError

THREAD = "channel;messageid=root"
TYPE = ConversationType.teams_channel


def mapping_document(thread=THREAD, agent_id="saved-thread", **extra):
    return {
        "id": thread,
        "customer_conversation_id": thread,
        "conversation_type": TYPE.value,
        "mapping_key": f"{TYPE.value}:{thread}",
        "agent_conversation_id": agent_id,
        "document_type": "conversation_mapping",
        **extra,
    }


class MappingContainer:
    def __init__(self):
        self.items = {}
        self.creates = 0
        self.upserts = 0

    async def read_item(self, *, item, partition_key):
        await asyncio.sleep(0)
        raw = self.items.get(item)
        if raw is None:
            raise CosmosResourceNotFoundError(status_code=404, message="Missing mapping")
        assert raw["mapping_key"] == partition_key
        return deepcopy(raw)

    async def create_item(self, body):
        await asyncio.sleep(0)
        self.creates += 1
        if body["id"] in self.items:
            raise CosmosResourceExistsError(status_code=409, message="Mapping exists")
        self.items[body["id"]] = deepcopy(body)
        return deepcopy(body)

    async def upsert_item(self, body):
        await asyncio.sleep(0)
        self.upserts += 1
        self.items[body["id"]] = deepcopy(body)
        return deepcopy(body)


@pytest.fixture
def mapping_container(monkeypatch):
    container = MappingContainer()
    monkeypatch.setattr(
        "services.conversation_service.get_conversation_mapping_container",
        AsyncMock(return_value=container),
    )
    return container


def setup_resolver(monkeypatch, *, enabled=False):
    service, request, output, invoke = setup_chat(
        monkeypatch, bot_settings={"show_confidence_label": enabled}
    )
    service._resolve_conversation = ChatService._resolve_conversation.__get__(service)
    service._conversation_service = ConversationService()
    client = service._openai_client
    client.conversations.retrieve = AsyncMock()
    counter = 0

    async def create(**kwargs):
        nonlocal counter
        counter += 1
        return SimpleNamespace(id=f"created-{counter}")

    client.conversations.create = AsyncMock(side_effect=create)
    return service, request, output, invoke


def assert_initial_format(items, enabled):
    assert items[0]["role"] == "system"
    assert items[0]["content"].count(CONFIDENCE_PROMPT) == int(enabled)
    assert not any(CONFIDENCE_PROMPT in item.get("content", "") for item in items[1:])


@pytest.mark.asyncio
async def test_old_mapping_defaults_to_plain_and_compatibility_wrapper_returns_id(mapping_container):
    mapping_container.items[THREAD] = mapping_document()
    service = ConversationService()
    saved = await service.get_agent_conversation_mapping(THREAD, TYPE)
    assert isinstance(saved, ConversationMappingItem)
    assert saved.confidence_enabled is False
    assert await service.get_agent_conversation_id(THREAD, TYPE) == "saved-thread"


@pytest.mark.asyncio
@pytest.mark.parametrize("invalid", [None, "false", "true", 0, 1, {}, []])
async def test_malformed_saved_pin_is_rejected(mapping_container, invalid):
    mapping_container.items[THREAD] = mapping_document(confidence_enabled=invalid)
    with pytest.raises(ValueError):
        await ConversationService().get_agent_conversation_mapping(THREAD, TYPE)


@pytest.mark.asyncio
async def test_missing_or_invalid_coordinates_do_not_create_mapping(mapping_container, monkeypatch):
    service = ConversationService()
    assert await service.get_agent_conversation_mapping(THREAD, TYPE) is None
    assert await service.get_agent_conversation_id(THREAD, TYPE) is None
    getter = AsyncMock(side_effect=AssertionError("No container for missing coordinates"))
    monkeypatch.setattr("services.conversation_service.get_conversation_mapping_container", getter)
    for coordinates in (None, ""):
        assert await service.get_agent_conversation_mapping(coordinates, TYPE) is None
        assert await service.save_agent_conversation_mapping(
            coordinates, TYPE, "new-thread", confidence_enabled=True
        ) is None
    getter.assert_not_awaited()
    assert mapping_container.items == {}


@pytest.mark.asyncio
@pytest.mark.parametrize("failure", [AzureError("Storage unavailable"), TimeoutError("Read timed out")])
async def test_storage_read_error_never_becomes_a_new_conversation(mapping_container, monkeypatch, failure):
    service, request, _, _ = setup_resolver(monkeypatch, enabled=True)
    monkeypatch.setattr(mapping_container, "read_item", AsyncMock(side_effect=failure))
    with pytest.raises(type(failure)):
        await service._resolve_conversation(service._openai_client, request)
    service._openai_client.conversations.create.assert_not_awaited()
    assert mapping_container.items == {}


@pytest.mark.asyncio
@pytest.mark.parametrize("enabled", [False, True])
async def test_new_mapping_saves_format_without_initial_messages(mapping_container, monkeypatch, enabled):
    service, request, _, _ = setup_resolver(monkeypatch, enabled=enabled)
    client = service._openai_client
    original_create = mapping_container.create_item

    async def save_created_mapping(body):
        client.conversations.create.assert_awaited_once_with()
        return await original_create(body)

    monkeypatch.setattr(mapping_container, "create_item", save_created_mapping)
    assert await service._resolve_conversation(client, request) == ("created-1", True, enabled)
    assert mapping_container.items[THREAD]["confidence_enabled"] is enabled
    assert mapping_container.upserts == 0
    service._bot_config_service.get_bot_settings.assert_awaited_once_with(
        request.conversation_id, request.channel_id
    )


@pytest.mark.asyncio
async def test_failed_foundry_creation_cannot_leave_a_mapping(mapping_container, monkeypatch):
    service, request, _, _ = setup_resolver(monkeypatch, enabled=True)
    service._openai_client.conversations.create.side_effect = TimeoutError("Create failed")
    with pytest.raises(TimeoutError):
        await service._resolve_conversation(service._openai_client, request)
    assert mapping_container.items == {}
    assert mapping_container.creates == mapping_container.upserts == 0


@pytest.mark.asyncio
@pytest.mark.parametrize("enabled", [False, True])
async def test_missing_mapping_after_creation_conflict_logs_and_continues_chat(mapping_container, monkeypatch, caplog, enabled):
    service, request, output, invoke = setup_resolver(monkeypatch, enabled=enabled)
    create = AsyncMock(
        side_effect=CosmosResourceExistsError(status_code=409, message="Mapping already exists")
    )
    monkeypatch.setattr(mapping_container, "create_item", create)

    result = await service.chat(request)
    await asyncio.sleep(0)

    assert result.agent_conversation_id == "created-1"
    assert result.answer == ("Useful guidance" if enabled else "Plain answer")
    assert (result.confidence is not None) is enabled
    assert invoke.call_args.kwargs["agent_conversation_id"] == "created-1"
    assert_initial_format(invoke.call_args.kwargs["conversation_items"], enabled)
    assert (
        "Conversation mapping was not saved; continuing with new AI Foundry conversation: "
        f"source_conversation_id={THREAD}, agent_conversation_id=created-1"
    ) in caplog.text
    create.assert_awaited_once()
    assert mapping_container.upserts == 0
    assert mapping_container.items == {}
    service._save_bot_answer_to_conversation.assert_awaited_once_with(
        request, output.id, result.answer, "trace"
    )


@pytest.mark.asyncio
@pytest.mark.parametrize("create_only", [False, True])
async def test_mapping_write_failure_is_not_hidden(mapping_container, monkeypatch, create_only):
    method = "create_item" if create_only else "upsert_item"
    monkeypatch.setattr(
        mapping_container, method, AsyncMock(side_effect=AzureError("Write failed"))
    )
    with pytest.raises(AzureError, match="Write failed"):
        await ConversationService().save_agent_conversation_mapping(
            THREAD, TYPE, "new", confidence_enabled=True, create_only=create_only
        )
    assert mapping_container.items == {}


@pytest.mark.asyncio
@pytest.mark.parametrize("saved_pin", [None, False, True])
async def test_existing_mapping_wins_over_current_flags(mapping_container, monkeypatch, saved_pin):
    old = mapping_document()
    if saved_pin is not None:
        old["confidence_enabled"] = saved_pin
    mapping_container.items[THREAD] = old
    service, request, _, _ = setup_resolver(monkeypatch, enabled=not bool(saved_pin))
    client = service._openai_client
    assert await service._resolve_conversation(client, request) == (
        "saved-thread", False, bool(saved_pin),
    )
    client.conversations.retrieve.assert_awaited_once_with("saved-thread")
    client.conversations.create.assert_not_awaited()
    service._bot_config_service.get_bot_settings.assert_awaited_once()
    assert mapping_container.items[THREAD] == old


@pytest.mark.asyncio
async def test_existing_mapping_still_validates_channel_configuration(mapping_container, monkeypatch):
    mapping_container.items[THREAD] = mapping_document(confidence_enabled=True)
    service, request, _, _ = setup_resolver(monkeypatch)
    service._bot_config_service.get_bot_settings.side_effect = ValueError("Invalid config")
    with pytest.raises(ValueError, match="Invalid config"):
        await service._resolve_conversation(service._openai_client, request)
    service._openai_client.conversations.create.assert_not_awaited()


@pytest.mark.asyncio
@pytest.mark.parametrize("saved_pin", [None, False, True])
async def test_settings_outage_allows_completion_without_optins_and_preserves_saved_format(mapping_container, monkeypatch, saved_pin):
    if saved_pin is not None:
        mapping_container.items[THREAD] = mapping_document(confidence_enabled=saved_pin)
    service, request, _, invoke = setup_resolver(monkeypatch, enabled=bool(saved_pin))
    service._bot_config_service = BotConfigService()
    monkeypatch.setattr(
        "services.bot_config_service.download_blob", AsyncMock(side_effect=TimeoutError("Blob unavailable"))
    )
    service._conversation_service.reserve_expert_notification = AsyncMock()

    result = await service.chat(request)
    await asyncio.sleep(0)

    assert result.answer == ("Useful guidance" if saved_pin else "Plain answer")
    assert (result.confidence is not None) is bool(saved_pin)
    assert not result.notify_experts
    invoke.assert_awaited_once()
    assert mapping_container.items[THREAD]["confidence_enabled"] is bool(saved_pin)
    service._conversation_service.reserve_expert_notification.assert_not_awaited()


@pytest.mark.asyncio
@pytest.mark.parametrize("saved_pin", [False, True])
async def test_foundry_404_recreation_preserves_and_initializes_saved_pin(mapping_container, monkeypatch, saved_pin):
    mapping_container.items[THREAD] = mapping_document(confidence_enabled=saved_pin)
    service, request, _, invoke = setup_resolver(monkeypatch, enabled=saved_pin)
    service._bot_config_service.get_bot_settings.return_value = BotSettings(
        show_confidence_label=not saved_pin
    )
    client = service._openai_client
    client.conversations.retrieve.side_effect = NotFoundError(
        "Missing Foundry conversation",
        response=httpx.Response(404, request=httpx.Request("GET", "https://example.com/conversation")),
        body=None,
    )
    result = await service.chat(request)
    await asyncio.sleep(0)
    assert result.agent_conversation_id == "created-1"
    assert (result.confidence is not None) is saved_pin
    client.conversations.create.assert_awaited_once_with()
    assert_initial_format(invoke.call_args.kwargs["conversation_items"], saved_pin)
    assert mapping_container.items[THREAD]["confidence_enabled"] is saved_pin
    assert mapping_container.upserts == 1


@pytest.mark.asyncio
@pytest.mark.parametrize("saved_pin", [False, True])
async def test_broken_conversation_recovery_initializes_same_pin_once(mapping_container, monkeypatch, saved_pin):
    mapping_container.items[THREAD] = mapping_document(confidence_enabled=saved_pin)
    service, request, output, invoke = setup_resolver(monkeypatch, enabled=saved_pin)
    service._bot_config_service.get_bot_settings.return_value = BotSettings(
        show_confidence_label=not saved_pin
    )
    service._list_message_items = AsyncMock(
        return_value=[{"role": "user", "content": "Prior question"}]
    )
    invoke.side_effect = [ConversationBrokenError("Broken"), ("trace", output)]
    result = await service.chat(request)
    await asyncio.sleep(0)
    assert (result.confidence is not None) is saved_pin
    assert result.agent_conversation_id == "created-1"
    service._openai_client.conversations.create.assert_awaited_once_with()
    assert mapping_container.items[THREAD]["confidence_enabled"] is saved_pin
    assert mapping_container.items[THREAD]["agent_conversation_id"] == "created-1"
    service._list_message_items.assert_awaited_once()
    initial, recovered = [call.kwargs["conversation_items"] for call in invoke.call_args_list]
    assert not any(CONFIDENCE_PROMPT in item.get("content", "") for item in initial)
    assert_initial_format(recovered, saved_pin)
    assert "[tenant_context]" in recovered[0]["content"]


@pytest.mark.asyncio
@pytest.mark.parametrize("first_pin", [False, True])
async def test_independent_posts_pin_independently_and_existing_posts_do_not_switch(mapping_container, monkeypatch, first_pin):
    service, request, _, _ = setup_resolver(monkeypatch, enabled=first_pin)
    client = service._openai_client
    first = await service._resolve_conversation(client, request)
    second_request = request.model_copy(update={"conversation_id": "channel;messageid=other"})
    service._bot_config_service.get_bot_settings.return_value = BotSettings(
        show_confidence_label=not first_pin
    )
    second = await service._resolve_conversation(client, second_request)
    repeated_first = await service._resolve_conversation(client, request)
    assert first == ("created-1", True, first_pin)
    assert second == ("created-2", True, not first_pin)
    assert repeated_first == ("created-1", False, first_pin)
    assert client.conversations.create.await_count == 2


@pytest.mark.asyncio
@pytest.mark.parametrize("first_pin", [False, True])
async def test_actual_chat_keeps_format_after_toggle_with_prompt_only_on_first_invocation(mapping_container, monkeypatch, first_pin):
    service, request, _, invoke = setup_resolver(monkeypatch, enabled=first_pin)
    service._build_tenant_system_message = MagicMock(return_value="Tenant instructions")
    service._conversation_service.get_messages_by_conversation_id = AsyncMock(
        side_effect=AssertionError("Ordinary completion must not load a Cosmos transcript")
    )
    first = await service.chat(request)
    service._bot_config_service.get_bot_settings.return_value = BotSettings(
        show_confidence_label=not first_pin
    )
    second = await service.chat(request)
    await asyncio.sleep(0)
    assert (first.confidence is not None) is first_pin
    assert (second.confidence is not None) is first_pin
    assert first.answer == second.answer == ("Useful guidance" if first_pin else "Plain answer")
    assert first.agent_conversation_id == second.agent_conversation_id == "created-1"
    service._openai_client.conversations.create.assert_awaited_once_with()
    first_items, next_items = [call.kwargs["conversation_items"] for call in invoke.call_args_list]
    assert_initial_format(first_items, first_pin)
    assert first_items[0]["content"] == (
        "Tenant instructions" + ("\n\n" + CONFIDENCE_PROMPT if first_pin else "")
    )
    assert [
        item["content"] for item in next_items if item["role"] == "system"
    ] == ["[memory_scope] value=user_author"]
    assert mapping_container.creates == 1
    assert mapping_container.upserts == 0
    service._build_tenant_system_message.assert_called_once_with(request.tenant_id)
    service._conversation_service.get_messages_by_conversation_id.assert_not_awaited()


@pytest.mark.asyncio
@pytest.mark.parametrize("first_pin", [False, True])
async def test_concurrent_initial_resolvers_adopt_winning_conversation_and_format(mapping_container, monkeypatch, caplog, first_pin):
    caplog.set_level("INFO", logger="services.chat_service")
    first_service, request, _, _ = setup_resolver(monkeypatch, enabled=first_pin)
    second_service, _, _, _ = setup_resolver(monkeypatch, enabled=not first_pin)
    first_service._openai_client.conversations.create.return_value = SimpleNamespace(id="first")
    first_service._openai_client.conversations.create.side_effect = None
    second_service._openai_client.conversations.create.return_value = SimpleNamespace(id="second")
    second_service._openai_client.conversations.create.side_effect = None
    results = await asyncio.gather(
        first_service._resolve_conversation(first_service._openai_client, request),
        second_service._resolve_conversation(second_service._openai_client, request),
    )
    assert results == [("first", True, first_pin), ("first", False, first_pin)]
    assert mapping_container.items[THREAD]["agent_conversation_id"] == "first"
    assert mapping_container.items[THREAD]["confidence_enabled"] is first_pin
    first_service._openai_client.conversations.create.assert_awaited_once_with()
    second_service._openai_client.conversations.create.assert_awaited_once_with()
    assert mapping_container.creates == 2
    assert mapping_container.upserts == 0
    assert [
        record.getMessage() for record in caplog.records
        if record.getMessage().startswith("Created new AI Foundry conversation:")
    ] == [
        "Created new AI Foundry conversation: first",
        "Created new AI Foundry conversation: second",
    ]
