"""Unit tests for tool-result offloading."""

from __future__ import annotations

from types import SimpleNamespace

import pytest
from agent_framework import AgentSession, Content, InMemoryAgentFileStore

from tools import TOOL_REGISTRY
from middleware.tool_output_security import (
    ToolOutputSecurityMiddleware,
    _SPOTLIGHT_OPEN,
)
from middleware.tool_output_storage import ToolOutputStorageMiddleware


def _make_context(tool_name: str, result, session: AgentSession | None = None):
    return SimpleNamespace(
        function=SimpleNamespace(name=tool_name),
        result=result,
        session=session,
    )


async def _noop_next() -> None:
    return None


@pytest.mark.asyncio
async def test_content_result_is_normalized_to_text():
    middleware = ToolOutputStorageMiddleware(InMemoryAgentFileStore())
    context = _make_context("github-issue_read", [Content.from_text("result")])

    await middleware.process(context, _noop_next)

    assert context.result == "result"


@pytest.mark.asyncio
async def test_large_result_is_saved_and_replaced():
    store = InMemoryAgentFileStore()
    middleware = ToolOutputStorageMiddleware(store)
    content = "external data\n" * 2_000
    context = _make_context(
        "github-pull_request_read",
        [Content.from_text(content)],
        AgentSession(session_id="test-session"),
    )

    await middleware.process(context, _noop_next)

    saved_files = await store.list_children("test-session")
    assert len(saved_files) == 1
    assert await store.read(f"test-session/{saved_files[0].name}") == content
    assert "Large tool result stored in session memory as `tool-result-" in context.result
    assert f"({len(content):,} characters)" in context.result
    assert content not in context.result


@pytest.mark.asyncio
async def test_native_tool_result_is_unchanged():
    middleware = ToolOutputStorageMiddleware(InMemoryAgentFileStore())
    tool_name = "_unit_test_native_tool"
    TOOL_REGISTRY[tool_name] = object
    try:
        result = [Content.from_text("result")]
        context = _make_context(tool_name, result)
        await middleware.process(context, _noop_next)
        assert context.result is result
    finally:
        TOOL_REGISTRY.pop(tool_name, None)


@pytest.mark.asyncio
async def test_offloaded_result_is_spotlighted():
    security = ToolOutputSecurityMiddleware()
    offload = ToolOutputStorageMiddleware(InMemoryAgentFileStore())
    context = _make_context(
        "github-pull_request_read",
        [Content.from_text("external data\n" * 2_000)],
        AgentSession(session_id="test-session"),
    )

    await security.process(
        context,
        lambda: offload.process(context, _noop_next),
    )

    assert context.result.startswith(_SPOTLIGHT_OPEN)
    assert "Large tool result stored in session memory" in context.result


@pytest.mark.asyncio
async def test_large_results_are_isolated_by_session():
    store = InMemoryAgentFileStore()
    middleware = ToolOutputStorageMiddleware(store)
    content = "external data\n" * 2_000

    first = _make_context(
        "github-issue_read",
        content,
        AgentSession(session_id="first-session"),
    )
    second = _make_context(
        "github-issue_read",
        content,
        AgentSession(session_id="second-session"),
    )

    await middleware.process(first, _noop_next)
    await middleware.process(second, _noop_next)

    assert len(await store.list_children("first-session")) == 1
    assert len(await store.list_children("second-session")) == 1
