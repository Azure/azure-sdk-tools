"""Data models for the chat (QA) scenario."""

from __future__ import annotations

from pydantic import BaseModel
from models.conversation import ConversationType
from config.tenant_config import TenantID


class ChatRequest(BaseModel):
    """Incoming chat request from the Teams App."""

    tenant_id: TenantID
    conversation_id: str | None = None
    conversation_type: ConversationType | None = None
    message: str


class Reference(BaseModel):
    """A knowledge reference cited in a chat response."""

    title: str
    source: str
    link: str
    content: str = ""
    chunk_id: str = ""
    header1: str = ""
    header2: str = ""
    header3: str = ""


class SearchKnowledgeBaseResult(BaseModel):
    """Output of the search_knowledge_base tool call."""

    results: list[Reference] = []


class ChatResponse(BaseModel):
    """Chat response returned to the caller."""

    answer: str
    agent_conversation_id: str
    references: list[Reference] = []
    routed_tenant: TenantID | None = None
