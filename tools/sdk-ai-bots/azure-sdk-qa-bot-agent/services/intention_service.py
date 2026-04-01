"""Intention classification service.

Hybrid approach:
1. Rule-based pre-filters handle clear-cut cases (zero latency).
2. Lightweight LLM call for ambiguous cases (post author, no expert reply yet).
"""

from __future__ import annotations

import logging
from pathlib import Path

from config.app_config import get as cfg
from models.chat import Message
from models.conversation import Role
from models.intention import IntentionRequest, IntentionResponse
from services.conversation_service import ConversationService
from utils.azure_ai_foundry import get_project_client

logger = logging.getLogger(__name__)

_PROMPTS_DIR = Path(__file__).resolve().parent.parent / "prompts"


def _load_classify_prompt() -> str:
    return (_PROMPTS_DIR / "intention_classify.md").read_text(encoding="utf-8").strip()


class IntentionService:
    """Classifies whether the bot should auto-reply to a Teams channel message."""

    def __init__(self) -> None:
        self._conversation_service = ConversationService()
        self._classify_prompt = _load_classify_prompt()

    async def classify(self, req: IntentionRequest) -> IntentionResponse:
        """Classify the intention of a message.

        Applies rule-based pre-filters first, then falls back to LLM
        classification for ambiguous cases.
        """
        # Rule: Check saved messages — if a non-author user has replied, skip
        if req.conversation_id and req.conversation_type and req.message.user_id:
            has_expert = await self._conversation_service.has_expert_reply(
                conversation_id=req.conversation_id,
                conversation_type=req.conversation_type,
                user_id=req.message.user_id,
            )
            if has_expert:
                return IntentionResponse(
                    should_respond=False,
                    reason="expert_already_replied",
                )

        # Ambiguous case: post author message, no expert reply yet → ask LLM
        return await self._classify_with_llm(req)

    async def _classify_with_llm(self, req: IntentionRequest) -> IntentionResponse:
        """Use a lightweight model to classify message intent."""
        project_client = get_project_client()
        openai_client = project_client.get_openai_client()

        model = cfg("AI_FOUNDRY_AGENT_COMPLETION_MODEL", "gpt-4o-mini")
        reasoning_effort = cfg("AI_FOUNDRY_INTENTION_REASONING_EFFORT", "low")

        try:
            system_msg = Message(role=Role.System, content=self._classify_prompt)
            user_msg = Message(role=Role.User, content=req.message.content)
            response = await openai_client.chat.completions.create(
                model=model,
                messages=[
                    system_msg.model_dump(exclude_none=True),
                    user_msg.model_dump(exclude_none=True),
                ],
                reasoning_effort=reasoning_effort,
                response_format={"type": "json_object"},
            )
            raw = (response.choices[0].message.content or "").strip()
            return IntentionResponse.model_validate_json(raw)
        except Exception:
            logger.exception(
                "LLM intention classification failed, defaulting to respond"
            )
            return IntentionResponse(
                should_respond=True,
                reason="llm_error_default_respond",
            )
