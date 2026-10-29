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
from azure.core.exceptions import ResourceNotFoundError
from openai import AsyncAzureOpenAI, AsyncOpenAI

from config.app_config import Settings, get as cfg
from utils.azure_credential import get_credential

logger = logging.getLogger(__name__)

# -- Compaction constants (token counts) -----------------------------------
COMPACTION_TRIGGER_TOKENS = 100000
COMPACTION_TARGET_TOKENS = 80000

_agent_client: FoundryChatClient | None = None
_project_client: AIProjectClient | None = None
_openai_clients: dict[str, AsyncOpenAI] = {}
_embedding_client: AsyncAzureOpenAI | None = None


def create_project_client(
    settings: Settings,
) -> AIProjectClient:
    """Create a project client from an explicit configuration scope."""
    endpoint = settings("AI_FOUNDRY_PROJECT_ENDPOINT", None)
    if not endpoint:
        raise RuntimeError(
            "AI_FOUNDRY_PROJECT_ENDPOINT is required in App Configuration."
        )
    return AIProjectClient(
        endpoint=endpoint,
        credential=get_credential(),
        allow_preview=True,
    )


def create_openai_client(
    project_client: AIProjectClient,
    settings: Settings,
) -> AsyncOpenAI:
    """Create a hosted-agent OpenAI client from an explicit config scope."""
    agent_name = settings("AI_FOUNDRY_AGENT_NAME", "azure-sdk-chat-agent")
    return project_client.get_openai_client(agent_name=agent_name)


def get_agent_client(*, model: str | None = None) -> FoundryChatClient:
    """Return the shared client, applying an optional model override on first use."""
    global _agent_client
    if _agent_client is None:
        _agent_client = FoundryChatClient(
            project_endpoint=cfg("AI_FOUNDRY_PROJECT_ENDPOINT"),
            model=model if model is not None else cfg("AI_FOUNDRY_AGENT_COMPLETION_MODEL"),
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
        _project_client = create_project_client(cfg)
    return _project_client


def get_openai_client(agent_name: str | None = None) -> AsyncOpenAI:
    """Return the OpenAI client for a hosted agent."""
    resolved_name = agent_name or cfg("AI_FOUNDRY_AGENT_NAME", "azure-sdk-chat-agent")
    if resolved_name not in _openai_clients:
        # Hosted agents in refreshed preview must be called via per-agent endpoint.
        _openai_clients[resolved_name] = get_project_client().get_openai_client(
            agent_name=resolved_name
        )
    return _openai_clients[resolved_name]


async def get_branch_agent_version(
    project_client: AIProjectClient, agent_name: str, branch: str
) -> str | None:
    """Find the newest active version tagged with the requested branch."""
    logger.info("Looking up Foundry agent=%s branch=%s newest-first.", agent_name, branch)
    try:
        async for candidate in project_client.agents.list_versions(agent_name, order="desc"):
            if (candidate.metadata or {}).get("branchname") != branch:
                continue
            # List responses may omit readiness; check details before selecting.
            if candidate.status is None:
                logger.info(
                    "Checking readiness for agent=%s version=%s branch=%s.",
                    agent_name, candidate.version, branch,
                )
                try:
                    candidate = await project_client.agents.get_version(agent_name, candidate.version)
                except ResourceNotFoundError:
                    logger.info(
                        "Skipping deleted agent=%s version=%s branch=%s.",
                        agent_name, candidate.version, branch,
                    )
                    continue
            status = str(candidate.status or "").lower().rsplit(".", 1)[-1]
            if status in ("active", "succeeded"):
                logger.info(
                    "Selected Foundry agent=%s version=%s branch=%s status=%s.",
                    agent_name, candidate.version, branch, status,
                )
                return candidate.version
            if status:
                logger.info(
                    "Skipping inactive agent=%s version=%s branch=%s status=%s.",
                    agent_name, candidate.version, branch, status,
                )
            if not status:
                logger.warning(
                    "Skipping agent %s v%s: Foundry did not return readiness status.",
                    agent_name, candidate.version,
                )
    except ResourceNotFoundError:
        logger.info("Foundry agent=%s was not found for branch=%s.", agent_name, branch)
        return None
    logger.info("No active Foundry version found for agent=%s branch=%s.", agent_name, branch)
    return None


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
    global _agent_client, _project_client, _embedding_client
    if _embedding_client is not None:
        await _embedding_client.close()
        _embedding_client = None
    for client in _openai_clients.values():
        await client.close()
    _openai_clients.clear()
    _stateless_session_ids.clear()
    if _agent_client is not None:
        _agent_client = None
    if _project_client is not None:
        await _project_client.close()
        _project_client = None


# -- Stateless warm-session cache ------------------------------------------
# Stateless requests (no customer conversation_id) share one warm sandbox per
# agent version via an agent_session_id captured from a stateless response.
# No `conversation` is threaded, so history is never shared across callers.
_stateless_session_ids: dict[tuple[str, str], str] = {}


def get_stateless_session_id(agent_name: str, agent_version: str) -> str | None:
    """Return the cached warm sandbox id for stateless requests, if any."""
    return _stateless_session_ids.get((agent_name, agent_version))


def set_stateless_session_id(
    agent_name: str, agent_version: str, session_id: str | None
) -> None:
    """Cache (or clear) the warm sandbox id reused by stateless requests."""
    if session_id is None:
        _stateless_session_ids.pop((agent_name, agent_version), None)
    else:
        _stateless_session_ids[(agent_name, agent_version)] = session_id
