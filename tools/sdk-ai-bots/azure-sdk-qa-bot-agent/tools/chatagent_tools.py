"""Tools for validating remediations against the deployed Chat Agent."""

from __future__ import annotations

from typing import TYPE_CHECKING, Annotated, Literal

from pydantic import BaseModel

from config.tenant_config import TenantID
from models.chat import ChatRequest, Message
from models.conversation import Role
from tools import tool

if TYPE_CHECKING:
    from services.chat_service import ChatService


class ChatResult(BaseModel):
    answer: str
    trace_id: str | None = None


class ChatAgentTools:
    """Tools that exercise the deployed Chat Agent."""

    def __init__(
        self,
        candidate_chat_service: ChatService,
        prod_chat_service: ChatService,
    ) -> None:
        self._candidate_chat_service = candidate_chat_service
        self._prod_chat_service = prod_chat_service

    @tool
    async def chat(
        self,
        *,
        tenant_id: Annotated[
            str,
            "Tenant ID from the original failed conversation, used to route the validation question through the same tenant knowledge sources.",
        ],
        question: Annotated[
            str,
            "The complete original user question to rerun against the deployed candidate Chat Agent.",
        ],
        target: Annotated[
            Literal["candidate", "prod"],
            "Use 'candidate' only during analysis after updating the candidate knowledge base. Use 'prod' only during validation after the issue is closed.",
        ] = "candidate",
    ) -> ChatResult:
        """Send a question to a deployed Chat Agent and return its evidence."""
        if not question.strip():
            raise ValueError("question must not be empty")
        try:
            tenant = TenantID(tenant_id)
        except ValueError as exc:
            raise ValueError(f"Unknown tenant_id: {tenant_id}") from exc

        chat_service = (
            self._candidate_chat_service
            if target == "candidate"
            else self._prod_chat_service
        )
        response = await chat_service.chat(
            ChatRequest(
                tenant_id=tenant,
                message=Message(role=Role.User, content=question),
            )
        )
        return ChatResult(
            answer=response.answer,
            trace_id=response.trace_id,
        )
