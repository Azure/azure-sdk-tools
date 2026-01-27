# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""
Module for managing APIView Copilot agents.

This module provides cached agent management to avoid recreating agents on every call.
Agents are persisted and reused, with queries tracked via threads.
"""

import asyncio
import logging
import threading
from contextlib import contextmanager
from dataclasses import dataclass
from typing import Dict, Optional

from azure.ai.agents import AgentsClient
from azure.ai.agents.models import (
    FunctionTool,
    MessageRole,
    MessageTextContent,
    ToolSet,
)
from src._credential import get_credential
from src._settings import SettingsManager
from src.agent.tools._api_review_tools import ApiReviewTools
from src.agent.tools._search_tools import SearchTools
from src.agent.tools._utility_tools import UtilityTools

logger = logging.getLogger(__name__)


@dataclass
class CachedAgent:
    """Holds a cached agent's metadata."""

    agent_id: str
    name: str
    client: AgentsClient


class AgentCache:
    """
    Thread-safe cache for Azure AI Foundry agents.

    Agents are created once and reused across calls. Each new query creates
    a new thread within the existing agent, avoiding the overhead of
    agent creation/deletion.
    """

    _instance: Optional["AgentCache"] = None
    _lock = threading.Lock()

    def __init__(self):
        # Only initialize if not already done (for singleton)
        if not hasattr(self, "_agents"):
            self._agents: Dict[str, CachedAgent] = {}
            self._cache_lock = threading.Lock()

    def __new__(cls) -> "AgentCache":
        if cls._instance is None:
            with cls._lock:
                if cls._instance is None:
                    cls._instance = super().__new__(cls)
        return cls._instance

    def get_or_create_agent(
        self,
        agent_key: str,
        name: str,
        description: str,
        instructions: str,
        model: str,
        toolset: ToolSet,
        client: AgentsClient,
    ) -> tuple[str, bool]:
        """
        Get an existing agent or create a new one.

        Args:
            agent_key: Unique key to identify this agent type (e.g., "readwrite", "readonly")
            name: Agent name
            description: Agent description
            instructions: System instructions for the agent
            model: Model deployment name
            toolset: Tools available to the agent
            client: AgentsClient instance

        Returns:
            Tuple of (agent_id, was_created)
        """
        with self._cache_lock:
            # Check if we have a cached agent
            cached = self._agents.get(agent_key)
            if cached:
                # Verify the agent still exists by trying to retrieve it
                try:
                    client.get_agent(cached.agent_id)
                    logger.info("Reusing cached agent '%s' (id: %s)", agent_key, cached.agent_id)
                    return cached.agent_id, False
                except Exception as e:
                    logger.warning("Cached agent '%s' no longer valid: %s", agent_key, e)
                    del self._agents[agent_key]

            # Create a new agent
            logger.info("Creating new agent '%s'...", agent_key)
            try:
                agent = client.create_agent(
                    name=name,
                    description=description,
                    model=model,
                    instructions=instructions,
                    toolset=toolset,
                )
                self._agents[agent_key] = CachedAgent(
                    agent_id=agent.id,
                    name=name,
                    client=client,
                )
                logger.info("Created agent '%s' (id: %s)", agent_key, agent.id)
                return agent.id, True
            except Exception as e:
                error_msg = str(e)
                if "timed out" in error_msg.lower():
                    raise RuntimeError(
                        "Failed to create agent: Azure Agents service timed out. "
                        "This may be a temporary service issue. Please try again in a moment."
                    ) from e
                else:
                    raise RuntimeError(f"Failed to create agent: {error_msg}") from e

    def delete_agent(self, agent_key: str, client: AgentsClient) -> bool:
        """
        Delete a cached agent.

        Args:
            agent_key: Key of the agent to delete
            client: AgentsClient instance

        Returns:
            True if agent was deleted, False if not found
        """
        with self._cache_lock:
            cached = self._agents.pop(agent_key, None)
            if cached:
                try:
                    client.delete_agent(cached.agent_id)
                    logger.info("Deleted agent '%s' (id: %s)", agent_key, cached.agent_id)
                    return True
                except Exception as e:
                    logger.warning("Failed to delete agent '%s': %s", agent_key, e)
            return False

    def clear_all(self, client: Optional[AgentsClient] = None) -> int:
        """
        Clear all cached agents.

        Args:
            client: Optional AgentsClient to use for deletion. If not provided,
                    agents are only removed from cache without server deletion.

        Returns:
            Number of agents cleared
        """
        with self._cache_lock:
            count = len(self._agents)
            if client:
                for key, cached in list(self._agents.items()):
                    try:
                        client.delete_agent(cached.agent_id)
                        logger.info("Deleted agent '%s' (id: %s)", key, cached.agent_id)
                    except Exception as e:
                        logger.warning("Failed to delete agent '%s': %s", key, e)
            self._agents.clear()
            return count

    def get_cached_agent_id(self, agent_key: str) -> Optional[str]:
        """Get the agent ID if cached, without validation."""
        with self._cache_lock:
            cached = self._agents.get(agent_key)
            return cached.agent_id if cached else None


# Global cache instance
_agent_cache = AgentCache()


def get_foundry_project_endpoint() -> str:
    """Get the full Azure AI Foundry project endpoint for Agents.

    Constructs the endpoint from foundry_endpoint and foundry_project settings.
    Returns: https://{host}/api/projects/{project}
    """
    settings = SettingsManager()
    foundry_endpoint = settings.get("foundry_endpoint")
    foundry_project = settings.get("foundry_project")

    if not foundry_endpoint:
        raise ValueError("foundry_endpoint not configured in AppConfiguration.")
    if not foundry_project:
        raise ValueError("foundry_project not configured in AppConfiguration.")

    base_url = foundry_endpoint.rstrip("/")
    return f"{base_url}/api/projects/{foundry_project}"


async def invoke_agent(
    *,
    client: AgentsClient,
    agent_id: str,
    user_input: str,
    thread_id: Optional[str] = None,
    messages: Optional[list[str]] = None,
) -> tuple[str, str, list[str]]:
    """
    Invoke an agent with the provided user input and thread ID.
    Returns: (response_text, thread_id, messages)
    """
    messages = messages or []
    # Only append user_input if not already the last message
    if not messages or messages[-1] != user_input:
        messages.append(user_input)

    # 1) Ensure a thread exists
    if not thread_id:
        thread_obj = await asyncio.to_thread(client.threads.create)
        thread_id = thread_obj.id

    # 2) Add user message to the thread
    await asyncio.to_thread(client.messages.create, thread_id=thread_id, role="user", content=user_input)

    # 3) Process a run (polls until terminal state; executes tools if auto-enabled)
    logger.info("Processing agent run... (this may take a moment)")
    await asyncio.to_thread(client.runs.create_and_process, thread_id=thread_id, agent_id=agent_id)
    logger.info("Agent run completed")

    # 4) Collect messages and extract the latest assistant text
    all_messages = await asyncio.to_thread(client.messages.list, thread_id=thread_id)

    def extract_text(obj):
        """Recursively extract all text values from nested lists/dicts and MessageTextContent."""
        if isinstance(obj, MessageTextContent):
            return obj.text.value
        elif isinstance(obj, list):
            return " ".join(extract_text(item) for item in obj)
        elif isinstance(obj, str):
            return obj
        else:
            return str(obj)

    response_text = ""
    # Note: client.messages.list returns messages with newest first, so we iterate
    # without reversing to get the most recent assistant message
    for m in list(all_messages):
        role = getattr(m, "role", None)
        if role == MessageRole.AGENT.value or role == "assistant":
            parts = getattr(m, "content", None)
            response_text = extract_text(parts)
            break
    return response_text, thread_id, messages


@contextmanager
def get_readwrite_agent(*, force_new: bool = False):
    """
    Get a read-write APIView Copilot agent.

    The agent is cached and reused across calls. Each invocation creates a new
    thread for query tracking instead of recreating the agent.

    Args:
        force_new: If True, delete any cached agent and create a fresh one.

    Yields:
        Tuple of (AgentsClient, agent_id)
    """
    settings = SettingsManager()
    endpoint = get_foundry_project_endpoint()
    model_deployment_name = settings.get("FOUNDRY_KERNEL_MODEL")

    ai_instructions = """
Your job is to receive a request from the user, determine their intent, and pass the request to the
appropriate agent or agents for processing. You will then return the response from that agent to the user.
If there's no agent that can handle the request, you will respond with a message indicating that you cannot
process the request. You will also handle any errors that occur during the processing of the request and return
an appropriate error message to the user.
"""
    credential = get_credential()
    client = AgentsClient(endpoint=endpoint, credential=credential)

    toolset = ToolSet()
    tools = SearchTools().all_tools() + ApiReviewTools().all_tools() + UtilityTools().all_tools()
    toolset.add(FunctionTool(tools))

    agent_key = "readwrite"

    # Force delete if requested
    if force_new:
        _agent_cache.delete_agent(agent_key, client)

    # Get or create the agent (cached)
    agent_id, _ = _agent_cache.get_or_create_agent(
        agent_key=agent_key,
        name="APIView Copilot Readwrite Agent",
        description="A read-write agent that can perform searches and trigger allowed actions.",
        instructions=ai_instructions,
        model=model_deployment_name,
        toolset=toolset,
        client=client,
    )

    # Enable auto function calls
    client.enable_auto_function_calls(tools=toolset)

    # Yield client and agent_id - agent is NOT deleted on exit
    yield client, agent_id


@contextmanager
def get_readonly_agent(*, force_new: bool = False):
    """
    Get a read-only APIView Copilot agent (no mutating tools).

    The agent is cached and reused across calls. Each invocation creates a new
    thread for query tracking instead of recreating the agent.

    Args:
        force_new: If True, delete any cached agent and create a fresh one.

    Yields:
        Tuple of (AgentsClient, agent_id)
    """
    settings = SettingsManager()
    endpoint = get_foundry_project_endpoint()
    model_deployment_name = settings.get("FOUNDRY_KERNEL_MODEL")

    ai_instructions = """
You are a READ-ONLY assistant.
- You may retrieve, search, and summarize information.
- You MUST NOT perform any operation that mutates data or triggers reindexing/admin actions.
- If asked to update/create/delete/link data or run indexers, refuse and explain alternatives.
"""

    credential = get_credential()
    client = AgentsClient(endpoint=endpoint, credential=credential)

    toolset = ToolSet()

    # Exclude tools that can mutate state or trigger background operations.
    safe_search_tools = [
        t for t in SearchTools().all_tools() if getattr(t, "__name__", "") not in ("run_indexer", "run_all_indexers")
    ]
    tools = safe_search_tools + ApiReviewTools().all_tools() + UtilityTools().all_tools()
    toolset.add(FunctionTool(tools))

    agent_key = "readonly"

    # Force delete if requested
    if force_new:
        _agent_cache.delete_agent(agent_key, client)

    # Get or create the agent (cached)
    agent_id, _ = _agent_cache.get_or_create_agent(
        agent_key=agent_key,
        name="APIView Copilot Readonly Agent",
        description="A read-only agent for search/retrieve/summarize without side effects.",
        instructions=ai_instructions,
        model=model_deployment_name,
        toolset=toolset,
        client=client,
    )

    # Enable auto function calls
    client.enable_auto_function_calls(tools=toolset)

    # Yield client and agent_id - agent is NOT deleted on exit
    yield client, agent_id


def delete_cached_agents() -> int:
    """
    Delete all cached agents from both memory and the Foundry service.

    Call this when you want to clean up agents (e.g., on application shutdown,
    or when agent configuration has changed).

    Returns:
        Number of agents deleted
    """
    endpoint = get_foundry_project_endpoint()
    credential = get_credential()
    client = AgentsClient(endpoint=endpoint, credential=credential)
    return _agent_cache.clear_all(client)


def get_cached_agent_ids() -> dict[str, str]:
    """
    Get a dictionary of all cached agent keys to their IDs.

    Returns:
        Dict mapping agent keys (e.g., "readwrite", "readonly") to agent IDs
    """
    result = {}
    for key in ["readwrite", "readonly"]:
        agent_id = _agent_cache.get_cached_agent_id(key)
        if agent_id:
            result[key] = agent_id
    return result
