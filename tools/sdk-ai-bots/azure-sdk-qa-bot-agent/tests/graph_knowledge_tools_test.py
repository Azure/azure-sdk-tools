from __future__ import annotations

import sys
from pathlib import Path

import pytest

# Ensure the project root is on sys.path so ``config``, ``tools``, etc. resolve.
_PROJECT_ROOT = str(Path(__file__).resolve().parent.parent)
if _PROJECT_ROOT not in sys.path:
    sys.path.insert(0, _PROJECT_ROOT)

from config.tenant_config import TenantID
from tools.graph_knowledge_tools import GraphKnowledgeTools


@pytest.mark.asyncio
async def test_search_graph_knowledge_tool() -> None:
    query = "What does the TypeSpec JSON Schema emitter do?"

    result = await GraphKnowledgeTools().search_knowledge_graph(
        queries=[query], tenant_id=TenantID.TYPESPEC_CHANNEL_QA_BOT
    )

    assert len(result.results) > 0
