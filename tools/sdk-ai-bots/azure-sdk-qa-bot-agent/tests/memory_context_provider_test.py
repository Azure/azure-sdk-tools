"""Tests for MemoryContextProvider — memory search and update."""

from __future__ import annotations

import sys
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import AsyncMock

import pytest
from agent_framework import Message

_PROJECT_ROOT = str(Path(__file__).resolve().parent.parent)
if _PROJECT_ROOT not in sys.path:
    sys.path.insert(0, _PROJECT_ROOT)

import utils.memory_context_provider as memory_context_provider_module
from utils.memory_context_provider import MemoryContextProvider


def _make_memory(mid: str, content: str):
    return SimpleNamespace(memory_item=SimpleNamespace(memory_id=mid, content=content))


class _FakeMemoryStores:
    """Records search/update calls and returns canned responses."""

    def __init__(self, contextual_memories=None, static_memories=None) -> None:
        self._contextual_memories = contextual_memories or []
        self._static_memories = static_memories or []
        self.search_calls: list[dict] = []
        self.update_calls: list[dict] = []

    async def search_memories(self, **kwargs):
        self.search_calls.append(kwargs)
        if "items" not in kwargs:
            return SimpleNamespace(
                memories=self._static_memories,
                search_id="static-search-001",
            )
        return SimpleNamespace(
            memories=self._contextual_memories,
            search_id="ctx-search-001",
        )

    async def begin_update_memories(self, **kwargs):
        self.update_calls.append(kwargs)
        return SimpleNamespace(update_id="update-001")


class _FakeContext:
    def __init__(self, input_messages, response_messages=None) -> None:
        self.input_messages = input_messages
        self.response = (
            SimpleNamespace(messages=response_messages) if response_messages is not None else None
        )
        self.extended_messages = []

    def extend_messages(self, source, messages):
        self.extended_messages.append((source, messages))


def _patch_stores(monkeypatch, *, user_store="test-memory-store", delay=123):
    monkeypatch.setattr(memory_context_provider_module, "get_user_store_name", lambda: user_store)
    monkeypatch.setattr(memory_context_provider_module, "get_memory_update_delay", lambda: delay)
    monkeypatch.setattr(memory_context_provider_module, "cfg", lambda key, default="": default)


# ---------------------------------------------------------------------------
# before_run: static + contextual search
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_before_run_fetches_static_memories_on_first_call(monkeypatch) -> None:
    _patch_stores(monkeypatch)
    static_mems = [_make_memory("s1", "User prefers Python")]
    ctx_mems = [_make_memory("c1", "User asked about pipeline failures")]
    memory_stores = _FakeMemoryStores(
        static_memories=static_mems, contextual_memories=ctx_mems
    )
    project_client = SimpleNamespace(beta=SimpleNamespace(memory_stores=memory_stores))
    provider = MemoryContextProvider(project_client)
    context = _FakeContext(
        input_messages=[
            Message("system", ["[memory_scope] value=scope-a"]),
            Message("user", ["What failed in my pipeline?"]),
        ]
    )
    state: dict = {}
    session = SimpleNamespace(session_id="session-1")

    await provider.before_run(agent=None, session=session, context=context, state=state)

    assert len(memory_stores.search_calls) == 2
    assert "items" not in memory_stores.search_calls[0]  # static
    assert "items" in memory_stores.search_calls[1]  # contextual
    assert state["initialized"] is True
    assert state["user_static_memories"] == static_mems
    injected_text = context.extended_messages[0][1][0].text
    assert "User prefers Python" in injected_text
    assert "User asked about pipeline failures" in injected_text
    assert "## User memories" in injected_text


@pytest.mark.asyncio
async def test_before_run_skips_static_fetch_on_subsequent_calls(monkeypatch) -> None:
    _patch_stores(monkeypatch)
    memory_stores = _FakeMemoryStores(contextual_memories=[_make_memory("c1", "context mem")])
    project_client = SimpleNamespace(beta=SimpleNamespace(memory_stores=memory_stores))
    provider = MemoryContextProvider(project_client)
    context = _FakeContext(
        input_messages=[
            Message("system", ["[memory_scope] value=scope-a"]),
            Message("user", ["second question"]),
        ]
    )
    state: dict = {
        "initialized": True,
        "user_static_memories": [_make_memory("s1", "cached static")],
        "user_scope": "scope-a",
    }
    session = SimpleNamespace(session_id="session-1")

    await provider.before_run(agent=None, session=session, context=context, state=state)

    assert len(memory_stores.search_calls) == 1
    assert "items" in memory_stores.search_calls[0]
    injected_text = context.extended_messages[0][1][0].text
    assert "cached static" in injected_text
    assert "context mem" in injected_text


# ---------------------------------------------------------------------------
# after_run: memory update
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_after_run_collects_all_user_and_assistant_messages(monkeypatch) -> None:
    _patch_stores(monkeypatch)
    memory_stores = _FakeMemoryStores()
    project_client = SimpleNamespace(beta=SimpleNamespace(memory_stores=memory_stores))
    provider = MemoryContextProvider(project_client)
    context = _FakeContext(
        input_messages=[
            Message("system", ["[memory_scope] value=scope-a"]),
            Message("user", ["First question"]),
            Message("assistant", ["First answer"]),
            Message("user", ["Follow-up"]),
        ],
        response_messages=[
            Message("assistant", ["Follow-up answer"]),
        ],
    )
    state = {"user_scope": "scope-a"}
    session = SimpleNamespace(session_id="session-2")

    await provider.after_run(agent=None, session=session, context=context, state=state)

    items = memory_stores.update_calls[0]["items"]
    assert len(items) == 4
    assert items[0] == {"type": "message", "role": "user", "content": "First question"}
    assert items[1] == {"type": "message", "role": "assistant", "content": "First answer"}
    assert items[2] == {"type": "message", "role": "user", "content": "Follow-up"}
    assert items[3] == {"type": "message", "role": "assistant", "content": "Follow-up answer"}


# ---------------------------------------------------------------------------
# Deduplication
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_before_run_deduplicates_memories(monkeypatch) -> None:
    _patch_stores(monkeypatch)
    shared = _make_memory("m1", "User works on .NET SDK")
    memory_stores = _FakeMemoryStores(
        static_memories=[shared],
        contextual_memories=[shared, _make_memory("m2", "User prefers concise answers")],
    )
    project_client = SimpleNamespace(beta=SimpleNamespace(memory_stores=memory_stores))
    provider = MemoryContextProvider(project_client)
    context = _FakeContext(
        input_messages=[
            Message("system", ["[memory_scope] value=scope-a"]),
            Message("user", ["What failed in my pipeline?"]),
        ]
    )
    state: dict = {}
    session = SimpleNamespace(session_id="session-1")

    await provider.before_run(agent=None, session=session, context=context, state=state)

    injected_text = context.extended_messages[0][1][0].text
    assert injected_text.count("User works on .NET SDK") == 1
    assert "User prefers concise answers" in injected_text


# ---------------------------------------------------------------------------
# User memories + Episodes
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_before_run_injects_user_memories_and_episodes(monkeypatch) -> None:
    """User memories and episodes are both injected when tenant context is present."""
    _patch_stores(monkeypatch)
    static_mems = [_make_memory("u1", "User prefers Python")]
    ctx_mems = [_make_memory("uc1", "User asked about ARM")]
    memory_stores = _FakeMemoryStores(
        static_memories=static_mems, contextual_memories=ctx_mems
    )
    project_client = SimpleNamespace(beta=SimpleNamespace(memory_stores=memory_stores))

    fake_embedding = [0.1] * 1536
    fake_embedding_response = SimpleNamespace(data=[SimpleNamespace(embedding=fake_embedding)])
    mock_embedding_client = AsyncMock()
    mock_embedding_client.embeddings.create = AsyncMock(return_value=fake_embedding_response)

    fake_episode = {
        "trigger": "tsp-client fails",
        "symptoms": ["error: emitter not found"],
        "reasoning_chain": ["check config", "add emitter"],
        "resolution": "Add emitter to config",
        "key_insight": "Config issues cause generation failures",
        "similarity_score": 0.9,
    }

    monkeypatch.setattr(
        memory_context_provider_module, "get_embedding_client",
        lambda: mock_embedding_client,
    )
    monkeypatch.setattr(
        memory_context_provider_module, "search_episodes_by_vector",
        AsyncMock(return_value=[fake_episode]),
    )

    provider = MemoryContextProvider(project_client)
    context = _FakeContext(
        input_messages=[
            Message("system", ["[tenant_context] original_tenant_id=azure_sdk_onboarding"]),
            Message("system", ["[memory_scope] value=user_alice"]),
            Message("user", ["How do I add a new API version?"]),
        ]
    )
    state: dict = {}
    session = SimpleNamespace(session_id="session-1")

    await provider.before_run(agent=None, session=session, context=context, state=state)

    injected_text = context.extended_messages[0][1][0].text
    assert "## User memories" in injected_text
    assert "User prefers Python" in injected_text


@pytest.mark.asyncio
async def test_after_run_updates_only_user_store(monkeypatch) -> None:
    """after_run should only update the user store."""
    _patch_stores(monkeypatch)
    memory_stores = _FakeMemoryStores()
    project_client = SimpleNamespace(beta=SimpleNamespace(memory_stores=memory_stores))
    provider = MemoryContextProvider(project_client)

    context = _FakeContext(
        input_messages=[Message("user", ["How do I use TypeSpec?"])],
        response_messages=[Message("assistant", ["TypeSpec is a language for defining APIs."])],
    )
    state = {"user_scope": "user_alice"}
    session = SimpleNamespace(session_id="session-1")

    await provider.after_run(agent=None, session=session, context=context, state=state)

    assert len(memory_stores.update_calls) == 1
    assert memory_stores.update_calls[0]["name"] == "test-memory-store"
    assert memory_stores.update_calls[0]["scope"] == "user_alice"
