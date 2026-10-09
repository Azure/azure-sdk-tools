"""Unit tests for knowledge retrieval tools."""

from __future__ import annotations

import os
import sys
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import AsyncMock

from azure.search.documents.indexes.models import IndexerExecutionStatus
import config.app_config as app_config
from config.tenant_config import (
    SRC_AZURE_REST_API_SPECS_DOCS,
    SRC_AZURE_REST_API_SPECS_WIKI,
    SRC_STATIC_ARM_DOCS,
    SRC_TYPESPEC_AZURE_DOCS,
    TenantID,
)
import pytest

# Ensure the project root is on sys.path so ``config``, ``tools``, etc. resolve.
_PROJECT_ROOT = str(Path(__file__).resolve().parent.parent)
if _PROJECT_ROOT not in sys.path:
    sys.path.insert(0, _PROJECT_ROOT)

import tools.knowledge_tools as knowledge_tools_module
from models.knowledge import KnowledgeChunk
from tools.knowledge_tools import KnowledgeTools
import utils.azure_ai_search as azure_ai_search_module
from utils.azure_ai_search import SearchClient, _raw_chunk_filter
from utils.azure_storage import BlobContent
from utils.knowledge_config import KbTarget, _build_targets


def test_raw_chunk_filter_excludes_wiki_pages() -> None:
    source_filter = "context_id eq 'static_arm_docs'"

    result = _raw_chunk_filter(source_filter)

    assert result == (
        "(context_id eq 'static_arm_docs') and "
        "(page_type eq null or page_type eq '')"
    )


def test_knowledge_chunk_accepts_null_page_type() -> None:
    chunk = KnowledgeChunk.model_validate({"page_type": None})

    assert chunk.page_type == ""


@pytest.mark.asyncio
async def test_list_knowledge_sources_includes_static_sources() -> None:
    result = await KnowledgeTools().list_knowledge_sources(
        tenant_id=TenantID.API_SPEC_REVIEW_BOT.value
    )
    source_names = {source.name for source in result.sources}

    assert SRC_AZURE_REST_API_SPECS_DOCS in source_names
    assert SRC_AZURE_REST_API_SPECS_WIKI in source_names
    assert SRC_STATIC_ARM_DOCS in source_names


@pytest.mark.asyncio
async def test_resolve_kb_source_allows_registered_static_source(monkeypatch) -> None:
    get_kb_targets = AsyncMock(return_value=())
    monkeypatch.setattr(knowledge_tools_module, "get_kb_targets", get_kb_targets)

    result = await KnowledgeTools().resolve_kb_source(folder=SRC_STATIC_ARM_DOCS)

    assert result.resolved is True
    assert result.owner is None
    assert result.repo is None
    assert result.path == SRC_STATIC_ARM_DOCS
    assert result.scope == SRC_STATIC_ARM_DOCS


@pytest.mark.asyncio
async def test_resolve_kb_source_rejects_unknown_source(monkeypatch) -> None:
    get_kb_targets = AsyncMock(return_value=())
    monkeypatch.setattr(knowledge_tools_module, "get_kb_targets", get_kb_targets)

    result = await KnowledgeTools().resolve_kb_source(folder="static_unknown")

    assert result.resolved is False
    assert result.reason == "folder_unmapped_or_non_github"


@pytest.mark.asyncio
async def test_resolve_kb_source_allows_registered_static_qa_source(monkeypatch) -> None:
    get_kb_targets = AsyncMock(return_value=())
    monkeypatch.setattr(knowledge_tools_module, "get_kb_targets", get_kb_targets)

    result = await KnowledgeTools().resolve_kb_source(
        folder="static_api_spec_view_qa"
    )

    assert result.resolved is True
    assert result.path == "static_api_spec_view_qa"
    assert result.scope == "static_api_spec_view_qa"


def test_build_targets_preserves_duplicate_folder_paths() -> None:
    targets = _build_targets(
        {
            "sources": [
                {
                    "repository": {
                        "url": "https://github.com/Azure/azure-sdk-for-net.git",
                        "branch": "main",
                    },
                    "paths": [
                        {
                            "folder": "azure_sdk_for_net_docs",
                            "path": "/doc",
                            "relativeByRepoPath": True,
                        },
                        {
                            "folder": "azure_sdk_for_net_docs",
                            "path": "/sdk/resourcemanager/Azure.ResourceManager/docs",
                            "relativeByRepoPath": True,
                        },
                    ],
                }
            ]
        }
    )

    assert [target.path for target in targets["azure_sdk_for_net_docs"]] == [
        "/doc",
        "/sdk/resourcemanager/Azure.ResourceManager/docs",
    ]
    assert all(
        target.relative_by_repo_path
        for target in targets["azure_sdk_for_net_docs"]
    )


@pytest.mark.asyncio
async def test_resolve_kb_source_uses_blob_path_for_duplicate_folder(
    monkeypatch,
) -> None:
    targets = (
        KbTarget(
            owner="Azure",
            repo="azure-sdk-for-net",
            branch="main",
            path="/doc",
            scope="azure_sdk_for_net_docs",
            relative_by_repo_path=True,
        ),
        KbTarget(
            owner="Azure",
            repo="azure-sdk-for-net",
            branch="main",
            path="/sdk/resourcemanager/Azure.ResourceManager/docs",
            scope="azure_sdk_for_net_docs",
            relative_by_repo_path=True,
        ),
    )
    get_kb_targets = AsyncMock(return_value=targets)
    monkeypatch.setattr(
        knowledge_tools_module,
        "get_kb_targets",
        get_kb_targets,
    )

    result = await KnowledgeTools().resolve_kb_source(
        folder="azure_sdk_for_net_docs",
        blob_path=(
            "azure_sdk_for_net_docs/"
            "doc#DataPlaneCodeGeneration#DeveloperDrivenEvolution.md"
        ),
    )

    assert result.resolved is True
    assert result.path == "/doc"

    arm_result = await KnowledgeTools().resolve_kb_source(
        folder="azure_sdk_for_net_docs",
        blob_path=(
            "azure_sdk_for_net_docs/"
            "sdk#resourcemanager#Azure.ResourceManager#docs#overview.md"
        ),
    )

    assert arm_result.resolved is True
    assert arm_result.path == "/sdk/resourcemanager/Azure.ResourceManager/docs"


@pytest.mark.asyncio
async def test_resolve_kb_source_requires_blob_path_for_duplicate_folder(
    monkeypatch,
) -> None:
    targets = (
        KbTarget(
            owner="Azure",
            repo="azure-sdk-for-net",
            branch="main",
            path="/doc",
            scope="azure_sdk_for_net_docs",
            relative_by_repo_path=True,
        ),
        KbTarget(
            owner="Azure",
            repo="azure-sdk-for-net",
            branch="main",
            path="/sdk/resourcemanager/Azure.ResourceManager/docs",
            scope="azure_sdk_for_net_docs",
            relative_by_repo_path=True,
        ),
    )
    get_kb_targets = AsyncMock(return_value=targets)
    monkeypatch.setattr(
        knowledge_tools_module,
        "get_kb_targets",
        get_kb_targets,
    )

    result = await KnowledgeTools().resolve_kb_source(
        folder="azure_sdk_for_net_docs",
    )

    assert result.resolved is False
    assert result.reason == "blob_path_required_for_ambiguous_folder"


@pytest.mark.asyncio
async def test_resolve_kb_source_returns_ownership_only(monkeypatch) -> None:
    monkeypatch.setattr(
        knowledge_tools_module,
        "get_kb_targets",
        AsyncMock(
            return_value=(
                KbTarget(
                    owner="Azure",
                    repo="typespec-azure",
                    branch="main",
                    path="./website/src/content/docs/docs",
                    scope=SRC_TYPESPEC_AZURE_DOCS,
                ),
            )
        ),
    )

    result = await KnowledgeTools().resolve_kb_source(
        folder=SRC_TYPESPEC_AZURE_DOCS,
        blob_path=f"{SRC_TYPESPEC_AZURE_DOCS}/howtos#arm#resource-operations.md",
    )

    assert result.owner == "Azure"
    assert result.repo == "typespec-azure"
    assert result.branch == "main"
    assert result.path == "./website/src/content/docs/docs"
    assert "upstream_url" not in result.model_fields
    assert "source_url" not in result.model_fields


@pytest.mark.asyncio
async def test_search_knowledge_tool() -> None:
    await app_config.init()
    query = "how to solve tsv failure"
    sources = [SRC_AZURE_REST_API_SPECS_WIKI]

    result = await KnowledgeTools().search_knowledge_base(
        queries=[query], sources=sources, tenant_id=TenantID.TYPESPEC_CHANNEL_QA_BOT
    )

    assert len(result.results) > 0


@pytest.mark.asyncio
async def test_read_knowledge_uses_configured_container(monkeypatch) -> None:
    download = AsyncMock(
        return_value=BlobContent(data=b"# API design", etag='"etag-1"')
    )
    monkeypatch.setattr(
        knowledge_tools_module,
        "cfg",
        lambda key, default="": "configured-knowledge" if key == "STORAGE_KNOWLEDGE_CONTAINER" else default,
    )
    monkeypatch.setattr(knowledge_tools_module, "download_blob", download)

    result = await KnowledgeTools().read_knowledge(
        blob_path="azure-sdk-docs-eng/docs#design#api-design.md"
    )

    assert result.content == "# API design"
    assert result.etag == '"etag-1"'
    download.assert_awaited_once_with(
        "configured-knowledge",
        "azure-sdk-docs-eng/docs#design#api-design.md",
        include_metadata=True,
    )


@pytest.mark.asyncio
async def test_read_knowledge_uses_injected_candidate_storage(monkeypatch) -> None:
    download = AsyncMock(
        return_value=BlobContent(data=b"# Candidate", etag='"etag-dev"')
    )
    candidate_blob_client = SimpleNamespace()
    candidate_settings = lambda key, default="": (
        "dev-knowledge" if key == "STORAGE_KNOWLEDGE_CONTAINER" else default
    )
    monkeypatch.setattr(knowledge_tools_module, "download_blob", download)

    result = await KnowledgeTools(
        settings=candidate_settings,
        blob_client=candidate_blob_client,
    ).read_knowledge(blob_path="azure-sdk-docs-eng/docs#design#api-design.md")

    assert result.content == "# Candidate"
    download.assert_awaited_once_with(
        "dev-knowledge",
        "azure-sdk-docs-eng/docs#design#api-design.md",
        include_metadata=True,
        client=candidate_blob_client,
    )


@pytest.mark.asyncio
async def test_update_knowledge_replaces_exact_content_and_runs_indexer(
    monkeypatch,
) -> None:
    download = AsyncMock(
        return_value=BlobContent(
            data=b"# API design\n\nOld guidance.\n",
            etag='"etag-current"',
        )
    )
    upload = AsyncMock()
    search_client = SimpleNamespace(run_indexer=AsyncMock(return_value="succeeded"))
    monkeypatch.setattr(
        knowledge_tools_module,
        "cfg",
        lambda key, default="": "configured-knowledge" if key == "STORAGE_KNOWLEDGE_CONTAINER" else default,
    )
    monkeypatch.setattr(knowledge_tools_module, "download_blob", download)
    monkeypatch.setattr(knowledge_tools_module, "upload_blob", upload)
    monkeypatch.setattr(knowledge_tools_module, "get_search_client", lambda: search_client)

    result = await KnowledgeTools().update_knowledge(
        blob_path="azure-sdk-docs-eng/docs#design#api-design.md",
        expected_content="Old guidance.",
        replacement_content="Corrected guidance.",
        etag='"etag-1"',
    )

    assert result.status == "updated"
    assert result.indexer_status == "succeeded"
    upload.assert_awaited_once_with(
        "configured-knowledge",
        "azure-sdk-docs-eng/docs#design#api-design.md",
        b"# API design\n\nCorrected guidance.\n",
        etag='"etag-1"',
    )
    search_client.run_indexer.assert_awaited_once_with()


@pytest.mark.asyncio
async def test_run_indexer_waits_until_success(monkeypatch) -> None:
    indexer_client = SimpleNamespace(
        run_indexer=AsyncMock(),
        get_indexer_status=AsyncMock(
            side_effect=[
                SimpleNamespace(
                    last_result=SimpleNamespace(
                        status=IndexerExecutionStatus.IN_PROGRESS
                    )
                ),
                SimpleNamespace(
                    last_result=SimpleNamespace(status=IndexerExecutionStatus.SUCCESS)
                ),
            ]
        ),
    )
    monkeypatch.setattr(azure_ai_search_module.asyncio, "sleep", AsyncMock())
    search_client = SearchClient.__new__(SearchClient)
    search_client._indexer_name = "knowledge-indexer"
    search_client._indexer_client = indexer_client

    result = await search_client.run_indexer()

    assert result == "succeeded"
    indexer_client.run_indexer.assert_awaited_once_with("knowledge-indexer")
    assert indexer_client.get_indexer_status.await_count == 2


@pytest.mark.asyncio
async def test_read_knowledge_rejects_unsafe_blob_path(monkeypatch) -> None:
    download = AsyncMock()
    monkeypatch.setattr(knowledge_tools_module, "download_blob", download)

    with pytest.raises(ValueError, match="normalized relative path"):
        await KnowledgeTools().read_knowledge(
            blob_path="azure-sdk-docs-eng/../secrets.md"
        )

    download.assert_not_awaited()
