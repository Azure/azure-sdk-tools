"""Completion and one-attempt notification integration with in-memory Cosmos."""

import asyncio
from types import SimpleNamespace
from unittest.mock import AsyncMock

import httpx
import pytest

from confidence_chat_test import setup_chat
from conversation_notification_storage_test import Container, CosmosError, document, message
from services.chat_service import ChatService
from services.conversation_service import ConversationService


def setup_flow(monkeypatch):
    service, request, output, invoke = setup_chat(monkeypatch)
    container = Container()
    container.store(document("root"))
    monkeypatch.setattr(
        "services.conversation_service.get_conversation_message_container",
        AsyncMock(return_value=container),
    )
    conversations = ConversationService()
    conversations.get_messages_by_conversation_id = AsyncMock(
        side_effect=AssertionError("Ordinary completion must reuse Foundry history")
    )
    service._conversation_service = conversations
    service._save_bot_answer_to_conversation = (
        ChatService._save_bot_answer_to_conversation.__get__(service)
    )
    tasks = []
    monkeypatch.setattr(
        "services.chat_service.BackgroundTaskTracker.instance",
        lambda: SimpleNamespace(track=tasks.append),
    )
    counter = 0

    async def generate(**kwargs):
        nonlocal counter
        counter += 1
        return "trace", SimpleNamespace(
            id=f"resp_{counter}", status="completed",
            output=[], output_text=output.output_text,
        )

    invoke.side_effect = generate
    return service, request, container, tasks


@pytest.mark.asyncio
@pytest.mark.parametrize("failure", [False, True])
async def test_http_message_save_is_best_effort_and_memory_requires_confirmed_write(monkeypatch, caplog, failure):
    monkeypatch.setattr("utils.azure_monitor.configure_metrics", lambda: None)
    import server

    _, _, container, tasks = setup_flow(monkeypatch)
    monkeypatch.setattr(server, "_conversation_service", ConversationService())
    memory_update = AsyncMock()
    monkeypatch.setattr(server, "_update_thread_memory", memory_update)
    if failure:
        container.read_item = AsyncMock(side_effect=CosmosError(503))

    async with httpx.AsyncClient(
        transport=httpx.ASGITransport(app=server.app), base_url="http://test"
    ) as client:
        response = await client.post("/conversation/save", json=message().model_dump(mode="json"))

    assert response.status_code == 200
    assert response.json() == {}
    if not failure:
        assert len(tasks) == 1
        await asyncio.gather(*tasks)
        memory_update.assert_awaited_once()
    else:
        assert tasks == []
        memory_update.assert_not_awaited()
        assert "skipping" in caplog.text


@pytest.mark.asyncio
@pytest.mark.parametrize("path", ["/completion", "/agent/chat"])
async def test_http_completion_reserves_attempt_and_saves_generation_history(monkeypatch, path):
    monkeypatch.setattr("utils.azure_monitor.configure_metrics", lambda: None)
    import server

    service, request, container, tasks = setup_flow(monkeypatch)
    monkeypatch.setattr(server, "_chat_service", service)
    async with httpx.AsyncClient(
        transport=httpx.ASGITransport(app=server.app), base_url="http://test"
    ) as client:
        completion = await client.post(path, json=request.model_dump(mode="json"))
        assert completion.status_code == 200
        response = completion.json()
        assert response["notify_experts"] is True
        assert response["answer"] == "Useful guidance"
        assert response["confidence"]["level"] == "medium"
        root = container.items["root"]
        assert set(root["expert_notification"]) == {"response_id", "reserved_at"}
        assert root["expert_notification"]["response_id"] == response["id"]
        await asyncio.gather(*tasks)
        bot = container.items[f"bot-{response['id']}"]
        assert bot["content"] == response["answer"]
        assert bot["trace_id"] == response["trace_id"]
        assert bot["sender_role"] == "system"
        assert set(container.items) == {"root", f"bot-{response['id']}"}

    schemas = server.app.openapi()["components"]["schemas"]
    properties = schemas["ChatResponse"]["properties"]
    assert {"notify_experts", "confidence"} <= properties.keys()
    settings = schemas["BotSettings"]["properties"]
    assert {"show_confidence_label", "allow_notify_experts", "experts"} <= settings.keys()
    assert settings["show_confidence_label"]["default"] is False
    assert settings["allow_replies_after_humans"]["default"] is False
    assert settings["allow_notify_experts"]["default"] is False
    assert "allow_notify_experts" not in properties
    inbound = schemas["ConversationMessage"]["properties"]
    assert "expert_notification" not in inbound


@pytest.mark.asyncio
async def test_lost_response_consumes_attempt_but_answer_retry_is_not_deduplicated(monkeypatch):
    service, request, container, tasks = setup_flow(monkeypatch)
    lost_response = await service.chat(request)
    await asyncio.gather(*tasks)
    # No acknowledgement or status update occurs, including across a restart.
    service._conversation_service.reserve_expert_notification = (
        ConversationService().reserve_expert_notification
    )
    retry = await service.chat(request)
    await asyncio.gather(*tasks)
    assert lost_response.answer == retry.answer == "Useful guidance"
    assert lost_response.notify_experts
    assert not retry.notify_experts
    assert container.items["root"]["expert_notification"]["response_id"] == lost_response.id
    assert set(container.items) == {"root", f"bot-{lost_response.id}", f"bot-{retry.id}"}


@pytest.mark.asyncio
async def test_concurrent_questions_share_one_notification_but_both_answers_are_saved(monkeypatch):
    service, request, container, tasks = setup_flow(monkeypatch)
    container.store(document("follow-up"))
    follow_up = request.model_copy(deep=True)
    follow_up.message.id = "follow-up"
    responses = await asyncio.gather(
        service.chat(request),
        service.chat(follow_up),
    )
    await asyncio.gather(*tasks)
    assert all(response.answer == "Useful guidance" for response in responses)
    assert sum(response.notify_experts for response in responses) == 1
    winner = next(response for response in responses if response.notify_experts)
    assert container.items["root"]["expert_notification"]["response_id"] == winner.id
    assert all(f"bot-{response.id}" in container.items for response in responses)
    assert container.items["follow-up"]["expert_notification"] is None


@pytest.mark.asyncio
async def test_missing_root_and_source_still_allow_answer_without_notification(monkeypatch):
    service, request, container, tasks = setup_flow(monkeypatch)
    container.items.pop("root")
    request.message.id = None
    response = await service.chat(request)
    await asyncio.gather(*tasks)
    assert response.answer == "Useful guidance"
    assert not response.notify_experts
    assert "root" not in container.items
    assert f"bot-{response.id}" in container.items


@pytest.mark.asyncio
async def test_existing_human_history_does_not_suppress_completion(monkeypatch):
    service, request, container, tasks = setup_flow(monkeypatch)
    expert = document("expert")
    expert["sender_id"] = "expert"
    container.store(expert)
    response = await service.chat(request)
    await asyncio.gather(*tasks)
    assert response.answer == "Useful guidance"
    assert response.notify_experts
    assert set(container.items) == {"root", "expert", f"bot-{response.id}"}
    service._conversation_service.get_messages_by_conversation_id.assert_not_awaited()


@pytest.mark.asyncio
async def test_history_failure_does_not_release_notification_attempt(monkeypatch):
    service, request, container, tasks = setup_flow(monkeypatch)
    service._conversation_service.save_conversation = AsyncMock(side_effect=TimeoutError())
    response = await service.chat(request)
    await asyncio.gather(*tasks)
    assert response.answer == "Useful guidance"
    assert response.notify_experts
    retry = await service.chat(request)
    await asyncio.gather(*tasks)
    assert retry.answer == "Useful guidance"
    assert not retry.notify_experts
    assert set(container.items) == {"root"}


@pytest.mark.asyncio
async def test_ambiguous_reservation_saves_answer_without_retrying_attempt(monkeypatch):
    service, request, container, tasks = setup_flow(monkeypatch)

    def committed_but_timed_out(body):
        container.store(body)
        raise TimeoutError("Response lost after commit")

    container.before_replace = committed_but_timed_out
    response = await service.chat(request)
    await asyncio.gather(*tasks)
    assert response.answer == "Useful guidance"
    assert not response.notify_experts
    assert container.items["root"]["expert_notification"]["response_id"] == response.id
    assert f"bot-{response.id}" in container.items
    retry = await service.chat(request)
    await asyncio.gather(*tasks)
    assert retry.answer == "Useful guidance"
    assert not retry.notify_experts
    assert container.items["root"]["expert_notification"]["response_id"] == response.id
    assert f"bot-{retry.id}" in container.items
