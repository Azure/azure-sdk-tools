"""Unit tests for knowledge retrieval tools."""

from __future__ import annotations

import os
import sys
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import AsyncMock

from azure.search.documents.indexes.models import IndexerExecutionStatus
from config.tenant_config import (
    SRC_AZURE_REST_API_SPECS_DOCS,
    SRC_AZURE_REST_API_SPECS_WIKI,
    SRC_STATIC_ARM_DOCS,
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
async def test_list_knowledge_sources_excludes_static_sources() -> None:
    result = await KnowledgeTools().list_knowledge_sources(
        tenant_id=TenantID.API_SPEC_REVIEW_BOT.value
    )
    source_names = {source.name for source in result.sources}

    assert SRC_AZURE_REST_API_SPECS_DOCS in source_names
    assert SRC_AZURE_REST_API_SPECS_WIKI in source_names
    assert SRC_STATIC_ARM_DOCS not in source_names
    assert all(not name.startswith("static_") for name in source_names)


@pytest.mark.asyncio
async def test_search_knowledge_tool() -> None:
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
