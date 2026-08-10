"""Tools for validating remediations against the deployed Chat Agent."""

from __future__ import annotations

from typing import TYPE_CHECKING, Annotated

from pydantic import BaseModel

from config.tenant_config import TenantID
from models.chat import ChatRequest, Message
from models.conversation import Role
from tools import tool

if TYPE_CHECKING:
    from services.chat_service import ChatService


class ValidateAgentResponseResult(BaseModel):
    answer: str
    trace_id: str | None = None


class ChatAgentTools:
    """Tools that exercise the deployed Chat Agent."""

    def __init__(self, chat_service: ChatService | None = None) -> None:
        if chat_service is None:
            from services.chat_service import ChatService

            chat_service = ChatService()
        self._chat_service = chat_service

    @tool
    async def validate_agent_response(
        self,
        *,
        tenant_id: Annotated[
            str,
            "Tenant ID from the original failed conversation, used to route the validation question through the same tenant knowledge sources.",
        ],
        question: Annotated[
            str,
            "The complete original user question to rerun against the deployed dev Chat Agent.",
        ],
    ) -> ValidateAgentResponseResult:
        """Rerun a failed case and return the deployed Chat Agent's evidence."""
        if not question.strip():
            raise ValueError("question must not be empty")
        try:
            tenant = TenantID(tenant_id)
        except ValueError as exc:
            raise ValueError(f"Unknown tenant_id: {tenant_id}") from exc

        response = await self._chat_service.chat(
            ChatRequest(
                tenant_id=tenant,
                message=Message(role=Role.User, content=question),
            )
        )
        return ValidateAgentResponseResult(
            answer=response.answer,
            trace_id=response.trace_id,
        )
