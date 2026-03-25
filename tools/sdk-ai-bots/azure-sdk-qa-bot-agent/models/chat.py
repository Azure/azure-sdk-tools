"""Data models for the chat (QA) scenario.

Aligned with Azure SDK QA Bot backend TypeSpec definitions.
See: tools/sdk-ai-bots/azure-sdk-qa-bot-backend/tsp/model.tsp
"""

from __future__ import annotations

from enum import Enum
from pydantic import BaseModel
from config.tenant_config import TenantID
from models.conversation import ConversationType
from models.knowledge import Reference

# ===== Enumerations =====

class Role(str, Enum):
    """Message roles in the conversation."""
    User = "user"
    Assistant = "assistant"
    System = "system"


class AdditionalInfoType(str, Enum):
    """Types of additional information."""
    Link = "link"
    Image = "image"
    Text = "text"

# ===== Message Models =====

class Message(BaseModel):
    """A message in the conversation."""
    role: Role
    content: str
    name: str | None = None

class AdditionalInfo(BaseModel):
    """Additional information to provide to the agent."""
    type: AdditionalInfoType
    content: str
    link: str | None = None

# ===== Request/Response Models =====

class ChatRequest(BaseModel):
    """Request for chat completion."""
    tenant_id: TenantID
    conversation_id: str | None = None
    conversation_type: ConversationType | None = None
    message: Message
    with_full_context: bool | None = False
    additional_infos: list[AdditionalInfo] | None = None

class ChatResponse(BaseModel):
    """Response from chat completion."""
    id: str
    answer: str
    references: list[Reference] | None = None
    full_context: str | None = None
    route_tenant: TenantID | None = None
