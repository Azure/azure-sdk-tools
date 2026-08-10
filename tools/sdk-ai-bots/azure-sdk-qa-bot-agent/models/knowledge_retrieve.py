from pydantic import BaseModel
from config.tenant_config import TenantID
from models.knowledge import Reference

class KnowledgeRetrieveRequest(BaseModel):
    """Request for knowledge retrieval."""

    tenant_id: TenantID
    query: str
    user_id: str | None = None
    service_type: str | None = None
    search_mode: str | None = None
    sources: list[str] | None = None
    top_k: int | None = None

class KnowledgeRetrieveResponse(BaseModel):
    """Response from knowledge retrieval."""

    has_result: bool
    knowledge_list: list[Reference] | None = None