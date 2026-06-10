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
from models.knowledge import GraphAnswerResult
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
async def test_ask_knowledge_graph(_graph_tools: GraphKnowledgeTools) -> None:
    query = "What does the TypeSpec JSON Schema emitter do?"

    result = await _graph_tools.ask_knowledge_graph(query=query)

    assert isinstance(result, GraphAnswerResult)
    assert result.answer
    assert result.query == query
    assert len(result.citations) > 0
