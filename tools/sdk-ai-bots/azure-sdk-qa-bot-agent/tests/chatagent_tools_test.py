"""Unit tests for deployed Chat Agent validation tools."""

from __future__ import annotations

from types import SimpleNamespace
from unittest.mock import AsyncMock

import pytest

from models.chat import ChatResponse
from tools.chatagent_tools import ChatAgentTools


@pytest.mark.asyncio
async def test_chat_returns_answer_and_trace_id() -> None:
    chat_service = SimpleNamespace(
        chat=AsyncMock(
            return_value=ChatResponse(
                id="response-1",
                answer="Use the corrected guidance.",
                has_result=True,
                trace_id="trace-1",
            )
        )
    )
    prod_chat_service = SimpleNamespace(chat=AsyncMock())
    result = await ChatAgentTools(
        chat_service, prod_chat_service
    ).chat(
        tenant_id="azure_typespec_authoring",
        question="What should I do?",
    )

    assert result.answer == "Use the corrected guidance."
    assert result.trace_id == "trace-1"
    assert result.model_dump() == {
        "answer": "Use the corrected guidance.",
        "trace_id": "trace-1",
    }
    request = chat_service.chat.await_args.args[0]
    assert request.conversation_id is None
    assert request.with_full_context is False
    assert request.message.content == "What should I do?"


@pytest.mark.asyncio
async def test_chat_routes_to_selected_environment() -> None:
    candidate = SimpleNamespace(
        chat=AsyncMock(
            return_value=ChatResponse(
                id="candidate", answer="candidate", has_result=True
            )
        )
    )
    prod = SimpleNamespace(
        chat=AsyncMock(return_value=ChatResponse(id="prod", answer="prod", has_result=True))
    )
    tools = ChatAgentTools(candidate, prod)

    result = await tools.chat(
        tenant_id="azure_typespec_authoring",
        question="What should I do?",
        target="prod",
    )

    assert result.answer == "prod"
    candidate.chat.assert_not_awaited()
    prod.chat.assert_awaited_once()


@pytest.mark.asyncio
async def test_chat_rejects_unknown_tenant() -> None:
    chat_service = SimpleNamespace(chat=AsyncMock())
    prod_chat_service = SimpleNamespace(chat=AsyncMock())

    with pytest.raises(ValueError, match="Unknown tenant_id"):
        await ChatAgentTools(chat_service, prod_chat_service).chat(
            tenant_id="unknown",
            question="Question",
        )

    chat_service.chat.assert_not_awaited()
    prod_chat_service.chat.assert_not_awaited()