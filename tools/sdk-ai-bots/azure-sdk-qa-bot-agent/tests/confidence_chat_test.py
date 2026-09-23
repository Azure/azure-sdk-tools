"""Assessed and plain answers share conversation orchestration and persistence."""

import asyncio
import json
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import AsyncMock, MagicMock

import pytest
from azure.core.exceptions import AzureError

from models.bot_config import BotSettings, ExpertIdentity
from models.chat import AdditionalInfo, AdditionalInfoType, ChatRequest
from services.chat_service import ChatService
from utils.azure_ai_foundry_agent import ConversationBrokenError
from expert_notification_policy_test import assessment


CONFIDENCE_PROMPT = (
    Path(__file__).resolve().parents[1] / "prompts" / "answer_confidence.md"
).read_text(encoding="utf-8")


def setup_chat(monkeypatch, *, level="medium", needs_expert_help=False, bot_settings=None):
    service = ChatService(
        project_client=MagicMock(), openai_client=MagicMock(),
        settings=lambda key, default=None: default,
    )
    service._get_agent = AsyncMock(return_value=SimpleNamespace(name="agent", version="1"))
    service._conversation_service.get_messages_by_conversation_id = AsyncMock(
        side_effect=AssertionError("Ordinary generation must not load a Cosmos transcript")
    )
    service._conversation_service.reserve_expert_notification = AsyncMock(return_value=True)
    service._save_bot_answer_to_conversation = AsyncMock()
    settings = BotSettings.model_validate(bot_settings) if bot_settings is not None else BotSettings(
        show_confidence_label=True,
        allow_replies_after_humans=True,
        allow_notify_experts=True,
        experts=[ExpertIdentity(id="expert", name="Expert")],
    )
    service._resolve_conversation = AsyncMock(
        return_value=("mapped-thread", False, settings.requires_confidence)
    )
    output = SimpleNamespace(
        id="response", status="completed", output=[],
        output_text=(
            assessment(level, needs_expert_help=needs_expert_help).model_dump_json()
            if settings.requires_confidence else "Plain answer"
        ),
    )
    invoke = AsyncMock(return_value=("trace", output))
    monkeypatch.setattr(
        "services.chat_service.HostedAgentClient", lambda _: SimpleNamespace(invoke=invoke)
    )
    service._bot_config_service.get_bot_settings = AsyncMock(return_value=settings)
    request = ChatRequest(
        tenant_id="azure_sdk_qa_bot", conversation_id="channel;messageid=root",
        conversation_type="teams_channel",
        message={"id": "root", "role": "user", "content": "Question", "user_id": "author"},
    )
    return service, request, output, invoke


@pytest.mark.asyncio
async def test_assessed_answer_reuses_mapped_conversation_and_saves(monkeypatch):
    service, req, output, invoke = setup_chat(monkeypatch)
    result = await service.chat(req)
    await asyncio.sleep(0)
    assert result.answer == "Useful guidance"
    assert result.confidence.level == "medium"
    assert result.notify_experts is True
    assert result.agent_conversation_id == "mapped-thread"
    assert invoke.call_args.kwargs["agent_conversation_id"] == "mapped-thread"
    service._resolve_conversation.assert_awaited_once()
    service._conversation_service.get_messages_by_conversation_id.assert_not_awaited()
    service._save_bot_answer_to_conversation.assert_awaited_once_with(
        req, output.id, result.answer, "trace"
    )
    service._conversation_service.reserve_expert_notification.assert_awaited_once_with(
        req.conversation_id, req.conversation_type, result.id
    )


@pytest.mark.asyncio
@pytest.mark.parametrize("level,needs_help,notify", [
    ("low", False, True),
    ("medium", False, True),
    ("high", False, False),
    ("high", True, True),
])
async def test_every_confidence_level_returns_and_saves_the_answer(monkeypatch, level, needs_help, notify):
    service, req, _, _ = setup_chat(monkeypatch, level=level, needs_expert_help=needs_help)
    req.message.id = None
    result = await service.chat(req)
    await asyncio.sleep(0)
    assert result.answer == "Useful guidance"
    assert result.confidence.level == level
    assert result.confidence.needs_expert_help is needs_help
    assert result.notify_experts is notify
    service._save_bot_answer_to_conversation.assert_awaited_once()
    service._conversation_service.get_messages_by_conversation_id.assert_not_awaited()


@pytest.mark.asyncio
@pytest.mark.parametrize("settings", [
    {},
    {"allow_replies_after_humans": True},
])
async def test_settings_without_confidence_flags_keep_plain_generation(monkeypatch, settings):
    service, req, _, invoke = setup_chat(monkeypatch, bot_settings=settings)
    req.message.id = None
    result = await service.chat(req)
    await asyncio.sleep(0)
    assert result.answer == "Plain answer"
    assert result.confidence is None
    assert not result.notify_experts
    assert invoke.call_args.kwargs["agent_conversation_id"] == "mapped-thread"
    service._conversation_service.get_messages_by_conversation_id.assert_not_awaited()
    service._conversation_service.reserve_expert_notification.assert_not_awaited()
    service._save_bot_answer_to_conversation.assert_awaited_once()


@pytest.mark.asyncio
@pytest.mark.parametrize("coordinates", [
    (None, None), (None, "teams_channel"), ("non-teams-conversation", None),
])
async def test_non_teams_and_stateless_keep_original_path(monkeypatch, coordinates):
    service, req, output, invoke = setup_chat(monkeypatch)
    req.conversation_id, req.conversation_type = coordinates
    req.message.id = None
    req.channel_id = "channel"
    service._resolve_conversation.return_value = ("mapped-thread", False, False)
    output.output_text = "Plain answer"
    result = await service.chat(req)
    await asyncio.sleep(0)
    assert result.answer == "Plain answer"
    assert result.confidence is None
    assert not result.notify_experts
    service._bot_config_service.get_bot_settings.assert_not_awaited()
    service._conversation_service.get_messages_by_conversation_id.assert_not_awaited()
    service._conversation_service.reserve_expert_notification.assert_not_awaited()
    assert invoke.call_args.kwargs["agent_conversation_id"] == (
        "mapped-thread" if req.conversation_id else None
    )


@pytest.mark.asyncio
@pytest.mark.parametrize("status,text,expected", [
    ("completed", "Plain guidance", "Plain guidance"),
    ("completed", '{"answer":"Unstructured"}', "Unstructured"),
    ("completed", '{"answer":"Useful guidance","confidence":null}', "Useful guidance"),
    ("completed", '{"answer":"Useful guidance","confidence":{"level":"certain"}}', "Useful guidance"),
    ("completed", '{"answer":"Truncated', '{"answer":"Truncated'),
    ("incomplete", '{"answer":"Truncated', '{"answer":"Truncated'),
    ("completed", '{"answer":123}', '{"answer":123}'),
    ("completed", "[]", "[]"),
    ("completed", "", ""),
    ("completed", None, ""),
])
async def test_invalid_assessment_preserves_available_answer_without_notification(
    monkeypatch, caplog, status, text, expected
):
    service, req, output, _ = setup_chat(monkeypatch)
    output.output_text = text
    output.status = status
    output.error = None
    output.incomplete_details = None
    output.usage = None

    result = await service.chat(req)
    await asyncio.sleep(0)

    assert result.answer == expected
    assert result.has_result is bool(expected.strip())
    assert result.confidence is None
    assert not result.notify_experts
    assert result.trace_id == "trace"
    assert result.agent_conversation_id == "mapped-thread"
    assert "returning available answer without confidence" in caplog.text
    service._bot_config_service.get_bot_settings.assert_not_awaited()
    service._conversation_service.reserve_expert_notification.assert_not_awaited()
    service._save_bot_answer_to_conversation.assert_awaited_once_with(
        req, output.id, expected, "trace"
    )


@pytest.mark.asyncio
async def test_invalid_assessment_keeps_answer_reference_processing(monkeypatch, caplog):
    service, req, output, _ = setup_chat(monkeypatch)
    data = assessment().model_dump()
    data["answer"] = "Useful guidance\n\n**References**\n- [Guide](https://example.com/guide)"
    data["unexpected"] = True
    output.output_text = json.dumps(data)

    result = await service.chat(req)
    await asyncio.sleep(0)

    assert result.answer == "Useful guidance"
    assert result.references[0].link == "https://example.com/guide"
    assert result.confidence is None
    assert not result.notify_experts
    assert "returning available answer without confidence" in caplog.text
    service._conversation_service.reserve_expert_notification.assert_not_awaited()


@pytest.mark.asyncio
async def test_plain_format_preserves_json_answers_without_assessment_parsing(monkeypatch, caplog):
    service, req, output, _ = setup_chat(monkeypatch, bot_settings={})
    output.output_text = '{"answer":"User-requested JSON"}'

    result = await service.chat(req)
    await asyncio.sleep(0)

    assert result.answer == output.output_text
    assert result.confidence is None
    assert "invalid confidence assessment" not in caplog.text


@pytest.mark.asyncio
@pytest.mark.parametrize("confidence_enabled", [False, True])
@pytest.mark.parametrize("status", ["incomplete", "failed", "cancelled"])
async def test_noncompleted_response_logs_and_processes_available_answer(
    monkeypatch, caplog, confidence_enabled, status
):
    service, req, output, _ = setup_chat(
        monkeypatch, bot_settings=None if confidence_enabled else {}
    )
    output.status = status
    output.error = None
    output.incomplete_details = {"reason": "max_output_tokens"}
    output.usage = None

    result = await service.chat(req)
    await asyncio.sleep(0)

    assert "Agent response not completed" in caplog.text
    assert f"status={status}" in caplog.text
    assert "max_output_tokens" in caplog.text
    assert result.answer == ("Useful guidance" if confidence_enabled else "Plain answer")
    assert result.has_result
    assert result.trace_id == "trace"
    assert (result.confidence is not None) is confidence_enabled
    assert result.notify_experts is confidence_enabled
    service._save_bot_answer_to_conversation.assert_awaited_once_with(
        req, output.id, result.answer, "trace"
    )
    if confidence_enabled:
        service._conversation_service.reserve_expert_notification.assert_awaited_once()
    else:
        service._conversation_service.reserve_expert_notification.assert_not_awaited()


@pytest.mark.asyncio
@pytest.mark.parametrize("change", [
    {"experts": []},
    {"allow_notify_experts": False},
    {"show_confidence_label": False, "allow_notify_experts": False, "allow_replies_after_humans": False},
])
async def test_current_notification_optout_never_hides_answer(monkeypatch, change):
    service, req, _, _ = setup_chat(monkeypatch)
    settings = service._bot_config_service.get_bot_settings.return_value
    service._bot_config_service.get_bot_settings.return_value = settings.model_copy(update=change)
    result = await service.chat(req)
    await asyncio.sleep(0)
    assert result.answer == "Useful guidance"
    assert result.confidence is not None
    assert not result.notify_experts
    service._conversation_service.reserve_expert_notification.assert_not_awaited()
    service._save_bot_answer_to_conversation.assert_awaited_once()


@pytest.mark.asyncio
async def test_posthuman_flag_change_does_not_hide_answer(monkeypatch):
    service, req, _, _ = setup_chat(monkeypatch)
    settings = service._bot_config_service.get_bot_settings.return_value
    service._bot_config_service.get_bot_settings.return_value = settings.model_copy(
        update={"allow_replies_after_humans": False}
    )
    result = await service.chat(req)
    await asyncio.sleep(0)
    assert result.answer == "Useful guidance"
    assert result.notify_experts
    service._conversation_service.get_messages_by_conversation_id.assert_not_awaited()
    service._save_bot_answer_to_conversation.assert_awaited_once()


@pytest.mark.asyncio
@pytest.mark.parametrize("needs_help", [False, True])
async def test_current_backend_threshold_or_explicit_need_controls_notification(monkeypatch, needs_help):
    service, req, _, _ = setup_chat(monkeypatch, level="medium", needs_expert_help=needs_help)
    settings = service._bot_config_service.get_bot_settings.return_value
    service._bot_config_service.get_bot_settings.return_value = settings.model_copy(
        update={"expert_help_threshold": "low"}
    )
    result = await service.chat(req)
    await asyncio.sleep(0)
    assert result.answer == "Useful guidance"
    assert result.notify_experts is needs_help
    if needs_help:
        service._conversation_service.reserve_expert_notification.assert_awaited_once()
    else:
        service._conversation_service.reserve_expert_notification.assert_not_awaited()


@pytest.mark.asyncio
@pytest.mark.parametrize("failure", [TimeoutError("lost acknowledgement"), AzureError("Azure unavailable")])
async def test_reservation_failure_logs_returns_and_saves_without_retry(monkeypatch, caplog, failure):
    service, req, _, _ = setup_chat(monkeypatch)
    service._conversation_service.reserve_expert_notification.side_effect = failure
    result = await service.chat(req)
    await asyncio.sleep(0)
    assert result.answer == "Useful guidance"
    assert not result.notify_experts
    service._conversation_service.reserve_expert_notification.assert_awaited_once()
    service._save_bot_answer_to_conversation.assert_awaited_once()
    assert any(
        "expert" in record.getMessage().lower() or "notification" in record.getMessage().lower()
        for record in caplog.records
    )


@pytest.mark.asyncio
@pytest.mark.parametrize("assessed", [False, True])
async def test_shared_context_invocation_and_postprocessing(monkeypatch, assessed):
    service, req, output, invoke = setup_chat(
        monkeypatch, bot_settings={"show_confidence_label": True} if assessed else {}
    )
    service._resolve_conversation.return_value = ("mapped-thread", True, assessed)
    service._build_tenant_system_message = MagicMock(return_value="Tenant instructions")
    attachment = {"role": "user", "content": "Attachment context"}
    service._build_additional_info_items = AsyncMock(return_value=[attachment])
    service._postprocess = MagicMock(wraps=service._postprocess)
    req.additional_infos = [AdditionalInfo(type=AdditionalInfoType.Text, content="Attached text")]
    answer = "Useful guidance\n\n**References**\n- [Guide](https://example.com/guide)"
    if assessed:
        data = assessment().model_dump()
        data["answer"] = answer
        output.output_text = json.dumps(data)
    else:
        output.output_text = answer
    result = await service.chat(req)
    await asyncio.sleep(0)
    args = invoke.call_args.kwargs
    items = args["conversation_items"]
    expected_initial = "Tenant instructions" + ("\n\n" + CONFIDENCE_PROMPT if assessed else "")
    assert items[0]["role"] == "system"
    assert items[0]["content"] == expected_initial
    assert sum(item.get("content") == expected_initial for item in items) == 1
    assert sum(item.get("content") == "[memory_scope] value=user_author" for item in items) == 1
    assert sum(item.get("content") == "Question" for item in items) == 1
    assert items.count(attachment) == 1
    assert args["agent_conversation_id"] == "mapped-thread"
    assert args["agent_session_id"] is None
    assert args["agent_ref"] == {"name": "agent", "version": "1", "type": "agent_reference"}
    service._build_additional_info_items.assert_awaited_once_with(req.additional_infos)
    service._resolve_conversation.assert_awaited_once()
    assert result.answer == "Useful guidance"
    assert result.references[0].link == "https://example.com/guide"
    assert result.trace_id == "trace"
    service._save_bot_answer_to_conversation.assert_awaited_once_with(
        req, output.id, result.answer, "trace"
    )


@pytest.mark.asyncio
async def test_stateless_warm_session_reuse_is_unchanged(monkeypatch):
    service, req, output, invoke = setup_chat(monkeypatch)
    req.conversation_id = None
    req.conversation_type = None
    output.output_text = "Plain answer"
    output.model_extra = {"agent_session_id": "warm-session"}
    await service.chat(req)
    await service.chat(req)
    await asyncio.sleep(0)
    assert [call.kwargs["agent_session_id"] for call in invoke.call_args_list] == [
        None, "warm-session",
    ]
    assert all(call.kwargs["agent_conversation_id"] is None for call in invoke.call_args_list)
    service._resolve_conversation.assert_not_awaited()
    service._bot_config_service.get_bot_settings.assert_not_awaited()


@pytest.mark.asyncio
@pytest.mark.parametrize("assessed", [False, True])
async def test_thread_recovery_passes_existing_pin_and_preserves_attachments(monkeypatch, assessed):
    service, req, output, invoke = setup_chat(
        monkeypatch, bot_settings={"show_confidence_label": True} if assessed else {}
    )
    service._resolve_conversation.return_value = ("broken-thread", False, assessed)
    service._rebuild_conversation_after_failure = AsyncMock(return_value=(
        "replacement-thread", [{"role": "user", "content": "Replayed question"}],
    ))
    service._conversation_service.save_agent_conversation_mapping = AsyncMock()
    service._build_tenant_system_message = MagicMock(return_value="Tenant instructions")
    attachment = {"role": "user", "content": "Attachment context"}
    service._build_additional_info_items = AsyncMock(return_value=[attachment])
    invoke.side_effect = [ConversationBrokenError("broken"), ("trace", output)]
    result = await service.chat(req)
    await asyncio.sleep(0)
    assert invoke.await_count == 2
    initial, recovered = [call.kwargs for call in invoke.call_args_list]
    assert recovered["agent_conversation_id"] == "replacement-thread"
    items = recovered["conversation_items"]
    expected_initial = "Tenant instructions" + ("\n\n" + CONFIDENCE_PROMPT if assessed else "")
    assert items[0]["role"] == "system"
    assert items[0]["content"] == expected_initial
    assert sum(item.get("content") == expected_initial for item in items) == 1
    assert {"role": "user", "content": "Replayed question"} in items
    assert items.count(attachment) == 1
    assert not any(
        CONFIDENCE_PROMPT in item.get("content", "")
        for item in initial["conversation_items"]
    )
    service._rebuild_conversation_after_failure.assert_awaited_once_with(
        req.conversation_id, req.conversation_type, service._openai_client,
    )
    service._conversation_service.save_agent_conversation_mapping.assert_awaited_once_with(
        req.conversation_id, req.conversation_type,
        agent_conversation_id="replacement-thread",
        confidence_enabled=assessed,
    )
    assert result.agent_conversation_id == "replacement-thread"
    service._save_bot_answer_to_conversation.assert_awaited_once()


@pytest.mark.asyncio
async def test_background_history_write_does_not_delay_answer(monkeypatch):
    service, req, _, _ = setup_chat(monkeypatch)
    started, release = asyncio.Event(), asyncio.Event()

    async def blocked_save(*args):
        started.set()
        await release.wait()

    service._save_bot_answer_to_conversation.side_effect = blocked_save
    tasks = []
    monkeypatch.setattr(
        "services.chat_service.BackgroundTaskTracker.instance",
        lambda: SimpleNamespace(track=tasks.append),
    )
    result = await service.chat(req)
    assert result.answer == "Useful guidance"
    await started.wait()
    assert len(tasks) == 1 and not tasks[0].done()
    release.set()
    await asyncio.gather(*tasks)
