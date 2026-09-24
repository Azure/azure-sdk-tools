from types import SimpleNamespace
from unittest.mock import AsyncMock, MagicMock

import pytest

from models.bot_config import BotSettings
from models.intention import IntentionRequest, IntentionResponse
from services.bot_config_service import BotConfigService
from services.intention_service import IntentionService
from expert_notification_policy_test import message


def setup_intention(settings=None):
    service = IntentionService()
    service._bot_config_service.get_bot_settings = AsyncMock(
        return_value=BotSettings.model_validate(
            settings if settings is not None else {"allow_replies_after_humans": True}
        )
    )
    service._conversation_service.has_expert_reply = AsyncMock(return_value=True)
    service._conversation_service.get_messages_by_conversation_id = AsyncMock(
        return_value=[message(), message("expert", "expert"), message("question")]
    )
    service._conversation_service.record_should_reply = AsyncMock()
    req = IntentionRequest(
        conversation_id="channel;messageid=root", conversation_type="teams_channel",
        message={"id": "question", "role": "user", "content": "New question?", "user_id": "author"},
    )
    return service, req


@pytest.mark.asyncio
@pytest.mark.parametrize("settings,allowed", [
    ({}, False),
    ({"show_confidence_label": True}, False),
    ({"allow_notify_experts": True}, False),
    ({"experts": [{"id": "expert", "name": "Expert"}]}, False),
    ({"expert_help_threshold": "low"}, False),
    ({"allow_replies_after_humans": True}, True),
    ({"show_confidence_label": True, "allow_replies_after_humans": True}, True),
])
async def test_only_allow_optin_removes_human_presence_block(settings, allowed):
    service, req = setup_intention(settings)
    service._classify_with_llm = AsyncMock(
        return_value=IntentionResponse(should_respond=True, reason="New question")
    )
    response = await service.classify(req)
    assert response.should_respond == allowed
    if allowed:
        service._classify_with_llm.assert_awaited_once()
        service._conversation_service.has_expert_reply.assert_not_awaited()
    else:
        service._classify_with_llm.assert_not_awaited()


@pytest.mark.asyncio
@pytest.mark.parametrize("human_replied", [False, True])
async def test_configuration_retrieval_failure_uses_legacy_intention(monkeypatch, caplog, human_replied):
    service, req = setup_intention()
    service._bot_config_service = BotConfigService()
    monkeypatch.setattr("services.bot_config_service.download_blob", AsyncMock(side_effect=TimeoutError("Blob unavailable")))
    service._conversation_service.has_expert_reply.return_value = human_replied
    history = [message(), message("question")]
    service._conversation_service.get_messages_by_conversation_id.return_value = history
    service._classify_with_llm = AsyncMock(
        return_value=IntentionResponse(should_respond=True, reason="Question")
    )
    response = await service.classify(req)
    assert response.should_respond is (not human_replied)
    assert "using default settings" in caplog.text
    if human_replied:
        assert response.reason == "expert_already_replied"
        service._classify_with_llm.assert_not_awaited()
    else:
        service._classify_with_llm.assert_awaited_once_with(
            req, history, enhanced_intention_rules_enabled=False
        )


@pytest.mark.asyncio
async def test_configuration_outage_preserves_legacy_classifier_error_fallback(monkeypatch):
    service, req = setup_intention()
    service._bot_config_service = BotConfigService()
    monkeypatch.setattr("services.bot_config_service.download_blob", AsyncMock(side_effect=TimeoutError()))
    service._conversation_service.has_expert_reply.return_value = False
    service._conversation_service.get_messages_by_conversation_id.return_value = [message()]
    client = MagicMock()
    client.chat.completions.create = AsyncMock(side_effect=RuntimeError("Model unavailable"))
    monkeypatch.setattr(
        "services.intention_service.get_project_client",
        lambda: SimpleNamespace(get_openai_client=lambda: client),
    )
    response = await service.classify(req)
    assert response.should_respond
    assert response.reason == "llm_error_default_respond"


@pytest.mark.asyncio
@pytest.mark.parametrize("enhanced_intention_rules_enabled", [True, False])
@pytest.mark.parametrize("failure", ["model_error", "invalid_json", "invalid_schema"])
async def test_classifier_failure_preserves_original_fallback(monkeypatch, caplog, enhanced_intention_rules_enabled, failure):
    service, req = setup_intention()
    client = MagicMock()
    if failure == "model_error":
        client.chat.completions.create = AsyncMock(side_effect=RuntimeError("Model unavailable"))
    else:
        content = "not JSON" if failure == "invalid_json" else '{"reason":"Missing decision"}'
        client.chat.completions.create = AsyncMock(return_value=SimpleNamespace(
            choices=[SimpleNamespace(message=SimpleNamespace(content=content))]
        ))
    monkeypatch.setattr(
        "services.intention_service.get_project_client",
        lambda: SimpleNamespace(get_openai_client=lambda: client),
    )
    response = await service._classify_with_llm(
        req, [], enhanced_intention_rules_enabled
    )
    assert response.should_respond
    assert response.reason == "llm_error_default_respond"
    assert "LLM intention classification failed, defaulting to respond" in caplog.text


@pytest.mark.asyncio
@pytest.mark.parametrize("enhanced,saved,message_id", [
    (False, True, "question"),
    (True, True, "question"),
    (False, False, "question"),
    (True, False, "question"),
    (False, True, None),
    (True, True, ""),
])
async def test_current_message_deduplication_is_independent_of_features(monkeypatch, enhanced, saved, message_id):
    service, req = setup_intention()
    req.message.id = message_id
    req.message.user_name = "Author"
    # Identical text on a different message must remain in the history.
    history = [message("earlier", content=req.message.content)]
    if saved:
        history.append(message("question", content="Previously saved text"))
    create = AsyncMock(return_value=SimpleNamespace(choices=[SimpleNamespace(
        message=SimpleNamespace(content='{"should_respond":true,"reason":"Question"}')
    )]))
    client = SimpleNamespace(chat=SimpleNamespace(completions=SimpleNamespace(create=create)))
    monkeypatch.setattr(
        "services.intention_service.get_project_client",
        lambda: SimpleNamespace(get_openai_client=lambda: client),
    )

    response = await service._classify_with_llm(req, history, enhanced)

    assert response.should_respond
    user_messages = [
        item["content"] for item in create.call_args.kwargs["messages"]
        if item["role"] == "user"
    ]
    history_prefix = "[sender: author; id: author]\n" if enhanced else ""
    current_prefix = "[sender: Author; id: author]\n" if enhanced else ""
    expected = [history_prefix + req.message.content]
    if saved and not message_id:
        expected.append(history_prefix + "Previously saved text")
    expected.append(current_prefix + req.message.content)
    assert user_messages == expected


@pytest.mark.asyncio
async def test_enhanced_classifier_receives_sender_context_once(monkeypatch):
    service, req = setup_intention()
    create = AsyncMock(return_value=SimpleNamespace(choices=[SimpleNamespace(
        message=SimpleNamespace(content='{"should_respond":true,"reason":"New question"}')
    )]))
    client = SimpleNamespace(chat=SimpleNamespace(completions=SimpleNamespace(create=create)))
    monkeypatch.setattr(
        "services.intention_service.get_project_client",
        lambda: SimpleNamespace(get_openai_client=lambda: client),
    )
    await service.classify(req)
    messages = create.call_args.kwargs["messages"]
    assert sum(item["content"].endswith(req.message.content) for item in messages) == 1
    assert "id: author" in messages[-1]["content"]
    assert any("id: expert" in item["content"] for item in messages)


@pytest.mark.asyncio
@pytest.mark.parametrize("settings,enhanced", [
    ({}, False),
    ({"expert_help_threshold": "low"}, False),
    ({"show_confidence_label": True}, True),
    ({"allow_replies_after_humans": True}, True),
    ({"allow_notify_experts": True}, True),
    ({"experts": [{"id": "expert", "name": "Expert"}]}, False),
])
async def test_intention_uses_same_optin_rule_before_humans(settings, enhanced):
    service, req = setup_intention(settings)
    service._conversation_service.has_expert_reply.return_value = False
    history = [message(), message("question")]
    service._conversation_service.get_messages_by_conversation_id.return_value = history
    service._classify_with_llm = AsyncMock(
        return_value=IntentionResponse(should_respond=True, reason="Question")
    )
    assert (await service.classify(req)).should_respond
    service._classify_with_llm.assert_awaited_once_with(
        req, history, enhanced_intention_rules_enabled=enhanced
    )


@pytest.mark.asyncio
@pytest.mark.parametrize("coordinates", [(None, None), (None, "teams_channel"), ("thread", None)])
async def test_non_teams_intention_skips_channel_optins(coordinates):
    service, req = setup_intention()
    req.conversation_id, req.conversation_type = coordinates
    service._classify_with_llm = AsyncMock(
        return_value=IntentionResponse(should_respond=True, reason="Question")
    )
    assert (await service.classify(req)).should_respond
    service._bot_config_service.get_bot_settings.assert_not_awaited()
    service._classify_with_llm.assert_awaited_once_with(
        req, [], enhanced_intention_rules_enabled=False
    )
