"""Data models for the chat (QA) scenario.

Aligned with Azure SDK QA Bot backend TypeSpec definitions.
See: tools/sdk-ai-bots/azure-sdk-qa-bot-backend/tsp/model.tsp
"""

from __future__ import annotations

from enum import Enum
from pydantic import BaseModel
from config.tenant_config import TenantID
from models.conversation import ConversationType, Role
from models.knowledge import Reference

# ===== Enumerations =====


class AdditionalInfoType(str, Enum):
    """Types of additional information."""

    Link = "link"
    Image = "image"
    Text = "text"


class ConversationItemType(str, Enum):
    """Item types accepted by the conversations.items.create API."""

    message = "message"


class AgentReferenceType(str, Enum):
    """Type discriminator for an agent_reference body."""

    agent_reference = "agent_reference"


# ===== Message Models =====


class Message(BaseModel):
    """A message in the conversation."""

    role: Role
    content: str
    user_name: str | None = None
    user_id: str | None = None


class ConversationItem(BaseModel):
    """Typed payload for a single conversations.items.create entry."""

    type: ConversationItemType = ConversationItemType.message
    role: Role
    content: str
    user_id: str | None = None
    user_name: str | None = None


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
