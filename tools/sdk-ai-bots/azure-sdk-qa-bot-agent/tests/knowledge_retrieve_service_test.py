"""Unit tests for the knowledge retrieve service."""

from services.knowledge_service import KnowledgeService
from models.chat import Message
from models.knowledge_retrieve import KnowledgeRetrieveRequest
import pytest
import pytest_asyncio

@pytest_asyncio.fixture(scope="module")
async def test_knowledge_retrieve_service() -> None:
    service = KnowledgeService()

    req = KnowledgeRetrieveRequest(
        tenant_id="azure_sdk_qa_bot",
        query: "hello"
    )

    resp = await service.retrieve(req)
    assert resp.has_result is True
    assert len(resp.knowledge_list) > 0