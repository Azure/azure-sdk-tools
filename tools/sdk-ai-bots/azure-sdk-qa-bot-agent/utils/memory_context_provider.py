"""Memory context provider for hosted agent runs.

Uses direct Azure AI Foundry Memory Store APIs to retrieve relevant memories
before model invocation and submit memory updates after the response.

Supports:
- **User store**: personal preferences, SDK/language, individual context.
  Scope derived from ``[memory_scope] value=…`` marker.
- **Episode store** (Cosmos DB): expert experience episodes retrieved via
  vector similarity search and injected as an ``## Expert experience`` section.
  Tenant scope derived from ``[tenant_context] original_tenant_id=…`` marker.

Key behaviors aligned with FoundryMemoryProvider best practices:
- Static (user profile) memories fetched once per session on first before_run
- Contextual memories searched every before_run using all input messages
- Incremental search/update tracking via previous_search_id / previous_update_id
- Dynamic scope resolution per request via marker messages
- Expert episodes from Cosmos DB vector search (per turn)
"""

from __future__ import annotations

import logging
import os
import re
from typing import Any

from agent_framework import Message
from agent_framework._sessions import BaseContextProvider
from azure.ai.projects.aio import AIProjectClient
from azure.ai.projects.models import MemorySearchOptions

from config.app_config import get as cfg
from utils.azure_cosmosdb import search_episodes_by_vector
from utils.azure_ai_foundry import get_embedding_client
from utils.azure_memory_store import (
    get_memory_update_delay,
    get_user_store_name,
    sanitize_scope,
)

logger = logging.getLogger(__name__)

_SCOPE_MARKER_RE = re.compile(r"^\[memory_scope\]\s*value=(.+)$", re.IGNORECASE)
_TENANT_CONTEXT_RE = re.compile(
    r"^\[tenant_context\]\s*original_tenant_id=(\S+)", re.IGNORECASE
)


class MemoryContextProvider(BaseContextProvider):
    """Agent context provider that owns memory retrieval and memory updates."""

    def __init__(
        self,
        project_client: AIProjectClient,
    ) -> None:
        super().__init__(source_id="memory_store")
        self._project_client = project_client

        # User store (None if not configured — memory features disabled)
        self._user_store_name = get_user_store_name()
        if not self._user_store_name:
            logger.warning("MEMORY_USER_STORE_NAME not set — user memory search/update disabled")

        self._update_delay = get_memory_update_delay()

        # Tenant memory search toggle (default: enabled)
        self._enable_tenant_memory_search = cfg(
            "ENABLE_TENANT_MEMORY_SEARCH", "true"
        ).lower() == "true"

        # Episode search config
        self._episode_top_k = int(cfg("MEMORY_EPISODE_SEARCH_TOP_K", "3"))
        self._episode_similarity_threshold = float(
            cfg("MEMORY_EPISODE_SIMILARITY_THRESHOLD", "0.5")
        )
        self._episode_embedding_model = cfg(
            "MEMORY_STORE_EMBEDDING_MODEL", "text-embedding-3-small"
        )

        logger.info(
            "MemoryContextProvider initialized: user_store=%s, "
            "update_delay=%d, enable_tenant_memory_search=%s, "
            "episode_top_k=%d, similarity_threshold=%.2f",
            self._user_store_name,
            self._update_delay,
            self._enable_tenant_memory_search,
            self._episode_top_k,
            self._episode_similarity_threshold,
        )

    # ------------------------------------------------------------------
    # before_run — search both stores and inject memories
    # ------------------------------------------------------------------

    async def before_run(self, *, agent, session, context, state: dict[str, Any]) -> None:
        if not self._user_store_name:
            logger.info("before_run skipped: no stores configured")
            return

        user_scope = self._resolve_user_scope(context)
        tenant_scope = self._resolve_tenant_scope(context)
        state["user_scope"] = user_scope
        state["tenant_scope"] = tenant_scope
        logger.info(
            "before_run: session=%s, user_scope=%s, tenant_scope=%s, input_messages=%d",
            session.session_id, user_scope, tenant_scope,
            len(list(context.input_messages or [])),
        )

        if not user_scope and not tenant_scope:
            logger.info("before_run skipped: no scope resolved from input messages")
            return

        # Fetch static (user profile) memories once per session
        if not state.get("initialized"):
            await self._fetch_static_memories(session, user_scope, state)
            state["initialized"] = True

        # Build search items from all non-empty input messages (excluding markers)
        items = self._build_search_items(context.input_messages)
        logger.info("before_run: %d search items built from input messages", len(items))

        if not items:
            memory_text = self._format_all_memories(
                state.get("user_static_memories", []),
            )
            if memory_text:
                context.extend_messages(self, [Message("system", [memory_text])])
            return

        # --- User contextual search ---
        user_ctx_mems: list = []
        if self._user_store_name and user_scope:
            user_ctx_mems = await self._search_contextual(
                self._user_store_name, user_scope, items, state, "user_previous_search_id"
            )

        # Combine user memories
        all_user = list(state.get("user_static_memories", [])) + user_ctx_mems

        # --- Episode search (Cosmos DB vector similarity) ---
        episodes: list[dict] = []
        if tenant_scope and self._enable_tenant_memory_search:
            episodes = await self._search_episodes(context, tenant_scope)
        elif tenant_scope and not self._enable_tenant_memory_search:
            logger.info("Tenant memory search disabled by config")

        memory_text = self._format_all_memories(all_user, episodes)

        if memory_text:
            context.extend_messages(self, [Message("system", [memory_text])])
            logger.info(
                "Injected memory context for session=%s user_scope=%s tenant_scope=%s",
                session.session_id, user_scope, tenant_scope,
            )
        else:
            logger.info(
                "No memory context found for session=%s user_scope=%s tenant_scope=%s",
                session.session_id, user_scope, tenant_scope,
            )

    async def _search_contextual(
        self,
        store_name: str,
        scope: str,
        items: list[dict],
        state: dict[str, Any],
        prev_id_key: str,
    ) -> list:
        """Run a contextual memory search against a single store."""
        search_kwargs: dict[str, Any] = {
            "name": store_name,
            "scope": scope,
            "options": MemorySearchOptions(max_memories=10),
            "items": items,
        }
        prev_search_id = state.get(prev_id_key)
        if prev_search_id:
            search_kwargs["previous_search_id"] = prev_search_id

        logger.info(
            "Contextual search: store=%s, scope=%s, items=%d, prev_search_id=%s",
            store_name, scope, len(items), prev_search_id,
        )

        try:
            result = await self._project_client.beta.memory_stores.search_memories(**search_kwargs)
            if hasattr(result, "search_id") and result.search_id:
                state[prev_id_key] = result.search_id
            mems = list(result.memories or [])
            logger.info(
                "Contextual search result: store=%s, scope=%s, %d memories returned",
                store_name, scope, len(mems),
            )
            return mems
        except Exception:
            logger.warning(
                "Memory search failed for store=%s scope=%s",
                store_name, scope, exc_info=True,
            )
            return []

    # ------------------------------------------------------------------
    # Episode search (Cosmos DB vector similarity)
    # ------------------------------------------------------------------

    async def _search_episodes(self, context, tenant_id: str) -> list[dict]:
        """Search Cosmos DB for expert episodes similar to the user's question."""
        # Extract the latest user message for embedding
        user_text = self._get_latest_user_text(context.input_messages)
        logger.info(
            "Episode search: tenant_id=%s, latest_user_text=%s", tenant_id, user_text
        )
        if not user_text:
            return []

        try:
            embedding = await self._generate_query_embedding(user_text)
        except Exception:
            logger.warning("Episode search skipped: embedding generation failed", exc_info=True)
            return []

        try:
            results = await search_episodes_by_vector(
                tenant_id, embedding, top_k=self._episode_top_k,
            )
        except Exception:
            logger.warning(
                "Episode search failed for tenant=%s", tenant_id, exc_info=True,
            )
            return []

        # Filter by similarity threshold
        filtered = [
            ep for ep in results
            if ep.get("similarity_score", 0) >= self._episode_similarity_threshold
        ]
        for ep in filtered:
            logger.info(
                "Episode matched: id=%s similarity=%.3f",
                ep.get("id", "?"), ep.get("similarity_score", 0),
            )
        return filtered

    async def _generate_query_embedding(self, text: str) -> list[float]:
        """Generate a vector embedding for the user's query."""
        client = get_embedding_client()
        response = await client.embeddings.create(
            model=self._episode_embedding_model, input=text,
        )
        return response.data[0].embedding

    @staticmethod
    def _get_latest_user_text(messages) -> str | None:
        """Extract the latest user message text from input messages."""
        for msg in reversed(list(messages or [])):
            if getattr(msg, "role", None) != "user":
                continue
            text = (getattr(msg, "text", "") or "").strip()
            if text:
                return text
        return None

    # ------------------------------------------------------------------
    # after_run — update user store
    # ------------------------------------------------------------------

    async def after_run(self, *, agent, session, context, state: dict[str, Any]) -> None:
        if not self._user_store_name:
            logger.info("after_run skipped: no user store configured")
            return

        user_scope = state.get("user_scope")
        if not user_scope:
            logger.info("after_run skipped: no user_scope in state for session=%s", session.session_id)
            return

        items = self._build_update_items(context)
        if not items:
            logger.info("after_run skipped: no user/assistant messages to update for session=%s", session.session_id)
            return

        logger.info(
            "after_run: session=%s, user_scope=%s, %d items to update",
            session.session_id, user_scope, len(items),
        )

        # Update user store only
        await self._update_store(
            self._user_store_name, user_scope, items, state,
            "user_previous_update_id", session,
        )

    async def _update_store(
        self,
        store_name: str,
        scope: str,
        items: list[dict],
        state: dict[str, Any],
        prev_id_key: str,
        session,
    ) -> None:
        """Submit a memory update to a single store."""
        logger.info(
            "after_run: submitting %d items to store=%s scope=%s delay=%d",
            len(items), store_name, scope, self._update_delay,
        )

        update_kwargs: dict[str, Any] = {
            "name": store_name,
            "scope": scope,
            "items": items,
            "update_delay": self._update_delay,
        }
        prev_update_id = state.get(prev_id_key)
        if prev_update_id:
            update_kwargs["previous_update_id"] = prev_update_id

        try:
            poller = await self._project_client.beta.memory_stores.begin_update_memories(**update_kwargs)
            if hasattr(poller, "update_id") and poller.update_id:
                state[prev_id_key] = poller.update_id
            logger.info(
                "Memory update submitted for store=%s scope=%s (update_id=%s)",
                store_name, scope, getattr(poller, "update_id", None),
            )
        except Exception:
            logger.warning(
                "Memory update failed for store=%s scope=%s",
                store_name, scope, exc_info=True,
            )

    # ------------------------------------------------------------------
    # Static memory fetch (once per session)
    # ------------------------------------------------------------------

    async def _fetch_static_memories(
        self, session, user_scope: str | None, state: dict[str, Any]
    ) -> None:
        """Fetch user-profile memories (no items/query) from the user store."""
        if self._user_store_name and user_scope:
            state["user_static_memories"] = await self._fetch_static_from_store(
                self._user_store_name, user_scope, session,
            )
        else:
            state["user_static_memories"] = []

    async def _fetch_static_from_store(
        self, store_name: str, scope: str, session,
    ) -> list:
        """Fetch static memories from a single store."""
        try:
            result = await self._project_client.beta.memory_stores.search_memories(
                name=store_name,
                scope=scope,
                options=MemorySearchOptions(max_memories=10),
            )
            mems = result.memories or []
            logger.info(
                "Retrieved %d static memories from store=%s scope=%s session=%s",
                len(mems), store_name, scope, session.session_id,
            )
            return list(mems)
        except Exception:
            logger.warning(
                "Static memory retrieval failed for store=%s scope=%s session=%s",
                store_name, scope, session.session_id, exc_info=True,
            )
            return []

    # ------------------------------------------------------------------
    # Scope resolution
    # ------------------------------------------------------------------

    def _resolve_user_scope(self, context) -> str | None:
        """Extract user scope from ``[memory_scope] value=…`` marker."""
        for msg in reversed(list(context.input_messages or [])):
            if getattr(msg, "role", None) != "system":
                continue
            text = getattr(msg, "text", "") or ""
            match = _SCOPE_MARKER_RE.match(text.strip())
            if match:
                candidate = match.group(1).strip()
                if candidate:
                    scope = sanitize_scope(candidate)
                    logger.info("User scope resolved from marker: %s", scope)
                    return scope
        logger.info("User scope not found in %d input messages", len(list(context.input_messages or [])))
        return None

    def _resolve_tenant_scope(self, context) -> str | None:
        """Extract tenant scope from ``[tenant_context] original_tenant_id=…``.

        Returns the sanitized tenant_id, or ``None`` if not found.
        """
        for msg in reversed(list(context.input_messages or [])):
            if getattr(msg, "role", None) != "system":
                continue
            text = getattr(msg, "text", "") or ""
            match = _TENANT_CONTEXT_RE.match(text.strip())
            if match:
                raw_tenant = match.group(1).strip()
                if raw_tenant:
                    scope = sanitize_scope(raw_tenant)
                    logger.info("Tenant scope resolved from marker: %s", scope)
                    return scope
        logger.info("Tenant scope not found in input messages")
        return None

    # ------------------------------------------------------------------
    # Helpers
    # ------------------------------------------------------------------

    @staticmethod
    def _build_search_items(messages) -> list[dict]:
        """Convert non-empty input messages to search items, excluding scope markers."""
        items = []
        for msg in (messages or []):
            role = getattr(msg, "role", None)
            if role not in ("user", "assistant", "system"):
                continue
            text = (getattr(msg, "text", "") or "").strip()
            if not text:
                continue
            if role == "system" and (_SCOPE_MARKER_RE.match(text) or _TENANT_CONTEXT_RE.match(text)):
                continue
            items.append({"type": "message", "role": role, "content": text})
        return items

    @staticmethod
    def _build_update_items(context) -> list[dict]:
        """Collect user + assistant messages for memory update."""
        items = []
        for msg in (context.input_messages or []):
            role = getattr(msg, "role", None)
            text = (getattr(msg, "text", "") or "").strip()
            if not text or role not in ("user", "assistant"):
                continue
            items.append({"type": "message", "role": role, "content": text})
        if context.response:
            for msg in (context.response.messages or []):
                role = getattr(msg, "role", None)
                text = (getattr(msg, "text", "") or "").strip()
                if not text or role != "assistant":
                    continue
                items.append({"type": "message", "role": role, "content": text})
        return items

    @staticmethod
    def _format_all_memories(
        user_memories, episodes: list[dict] | None = None,
    ) -> str | None:
        """Format user memories + expert episodes into labeled sections."""
        user_text = MemoryContextProvider._format_section(
            user_memories,
            "## User memories",
            "Use these personal memories when they are relevant to the current question.",
        )
        episode_text = MemoryContextProvider._format_episodes(episodes or [])

        parts = [p for p in (user_text, episode_text) if p]
        return "\n\n".join(parts) if parts else None

    @staticmethod
    def _format_section(memories, header: str, description: str) -> str | None:
        """Deduplicate and format a single memory section."""
        seen: set[str] = set()
        unique: list[str] = []
        for mem in (memories or []):
            mid = mem.memory_item.memory_id
            if mid in seen:
                continue
            seen.add(mid)
            unique.append(mem.memory_item.content)

        if not unique:
            return None
        return (
            f"{header}\n{description}\n"
            + "\n".join(f"- {content}" for content in unique)
        )

    @staticmethod
    def _format_episodes(episodes: list[dict]) -> str | None:
        """Format expert episodes into an injectable context section."""
        if not episodes:
            return None

        lines = [
            "## Expert experience",
            "The following are relevant expert-resolved episodes from past conversations. "
            "Use the reasoning chain and resolution to guide your answer when applicable.",
        ]

        for i, ep in enumerate(episodes, 1):
            lines.append(f"\n### Episode {i}")
            lines.append(f"**Trigger:** {ep.get('trigger', 'N/A')}")

            symptoms = ep.get("symptoms", [])
            if symptoms:
                lines.append("**Symptoms:**")
                for s in symptoms:
                    lines.append(f"  - {s}")

            chain = ep.get("reasoning_chain", [])
            if chain:
                lines.append("**Reasoning chain:**")
                for step_idx, step in enumerate(chain, 1):
                    lines.append(f"  {step_idx}. {step}")

            lines.append(f"**Resolution:** {ep.get('resolution', 'N/A')}")
            lines.append(f"**Key insight:** {ep.get('key_insight', 'N/A')}")

        return "\n".join(lines)
