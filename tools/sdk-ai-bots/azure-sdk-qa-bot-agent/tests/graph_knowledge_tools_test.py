from __future__ import annotations

import os
import sys
from pathlib import Path

import pytest
import pytest_asyncio
from dotenv import load_dotenv

# Ensure the project root is on sys.path so ``config``, ``tools``, etc. resolve.
_PROJECT_ROOT = str(Path(__file__).resolve().parent.parent)
if _PROJECT_ROOT not in sys.path:
    sys.path.insert(0, _PROJECT_ROOT)

load_dotenv()

import config.app_config as app_config
from models.knowledge import GraphSearchResult
from tools.graph_knowledge_tools import GraphKnowledgeTools


@pytest_asyncio.fixture(scope="module")
async def _graph_tools() -> GraphKnowledgeTools:
    # Hydrate settings from Azure App Configuration so KnowledgeGraphService
    # picks up STORAGE_GRAPHRAG_OUTPUT_CONTAINER and friends.
    await app_config.init()
    # The bundled GraphRAG settings.yaml uses ${VAR} substitution against
    # os.environ. In production the host injects these env vars from App
    # Config references; for tests we bridge the App Config dict into
    # os.environ ourselves so load_config() can resolve them.
    for key, value in (app_config._settings or {}).items():
        os.environ.setdefault(key, value)
    return GraphKnowledgeTools()


@pytest.mark.asyncio(loop_scope="module")
async def test_search_knowledge_graph(_graph_tools: GraphKnowledgeTools) -> None:
    query = "What does the TypeSpec JSON Schema emitter do?"

    result = await _graph_tools.search_knowledge_graph(query=query)

    assert isinstance(result, GraphSearchResult)
    assert result.query == query
    assert len(result.references) > 0
    # Retrieval-only — each reference should carry a verbatim snippet.
    assert any(ref.snippet for ref in result.references)
    # Source attribution: every reference must report a real KB source
    # (e.g. "typespec_docs"), not the generic "graphrag" fallback. The
    # graph rebuilds source names from the knowledge blob container at
    # load time — if this fails, the title→source preload regressed.
    sources = {ref.source for ref in result.references if ref.source}
    assert "graphrag" not in sources, (
        f"References still attributed to bare 'graphrag': sources={sources}"
    )
    assert sources, "No source attribution returned for any graph reference"
