"""Azure AI Foundry client singletons.

Each client is created once on first access and reused for the lifetime of
the process.
"""

from agent_framework.azure import AzureOpenAIResponsesClient
from azure.ai.projects.aio import AIProjectClient
from openai import AsyncAzureOpenAI

import logging

from config.app_config import get as cfg
from utils.azure_credential import get_credential

logger = logging.getLogger(__name__)

_agent_client: AzureOpenAIResponsesClient | None = None
_project_client: AIProjectClient | None = None
_openai_client = None
_embedding_client: AsyncAzureOpenAI | None = None


def get_agent_client() -> AzureOpenAIResponsesClient:
    """Return the shared AzureOpenAIResponsesClient (created once on first call)."""
    global _agent_client
    if _agent_client is None:
        _agent_client = AzureOpenAIResponsesClient(
            project_endpoint=cfg("AI_FOUNDRY_PROJECT_ENDPOINT"),
            deployment_name=cfg("AI_FOUNDRY_AGENT_COMPLETION_MODEL", "gpt-5.4"),
            credential=get_credential(),
        )
    return _agent_client


def get_project_client() -> AIProjectClient:
    """Return the shared AIProjectClient (created once on first call)."""
    global _project_client
    if _project_client is None:
        _project_client = AIProjectClient(
            endpoint=cfg("AI_FOUNDRY_PROJECT_ENDPOINT"),
            credential=get_credential(),
        )
    return _project_client


def get_openai_client():
    """Return the shared OpenAI client (created once on first call)."""
    global _openai_client
    if _openai_client is None:
        _openai_client = get_project_client().get_openai_client()
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
        import re
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
    import asyncio

    credential = get_credential()

    async def _provider():
        token = await credential.get_token("https://cognitiveservices.azure.com/.default")
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
        await _agent_client.close()
        _agent_client = None
    if _project_client is not None:
        await _project_client.close()
        _project_client = None
