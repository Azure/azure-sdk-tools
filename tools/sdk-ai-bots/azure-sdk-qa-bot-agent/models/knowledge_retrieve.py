from pydantic import BaseModel
from config.tenant_config import TenantID
from models.conversation import ConversationType
from models.chat import Message
from models.knowledge import Reference

class KnowledgeRetrieveRequest(BaseModel):
    """Request for chat completion."""

    tenant_id: TenantID
    conversation_id: str | None = None
    message: Message
    service_type: str | None = None
    search_mode: str | None = None

class KnowledgeRetrieveResponse(BaseModel):
    """Response from knowledge retrieve."""

    has_result: bool
    knowledgeList: list[Reference] | None = None