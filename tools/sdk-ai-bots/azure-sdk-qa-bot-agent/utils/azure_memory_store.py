"""Azure SDK QA Bot Agent Memory Store helpers.

Provides shared config accessors (store names, update delay, scope
sanitisation), plus one-time store-creation helpers.
"""

from __future__ import annotations

import logging
import os
import re

from azure.ai.projects.aio import AIProjectClient
from azure.ai.projects.models import (
    MemoryStoreDefaultDefinition,
    MemoryStoreDefaultOptions,
)
from azure.core.exceptions import ResourceNotFoundError

from config.app_config import get as cfg

logger = logging.getLogger(__name__)

# ---------------------------------------------------------------------------
# Shared config accessors
# ---------------------------------------------------------------------------

_SANITIZE_RE = re.compile(r"[^A-Za-z0-9_-]")


def get_user_store_name() -> str | None:
    """Return the user memory store name from App Config, or ``None``."""
    return cfg("MEMORY_USER_STORE_NAME") or None


def get_tenant_store_name() -> str | None:
    """Return the tenant memory store name from App Config, or ``None``."""
    return cfg("MEMORY_TENANT_STORE_NAME") or None


def get_memory_update_delay() -> int:
    """Return memory update delay in seconds.

    Local env var overrides App Config for flexibility during development.
    """
    return int(os.environ.get("MEMORY_UPDATE_DELAY") or cfg("MEMORY_UPDATE_DELAY", "300"))


def sanitize_scope(raw: str) -> str:
    """Keep only characters allowed by Foundry Memory Store: A-Z, a-z, 0-9, '-', '_'."""
    return _SANITIZE_RE.sub("", raw)[:256]


# ---------------------------------------------------------------------------
# User‑profile extraction instructions
# ---------------------------------------------------------------------------

_USER_PROFILE_DETAILS = (
    "Extract personal preferences, the SDK and programming language the user "
    "works with, their specific project context, and individual working patterns."
)

_TENANT_PROFILE_DETAILS = (
    "Extract ONLY knowledge that applies universally to all users in this tenant. "
    "Focus on resolved threads and expert responses. For each valuable insight, capture: "
    "the intent (what the user was trying to do), root cause (why it failed or what was misunderstood), "
    "resolution steps (how the expert resolved it), and any constraints or caveats. "
    "Also capture known active issues (e.g. bugs, outages) with their current status. "
    "Include temporal context when relevant (e.g. 'as of March 2026', 'since version X'). "
    "Do NOT store: user-specific information (individual projects, personal preferences, "
    "service-specific details), clarification questions without resolution, "
    "or social exchanges (greetings, thanks)."
)


# ---------------------------------------------------------------------------
# Ensure helpers
# ---------------------------------------------------------------------------

async def _ensure_memory_store(
    project_client: AIProjectClient,
    store_name: str,
    *,
    chat_summary_enabled: bool,
    user_profile_details: str,
    description: str,
) -> str:
    """Create a memory store if it doesn't already exist. Returns the name."""
    chat_model = cfg("AOAI_CHAT_REASONING_MODEL", "gpt-5.4")
    embedding_model = cfg("MEMORY_STORE_EMBEDDING_MODEL", "text-embedding-ada-002")

    try:
        await project_client.beta.memory_stores.get(store_name)
        logger.info("Memory store already exists: %s", store_name)
    except ResourceNotFoundError:
        await project_client.beta.memory_stores.create(
            name=store_name,
            definition=MemoryStoreDefaultDefinition(
                chat_model=chat_model,
                embedding_model=embedding_model,
                options=MemoryStoreDefaultOptions(
                    user_profile_enabled=True,
                    chat_summary_enabled=chat_summary_enabled,
                    user_profile_details=user_profile_details,
                ),
            ),
            description=description,
        )
        logger.info("Created memory store: %s", store_name)

    return store_name


async def ensure_user_memory_store(project_client: AIProjectClient) -> str | None:
    """Create the **user** memory store if it doesn't exist yet.

    Returns the store name, or ``None`` when ``MEMORY_USER_STORE_NAME`` is unset.
    Call once at startup.
    """
    name = get_user_store_name()
    if not name:
        return None
    return await _ensure_memory_store(
        project_client,
        name,
        chat_summary_enabled=True,
        user_profile_details=_USER_PROFILE_DETAILS,
        description="User-scoped memory store for the Azure SDK QA bot agent.",
    )


async def ensure_tenant_memory_store(project_client: AIProjectClient) -> str | None:
    """Create the **tenant** memory store if it doesn't exist yet.

    Returns the store name, or ``None`` when ``MEMORY_TENANT_STORE_NAME`` is unset.
    Call once at startup.
    """
    name = get_tenant_store_name()
    if not name:
        return None
    return await _ensure_memory_store(
        project_client,
        name,
        chat_summary_enabled=False,
        user_profile_details=_TENANT_PROFILE_DETAILS,
        description="Tenant-scoped memory store for universally applicable knowledge.",
    )
