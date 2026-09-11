"""Offline tests for tenant-scoped code retrieval."""

from __future__ import annotations

import asyncio
from types import SimpleNamespace
from typing import cast
from unittest.mock import AsyncMock

from agent_framework import (
    AgentContext,
    AgentMiddleware,
    FunctionInvocationContext,
    Message,
)
from openai.types.responses import (
    Response as OpenAIResponse,
    ResponseFunctionToolCall,
    ResponseFunctionToolCallOutputItem,
)

from config.tenant_config import (
    TenantID,
    get_all_code_repository_configs,
)
from models.chat import ChatRequest, Message as ChatMessage
from models.code_search import (
    GitRepositoryRef,
    SearchIndexedCodeResult,
)
from models.conversation import Role
from services.chat_service import ChatService
from tools.code_search_tools import CodeSearchTools
from utils.code_search import (
    _Binding,
    CodeSearchClient,
    _Repository,
    _build_filter,
    _rank_and_format,
)
from utils.tenant_context import TenantContextMiddleware, _tenant_from_messages

_URL = "https://github.com/Azure/typespec-azure.git"
_REF = "refs/heads/main"
_COMMIT = "a" * 40


def test_repository_demand_and_tenant_context_are_shared() -> None:
    configs = get_all_code_repository_configs()
    assert configs[TenantID.TYPESPEC_CHANNEL_QA_BOT.value]
    assert (
        _tenant_from_messages(
            [
                Message(
                    "system",
                    [
                        "[tenant_context] "
                        "original_tenant_id=typespec_channel_qa_bot"
                    ],
                )
            ]
        )
        == TenantID.TYPESPEC_CHANNEL_QA_BOT
    )


def test_tenant_context_is_forwarded_before_tool_invocation() -> None:
    middleware = TenantContextMiddleware()
    context = cast(
        AgentContext,
        SimpleNamespace(
            messages=[
                Message(
                    "system",
                    [
                        "[tenant_context] "
                        "original_tenant_id=typespec_channel_qa_bot"
                    ],
                )
            ],
            function_invocation_kwargs={},
        ),
    )
    called = False

    async def call_next() -> None:
        nonlocal called
        called = True
        assert (
            context.function_invocation_kwargs["tenant_id"]
            == TenantID.TYPESPEC_CHANNEL_QA_BOT.value
        )

    assert isinstance(middleware, AgentMiddleware)
    asyncio.run(middleware.process(context, call_next))
    assert called


def test_code_search_accepts_repository_dicts_from_tool_invocation() -> None:
    search = AsyncMock(return_value=SearchIndexedCodeResult())
    client = cast(
        CodeSearchClient,
        SimpleNamespace(search=search),
    )
    tools = CodeSearchTools(client)
    context = cast(
        FunctionInvocationContext,
        SimpleNamespace(
            kwargs={"tenant_id": TenantID.TYPESPEC_CHANNEL_QA_BOT.value}
        ),
    )

    asyncio.run(
        tools.search_indexed_code(
            queries=["ResourceNameParameter"],
            repositories=cast(
                list[GitRepositoryRef],
                [{"git_url": _URL, "git_ref": _REF}],
            ),
            context=context,
        )
    )

    assert search.await_args is not None
    repositories = search.await_args.kwargs["repositories"]
    assert repositories == [GitRepositoryRef(git_url=_URL, git_ref=_REF)]


def test_postprocess_includes_local_code_tool_output_in_full_context() -> None:
    service = ChatService.__new__(ChatService)
    response = cast(
        OpenAIResponse,
        SimpleNamespace(
            id="response-1",
            output_text="Use the implementation.",
            output=[
                ResponseFunctionToolCall(
                    arguments="{}",
                    call_id="call-1",
                    name="search_indexed_code",
                    type="function_call",
                ),
                ResponseFunctionToolCallOutputItem(
                    id="output-1",
                    call_id="call-1",
                    output=(
                        '{"results":[{"git_url":"https://github.com/Azure/typespec-azure.git",'
                        '"git_ref":"refs/heads/main","commit_sha":"'
                        + "a" * 40
                        + '","indexed_at":"2026-01-01T00:00:00Z",'
                        '"path":"packages/example/main.tsp","start_line":1,"end_line":2,'
                        '"language":"typespec","artifact_type":"source",'
                        '"content":"model Example {}","link":"https://example.test/source",'
                        '"score":1.0}],"unavailable_repositories":[]}'
                    ),
                    status="completed",
                    type="function_call_output",
                ),
            ],
        ),
    )
    request = ChatRequest(
        tenant_id=TenantID.TYPESPEC_CHANNEL_QA_BOT,
        message=ChatMessage(
            role=Role.User,
            content="Where is Example defined?",
        ),
        with_full_context=True,
    )

    result = service._postprocess(request, response, None)

    assert result.full_context is not None
    assert "packages/example/main.tsp" in result.full_context
    assert "model Example {}" in result.full_context


def test_filter_intersects_tenant_paths_and_active_generation() -> None:
    repository = _Repository(_URL, _REF, 3, _COMMIT, "2026-01-01T00:00:00+00:00")
    search_filter, metadata, unavailable = _build_filter(
        (_Binding(_URL, _REF, ("packages",)),),
        {(_URL, _REF): repository},
        ("packages/http",),
        ["typespec"],
    )

    assert "valid_from_generation le 3" in search_filter
    assert "path_prefixes/any(p: p eq 'packages/http')" in search_filter
    assert "language eq 'typespec'" in search_filter
    assert metadata[(_URL, _REF)] == repository
    assert unavailable == []


def test_results_include_commit_pinned_links_and_diversity_limits() -> None:
    repository = _Repository(_URL, _REF, 3, _COMMIT, "2026-01-01T00:00:00+00:00")
    documents = [
        {
            "chunk_id": f"chunk-{index}",
            "git_url": _URL,
            "git_ref": _REF,
            "path": "packages/http/main.tsp",
            "language": "typespec",
            "artifact_type": "source",
            "content": f"model Example{index} {{}}",
            "start_line": index,
            "end_line": index + 1,
        }
        for index in range(1, 4)
    ]
    results = _rank_and_format(
        [documents],
        {(_URL, _REF): repository},
        limit=3,
        content_limit=1000,
    )

    assert len(results) == 2
    assert results[0].commit_sha == _COMMIT
    assert results[0].link == (
        f"https://github.com/Azure/typespec-azure/blob/{_COMMIT}/"
        "packages/http/main.tsp#L1-L2"
    )
