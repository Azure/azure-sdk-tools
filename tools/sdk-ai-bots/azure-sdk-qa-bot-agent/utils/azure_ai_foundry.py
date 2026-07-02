"""Azure AI Foundry client singletons.

Each client is created once on first access and reused for the lifetime of
the process.
"""

import asyncio
import logging
import re

from agent_framework import TruncationStrategy
from agent_framework.foundry import FoundryChatClient
from azure.ai.projects.aio import AIProjectClient
from openai import AsyncAzureOpenAI, AsyncOpenAI

from config.app_config import get as cfg
from utils.azure_credential import get_credential

logger = logging.getLogger(__name__)

# -- Compaction constants (token counts) -----------------------------------
COMPACTION_TRIGGER_TOKENS = 100000
COMPACTION_TARGET_TOKENS = 80000

_agent_client: FoundryChatClient | None = None
_project_client: AIProjectClient | None = None
_openai_client: AsyncOpenAI | None = None
_embedding_client: AsyncAzureOpenAI | None = None


def get_agent_client() -> FoundryChatClient:
    """Return the shared FoundryChatClient (created once on first call)."""
    global _agent_client
    if _agent_client is None:
        _agent_client = FoundryChatClient(
            project_endpoint=cfg("AI_FOUNDRY_PROJECT_ENDPOINT"),
            model=cfg("AI_FOUNDRY_AGENT_COMPLETION_MODEL"),
            credential=get_credential(),
            compaction_strategy=TruncationStrategy(
                max_n=COMPACTION_TRIGGER_TOKENS,
                compact_to=COMPACTION_TARGET_TOKENS,
            ),
        )
    return _agent_client


def get_project_client() -> AIProjectClient:
    """Return the shared AIProjectClient (created once on first call)."""
    global _project_client
    if _project_client is None:
        endpoint = cfg("AI_FOUNDRY_PROJECT_ENDPOINT")
        if not endpoint:
            raise RuntimeError(
                "AI_FOUNDRY_PROJECT_ENDPOINT is required in App Configuration."
            )
        _project_client = AIProjectClient(
            endpoint=endpoint,
            credential=get_credential(),
            allow_preview=True,
        )
    return _project_client


def get_openai_client() -> AsyncOpenAI:
    """Return the shared OpenAI client (created once on first call)."""
    global _openai_client
    if _openai_client is None:
        agent_name = cfg("AI_FOUNDRY_AGENT_NAME", "azure-sdk-chat-agent")
        # Hosted agents in refreshed preview must be called via per-agent endpoint.
        _openai_client = get_project_client().get_openai_client(agent_name=agent_name)
    return _openai_client


def get_embedding_client() -> AsyncAzureOpenAI:
    """Return a dedicated Azure OpenAI client for embedding generation.

    The embedding model is deployed on the Azure OpenAI resource directly
    (``*.openai.azure.com``), not through the AI Foundry project proxy
    (``*.services.ai.azure.com``).  This client uses the direct endpoint.
    """
    global _embedding_client
    if _embedding_client is not None:
        return _embedding_client

    try:
        endpoint = cfg("AI_FOUNDRY_PROJECT_ENDPOINT", "")
        # Extract resource name from AI Foundry endpoint
        # e.g. https://<resource>.services.ai.azure.com/... → <resource>
        m = re.search(r"https://([^.]+)\.", endpoint)
        resource_name = m.group(1) if m else ""
        azure_openai_endpoint = f"https://{resource_name}.openai.azure.com"

        _embedding_client = AsyncAzureOpenAI(
            azure_endpoint=azure_openai_endpoint,
            api_version="2024-02-01",
            azure_ad_token_provider=_get_token_provider(),
        )
        return _embedding_client
    except Exception:
        logger.info("Failed to create embedding client", exc_info=True)
        raise


def _get_token_provider():
    """Return a callable that provides Azure AD tokens for Azure OpenAI."""
    credential = get_credential()

    async def _provider():
        token = await credential.get_token(
            "https://cognitiveservices.azure.com/.default"
        )
        return token.token

    return _provider


async def close_clients() -> None:
    """Close all clients.  Safe to call even if never created."""
    global _agent_client, _project_client, _openai_client, _embedding_client
    if _embedding_client is not None:
        await _embedding_client.close()
        _embedding_client = None
    if _openai_client is not None:
        await _openai_client.close()
        _openai_client = None
    if _agent_client is not None:
        _agent_client = None
    if _project_client is not None:
        await _project_client.close()
        _project_client = None


# -- Stateless warm-session cache ------------------------------------------
# Stateless requests (no customer conversation_id) share one warm sandbox via a
# stable agent_session_id captured from the first stateless response. No
# `conversation` is threaded, so history is never shared across callers.
_stateless_session_id: str | None = None


def get_stateless_session_id() -> str | None:
    """Return the cached warm sandbox id for stateless requests, if any."""
    return _stateless_session_id


def set_stateless_session_id(session_id: str | None) -> None:
    """Cache (or clear) the warm sandbox id reused by stateless requests."""
    global _stateless_session_id
    _stateless_session_id = session_id
