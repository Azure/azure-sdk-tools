"""Thread memory service.

For threads with expert replies and sufficient depth, extracts structured
**episodes** — problem-solution pairs with reasoning chains — and stores
them in a dedicated Cosmos DB container for vector-similarity retrieval.

Triggered as a background task whenever ``/conversation/save`` persists
a new message.  The service:
1. Queries the full thread from Cosmos DB.
2. If the thread qualifies, extracts an episode via ``chat.completions``
   and stores it in the ``experience-episodes`` Cosmos DB container.
"""

from __future__ import annotations

import json
import logging
from pathlib import Path

from models.conversation import ConversationMessage, ConversationMessageItem, Role
from models.episode import Episode, EpisodeDocument
from utils.azure_ai_foundry import get_embedding_client, get_openai_client
from utils.azure_cosmosdb import save_episode
from config.app_config import get as cfg

logger = logging.getLogger(__name__)

_EPISODE_EXTRACTION_TEMPERATURE = 0.2

_EPISODE_PROMPT_PATH = (
    Path(__file__).resolve().parent.parent / "prompts" / "episode_extraction.md"
)


class ThreadMemoryService:
    """Extracts structured episodes from conversation threads."""

    def __init__(self) -> None:
        self._episode_prompt: str | None = None

    async def process_thread_update(
        self,
        message: ConversationMessage,
        thread_messages: list[ConversationMessageItem],
    ) -> None:
        """Extract episodes from the thread if it qualifies."""
        tenant_id = (message.tenant_id or "").strip()

        # --- Episode extraction ---
        await self._extract_episode(message, thread_messages, tenant_id)

    # ------------------------------------------------------------------
    # Episode extraction
    # ------------------------------------------------------------------

    async def _extract_episode(
        self,
        message: ConversationMessage,
        thread_messages: list[ConversationMessageItem],
        tenant_id: str,
    ) -> None:
        """Extract a structured episode from the thread if it qualifies.

        Because ``/conversation/save`` is called incrementally (each new
        message), the thread grows over time.  We re-extract when:
        - The thread newly qualifies (first time), OR
        - The latest message is from someone other than the original poster
          or the bot (i.e., a potential expert reply).

        The LLM decides whether the conversation has reached a conclusion.
        If not, it returns ``null`` and we wait for more messages.  If it
        returns an episode, we upsert (deterministic ID replaces previous).
        """
        if not tenant_id:
            return

        raw_messages = [self._item_to_dict(m) for m in thread_messages]

        if not self._qualifies_for_episode(message, raw_messages):
            logger.info(
                "Episode extraction skipped: thread does not qualify "
                "(conversation=%s, messages=%d)",
                message.conversation_id,
                len(thread_messages),
            )
            return

        source_thread_id = message.conversation_id or "unknown"
        current_length = len(thread_messages)

        # Format the thread as a transcript
        formatted = self._format_thread(raw_messages)

        # Call LLM — it returns null if the thread is unresolved
        episode = await self._call_llm(formatted)

        if episode is None:
            logger.info(
                "Episode extraction returned null for thread=%s "
                "(low-value or not yet resolved, messages=%d)",
                source_thread_id,
                current_length,
            )
            return

        # Build document with deterministic ID (upsert replaces previous)
        doc = EpisodeDocument.from_episode(
            episode,
            tenant_id=tenant_id,
            source_thread_id=source_thread_id,
            message_count=current_length,
        )

        # Generate embedding
        doc.embedding = await self._generate_embedding(doc.to_searchable_text())

        # Upsert to Cosmos DB
        try:
            await save_episode(doc.model_dump(mode="json"))
            logger.info(
                "Episode saved: id=%s tenant=%s thread=%s "
                "messages=%d confidence=%.2f",
                doc.id,
                tenant_id,
                source_thread_id,
                current_length,
                doc.confidence,
            )
        except Exception:
            logger.warning(
                "Episode save failed: thread=%s tenant=%s",
                source_thread_id,
                tenant_id,
                exc_info=True,
            )

    async def _call_llm(self, formatted_thread: str) -> Episode | None:
        """Call chat.completions to extract an episode."""
        if self._episode_prompt is None:
            self._episode_prompt = _EPISODE_PROMPT_PATH.read_text(encoding="utf-8")

        model = cfg("MEMORY_AGENT_MODEL", "gpt-4.1")
        openai_client = get_openai_client()

        try:
            response = await openai_client.chat.completions.create(
                model=model,
                messages=[
                    {"role": "system", "content": self._episode_prompt},
                    {"role": "user", "content": formatted_thread},
                ],
                response_format={"type": "json_object"},
                temperature=_EPISODE_EXTRACTION_TEMPERATURE,
            )
        except Exception:
            logger.warning("Episode extraction LLM call failed", exc_info=True)
            return None

        raw = (response.choices[0].message.content or "").strip()
        logger.debug("Episode extraction raw response: %s", raw[:500])
        return self._parse_episode(raw)

    async def _generate_embedding(self, text: str) -> list[float]:
        """Generate a vector embedding for similarity search."""
        model = cfg("MEMORY_STORE_EMBEDDING_MODEL", "text-embedding-3-small")
        client = get_embedding_client()
        try:
            response = await client.embeddings.create(model=model, input=text)
            return response.data[0].embedding
        except Exception:
            logger.warning("Embedding generation failed", exc_info=True)
            return []

    # ------------------------------------------------------------------
    # Quality gate
    # ------------------------------------------------------------------

    @staticmethod
    def _qualifies_for_episode(
        message: ConversationMessage,
        thread_messages: list[dict],
    ) -> bool:
        """Check whether a thread qualifies for episode extraction.

        Qualifies when the latest message is from an expert — someone
        other than the original poster or the bot.
        """
        first_human = next(
            (m for m in thread_messages if m.get("sender_role") == Role.User.value),
            None,
        )
        if first_human is None:
            return False

        author_id = first_human.get("sender_id")

        latest_role = message.sender_role
        is_bot = latest_role == Role.System
        is_poster = message.sender_id == author_id
        if is_bot or is_poster:
            logger.debug(
                "Episode extraction skipped: latest message is from %s "
                "(poster=%s, sender=%s, role=%s)",
                "bot" if is_bot else "original poster",
                author_id,
                message.sender_id,
                latest_role,
            )
            return False

        return True

    # ------------------------------------------------------------------
    # Helpers
    # ------------------------------------------------------------------

    @staticmethod
    def _item_to_dict(item: ConversationMessageItem) -> dict:
        """Convert a ConversationMessageItem to the dict format used by quality gate and formatting."""
        return {
            "sender_role": (
                item.sender_role.value
                if isinstance(item.sender_role, Role)
                else item.sender_role
            ),
            "sender_id": item.sender_id,
            "sender_name": item.sender_name,
            "content": item.content,
            "conversation_id": item.conversation_id,
            "conversation_partition": getattr(item, "conversation_partition", None),
        }

    @staticmethod
    def _format_thread(thread_messages: list[dict]) -> str:
        """Format a thread into a readable transcript for the LLM."""
        lines: list[str] = []
        for msg in thread_messages:
            role = msg.get("sender_role", "unknown")
            name = msg.get("sender_name", "Unknown")
            content = (msg.get("content") or "").strip()
            if not content:
                continue
            label = f"[Bot: {name}]" if role == Role.System.value else f"[{name}]"
            lines.append(f"{label}\n{content}")
        return "\n\n".join(lines)

    @staticmethod
    def _parse_episode(raw: str) -> Episode | None:
        """Parse the LLM JSON response into an Episode or None."""
        try:
            data = json.loads(raw)
        except json.JSONDecodeError:
            logger.warning("Episode extraction returned invalid JSON: %s", raw[:200])
            return None

        if data is None:
            return None

        # The LLM might wrap the episode in a key
        if isinstance(data, dict) and "episode" in data:
            data = data["episode"]
        if data is None:
            return None

        try:
            return Episode.model_validate(data)
        except Exception:
            logger.warning("Episode validation failed: %s", raw[:200], exc_info=True)
            return None
