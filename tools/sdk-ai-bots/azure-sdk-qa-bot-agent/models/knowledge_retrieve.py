from pydantic import BaseModel
from config.tenant_config import TenantID
from models.chat import Message
from models.knowledge import Reference

class KnowledgeRetrieveRequest(BaseModel):
    """Request for knowledge retrieval."""

    tenant_id: TenantID
    message: Message
    service_type: str | None = None
    search_mode: str | None = None

class KnowledgeRetrieveResponse(BaseModel):
    """Response from knowledge retrieval."""

    has_result: bool
    knowledge_list: list[Reference] | None = None