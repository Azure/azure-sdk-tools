"""Unit tests for knowledge retrieval tools."""

from __future__ import annotations

import os
import sys
from pathlib import Path
from config.tenant_config import SRC_AZURE_REST_API_SPECS_WIKI, TenantID
import pytest

# Ensure the project root is on sys.path so ``config``, ``tools``, etc. resolve.
_PROJECT_ROOT = str(Path(__file__).resolve().parent.parent)
if _PROJECT_ROOT not in sys.path:
    sys.path.insert(0, _PROJECT_ROOT)

from tools.knowledge_tools import KnowledgeTools


@pytest.mark.asyncio
async def test_search_knowledge_tool() -> None:
    query = "how to solve tsv failure"
    sources = [SRC_AZURE_REST_API_SPECS_WIKI]

    result = await KnowledgeTools().search_knowledge_base(
        queries=[query], sources=sources, tenant_id=TenantID.TYPESPEC_CHANNEL_QA_BOT
    )

    assert len(result.results) > 0
