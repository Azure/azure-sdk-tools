"""
Azure SDK QA Bot Agent Memory Store helpers.

This module provides utilities to create or reuse a Foundry memory store for the Azure SDK QA Bot Agent.
The memory store enables the agent to recall user context, previous questions, and conversation history,
enhancing the quality and relevance of answers for Azure SDK-related queries.

Key Features:
- Ensures contextual recall for QA bot interactions.
- Supports continuity across multiple user sessions.
- Designed for Azure SDK engineering and developer support scenarios.

Example prompt for memory store usage:
"Recall my last question about Azure Blob Storage and provide updated guidance."
"""

from agent_framework.azure import FoundryMemoryProvider
from azure.ai.projects.aio import AIProjectClient
from azure.ai.projects.models import (
    MemoryStoreDefaultDefinition,
    MemoryStoreDefaultOptions,
)
from azure.core.exceptions import ResourceNotFoundError

from config.app_config import get as cfg


async def ensure_memory_provider(
    project_client: AIProjectClient,
) -> FoundryMemoryProvider | None:
    """Create or reuse a Foundry memory store and return a context provider.

    Returns ``None`` when ``MEMORY_STORE_NAME`` is not set, which lets the
    caller skip memory entirely.
    """
    store_name = cfg("MEMORY_STORE_NAME", "azure-sdk-qa-bot-memory-store")
    if not store_name:
        return None
    
    update_delay = int(cfg("MEMORY_UPDATE_DELAY", "300"))
    chat_model = cfg("AOAI_CHAT_REASONING_MODEL", "gpt-5.4")
    embedding_model = cfg("MEMORY_STORE_EMBEDDING_MODEL", "text-embedding-ada-002")

    try:
        await project_client.beta.memory_stores.get(store_name)
    except ResourceNotFoundError:
        if not embedding_model:
            raise ValueError(
                "MEMORY_STORE_EMBEDDING_MODEL is required when creating a new memory store."
            )

        await project_client.beta.memory_stores.create(
            name=store_name,
            definition=MemoryStoreDefaultDefinition(
                chat_model=chat_model,
                embedding_model=embedding_model,
                options=MemoryStoreDefaultOptions(
                    chat_summary_enabled=True,
                    user_profile_enabled=True,
                ),
            ),
            description="Memory store for the Azure SDK QA bot agent.",
        )

    return FoundryMemoryProvider(
        project_client=project_client,
        memory_store_name=store_name,
        scope=cfg("MEMORY_STORE_SCOPE", "azure-sdk-qa-bot"),
        update_delay=update_delay,
        context_prompt=(
            "## Remembered user context\n"
            "Use these memories when they are relevant to the current Azure SDK question."
        ),
    )
