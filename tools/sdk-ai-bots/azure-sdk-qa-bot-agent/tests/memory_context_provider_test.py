"""Tests for MemoryContextProvider — dual-store memory search and update."""

from __future__ import annotations

import sys
from pathlib import Path
from types import SimpleNamespace

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


class _DualFakeMemoryStores:
    """Records search/update calls, keyed by store name, for dual-store tests."""

    def __init__(
        self,
        user_static=None, user_contextual=None,
        tenant_static=None, tenant_contextual=None,
    ) -> None:
        self._data = {
            "user-store": {"static": user_static or [], "contextual": user_contextual or []},
            "tenant-store": {"static": tenant_static or [], "contextual": tenant_contextual or []},
        }
        self.search_calls: list[dict] = []
        self.update_calls: list[dict] = []

    async def search_memories(self, **kwargs):
        self.search_calls.append(kwargs)
        store = kwargs.get("name", "user-store")
        data = self._data.get(store, {"static": [], "contextual": []})
        if "items" not in kwargs:
            return SimpleNamespace(memories=data["static"], search_id=f"static-{store}")
        return SimpleNamespace(memories=data["contextual"], search_id=f"ctx-{store}")

    async def begin_update_memories(self, **kwargs):
        self.update_calls.append(kwargs)
        return SimpleNamespace(update_id=f"update-{kwargs.get('name', 'unknown')}")


class _FakeContext:
    def __init__(self, input_messages, response_messages=None) -> None:
        self.input_messages = input_messages
        self.response = (
            SimpleNamespace(messages=response_messages) if response_messages is not None else None
        )
        self.extended_messages = []

    def extend_messages(self, source, messages):
        self.extended_messages.append((source, messages))


def _patch_stores(monkeypatch, *, user_store="test-memory-store", tenant_store=None, delay=123):
    monkeypatch.setattr(memory_context_provider_module, "get_user_store_name", lambda: user_store)
    monkeypatch.setattr(memory_context_provider_module, "get_tenant_store_name", lambda: tenant_store)
    monkeypatch.setattr(memory_context_provider_module, "get_memory_update_delay", lambda: delay)


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
        "tenant_static_memories": [],
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
# Dual-store
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_dual_store_before_run_injects_separate_sections(monkeypatch) -> None:
    """With both stores configured, memories are injected as separate sections."""
    _patch_stores(monkeypatch, user_store="user-store", tenant_store="tenant-store")
    memory_stores = _DualFakeMemoryStores(
        user_static=[_make_memory("u1", "User prefers Python")],
        user_contextual=[_make_memory("uc1", "User asked about ARM")],
        tenant_static=[_make_memory("t1", "TypeSpec requires @versioning")],
        tenant_contextual=[_make_memory("tc1", "Use @added decorator for new fields")],
    )
    project_client = SimpleNamespace(beta=SimpleNamespace(memory_stores=memory_stores))
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
    assert "## Tenant memories" in injected_text
    assert "User prefers Python" in injected_text
    assert "TypeSpec requires @versioning" in injected_text
    store_names = [c["name"] for c in memory_stores.search_calls]
    assert "user-store" in store_names
    assert "tenant-store" in store_names


@pytest.mark.asyncio
async def test_dual_store_after_run_updates_only_user_store(monkeypatch) -> None:
    """after_run should only update the user store; tenant is handled by ThreadMemoryService."""
    _patch_stores(monkeypatch, user_store="user-store", tenant_store="tenant-store")
    memory_stores = _DualFakeMemoryStores()
    project_client = SimpleNamespace(beta=SimpleNamespace(memory_stores=memory_stores))
    provider = MemoryContextProvider(project_client)

    context = _FakeContext(
        input_messages=[Message("user", ["How do I use TypeSpec?"])],
        response_messages=[Message("assistant", ["TypeSpec is a language for defining APIs."])],
    )
    state = {"user_scope": "user_alice", "tenant_scope": "azure_sdk_onboarding"}
    session = SimpleNamespace(session_id="session-1")

    await provider.after_run(agent=None, session=session, context=context, state=state)

    assert len(memory_stores.update_calls) == 1
    assert memory_stores.update_calls[0]["name"] == "user-store"
    assert memory_stores.update_calls[0]["scope"] == "user_alice"
