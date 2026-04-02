"""Thread memory service.

Feeds full conversation threads to the tenant memory store so that
expert replies and resolved issues become shared tenant knowledge.

Triggered as a background task whenever ``/conversation/save`` persists
a new message.  The service:
1. Queries the full thread from Cosmos DB.
2. Converts thread messages to memory-update items.
3. Calls ``begin_update_memories`` on the tenant store.

Foundry's AI extraction (guided by ``user_profile_details`` on the tenant
store) decides what's worth storing.  Consolidation handles duplicate /
overlapping knowledge across repeated submissions of the same growing thread.
"""

from __future__ import annotations

import logging

from models.conversation import ConversationMessage, ConversationMessageItem, Role
from utils.azure_ai_foundry import get_project_client
from utils.azure_memory_store import (
    get_memory_update_delay,
    get_tenant_store_name,
    sanitize_scope,
)

logger = logging.getLogger(__name__)

# Minimum thread length before we bother updating tenant memory.
# A single message (just the poster's question) has no expert replies yet.
_MIN_THREAD_LENGTH = 2


class ThreadMemoryService:
    """Submits conversation threads to the tenant memory store."""

    def __init__(self) -> None:
        # In-memory map: conversation_partition → previous_update_id.
        # Lost on restart — acceptable because Foundry handles full
        # re-extraction gracefully.
        self._update_ids: dict[str, str] = {}

    async def process_thread_update(
        self,
        message: ConversationMessage,
        thread_messages: list[ConversationMessageItem],
    ) -> None:
        """Submit a thread to the tenant memory store.

        Args:
            message: The message that triggered this update (used for
                tenant_id resolution and partition key).
            thread_messages: Full thread from Cosmos DB, ordered by
                ``created_at`` ascending.
        """
        tenant_store = get_tenant_store_name()
        tenant_id = (message.tenant_id or "").strip()
        if (
            not tenant_store
            or not tenant_id
            or len(thread_messages) < _MIN_THREAD_LENGTH
        ):
            return

        scope = sanitize_scope(tenant_id)
        if not scope:
            return

        items = self._build_memory_items(thread_messages)
        if not items:
            return

        # Determine conversation key for incremental tracking
        conv_key = message.conversation_id or message.channel_id

        project_client = get_project_client()
        update_kwargs: dict = {
            "name": tenant_store,
            "scope": scope,
            "items": items,
            "update_delay": get_memory_update_delay(),
        }
        prev_id = self._update_ids.get(conv_key)
        if prev_id:
            update_kwargs["previous_update_id"] = prev_id

        try:
            poller = await project_client.beta.memory_stores.begin_update_memories(
                **update_kwargs
            )
            new_id = getattr(poller, "update_id", None)
            if new_id:
                self._update_ids[conv_key] = new_id
            logger.info(
                "Thread memory update submitted: tenant_store=%s scope=%s "
                "thread_len=%d update_id=%s",
                tenant_store,
                scope,
                len(items),
                new_id,
            )
        except Exception:
            logger.warning(
                "Thread memory update failed: tenant_store=%s scope=%s",
                tenant_store,
                scope,
                exc_info=True,
            )

    @staticmethod
    def _build_memory_items(
        thread_messages: list[ConversationMessageItem],
    ) -> list[dict]:
        """Convert thread messages to memory-update conversation items.

        Only human messages (``sender_role=user``) are included — bot
        responses are excluded to avoid storing the bot's own generated
        answers as ground-truth facts in tenant memory.
        """
        items: list[dict] = []
        for msg in thread_messages:
            if msg.sender_role == Role.System:
                continue
            content = (msg.content or "").strip()
            if not content:
                continue
            items.append({"type": "message", "role": "user", "content": content})
        return items
