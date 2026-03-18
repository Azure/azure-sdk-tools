"""Azure AI Foundry client singletons.

Each client is created once on first access and reused for the lifetime of
the process.
"""

from agent_framework.azure import AzureAIAgentClient
from azure.ai.projects.aio import AIProjectClient

from config.app_config import get as cfg
from utils.azure_credential import get_credential

_agent_client: AzureAIAgentClient | None = None
_project_client: AIProjectClient | None = None


def get_agent_client() -> AzureAIAgentClient:
    """Return the shared AzureAIAgentClient (created once on first call)."""
    global _agent_client
    if _agent_client is None:
        _agent_client = AzureAIAgentClient(
            project_endpoint=cfg("AI_FOUNDRY_PROJECT_ENDPOINT"),
            model_deployment_name=cfg("AI_FOUNDRY_AGENT_COMPLETION_MODEL", "gpt-5.1"),
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


async def close_clients() -> None:
    """Close both clients.  Safe to call even if never created."""
    global _agent_client, _project_client
    if _agent_client is not None:
        await _agent_client.close()
        _agent_client = None
    if _project_client is not None:
        await _project_client.close()
        _project_client = None