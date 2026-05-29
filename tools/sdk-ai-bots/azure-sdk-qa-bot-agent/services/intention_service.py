"""Intention classification service.

Hybrid approach:
1. Rule-based pre-filters handle clear-cut cases (zero latency).
2. Lightweight LLM call for ambiguous cases (post author, no expert reply yet).
"""

from __future__ import annotations

import logging
from pathlib import Path
from typing import Sequence, cast

from config.app_config import get as cfg
from models.chat import Message
from models.conversation import ConversationMessageItem, Role
from models.intention import IntentionRequest, IntentionResponse
from openai.types.chat import ChatCompletionMessageParam
from openai.types.shared_params.reasoning_effort import ReasoningEffort
from services.conversation_service import ConversationService
from utils.azure_ai_foundry import get_project_client

logger = logging.getLogger(__name__)

_PROMPTS_DIR = Path(__file__).resolve().parent.parent / "prompts"


def _load_classify_prompt() -> str:
    return (_PROMPTS_DIR / "intention_classify.md").read_text(encoding="utf-8").strip()


def _cfg_or_default(key: str, default: str) -> str:
    try:
        value = cfg(key, default)
        return value if value is not None else default
    except RuntimeError:
        logger.debug(
            "App config is not initialized; using default for %s",
            key,
        )
        return default


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
        history: list[ConversationMessageItem] = []

        if req.message.user_id and req.conversation_id and req.conversation_type:
            if await self._conversation_service.has_expert_reply(
                req.conversation_id,
                req.conversation_type,
                req.message.user_id,
            ):
                return IntentionResponse(
                    should_respond=False,
                    reason="expert_already_replied",
                )

        if req.conversation_id and req.conversation_type:
            history = await self._conversation_service.get_messages_by_conversation(
                req.conversation_id,
                req.conversation_type,
            )

        if req.message.user_id and self._has_expert_reply(history, req.message.user_id):
            return IntentionResponse(
                should_respond=False,
                reason="expert_already_replied",
            )

        # Ambiguous case: post author message, no expert reply yet → ask LLM
        return await self._classify_with_llm(req, history)

    def _has_expert_reply(
        self, history: Sequence[ConversationMessageItem], user_id: str
    ) -> bool:
        return any(
            item.sender_role == Role.User and item.sender_id != user_id
            for item in history
        )

    async def _classify_with_llm(
        self,
        req: IntentionRequest,
        history: Sequence[ConversationMessageItem] | None = None,
    ) -> IntentionResponse:
        """Use a lightweight model to classify message intent.

        When conversation context is available, includes the full thread
        so the model can make a better decision.
        """
        try:
            project_client = get_project_client()
            openai_client = project_client.get_openai_client()

            model = _cfg_or_default("AI_FOUNDRY_AGENT_COMPLETION_MODEL", "gpt-4o-mini")
            reasoning_effort = cast(
                ReasoningEffort,
                _cfg_or_default("AI_FOUNDRY_INTENTION_REASONING_EFFORT", "low"),
            )

            messages: list[ChatCompletionMessageParam] = [
                {"role": "system", "content": self._classify_prompt},
            ]

            # Include conversation history when available
            if history:
                for item in history:
                    if item.sender_role in (Role.Assistant, Role.System):
                        messages.append({"role": "assistant", "content": item.content})
                    else:
                        messages.append({"role": "user", "content": item.content})

            # Append current message (may not be saved yet)
            messages.append({"role": "user", "content": req.message.content})

            response = await openai_client.chat.completions.create(
                model=model,
                messages=messages,
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
