# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""
Module for managing APIView Copilot agents.

Agents are retrieved by name if they exist, or created if not. Each chat session
creates a new thread with the persistent agent, rather than creating/deleting agents per request.
"""

import asyncio
import logging
from contextlib import contextmanager
from typing import Optional

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


def _get_agents_endpoint() -> str:
    """Get the Azure AI Agents endpoint.

    Constructs the full endpoint: {FOUNDRY_ENDPOINT}/api/projects/{FOUNDRY_PROJECT}
    """
    settings = SettingsManager()
    endpoint = settings.get("FOUNDRY_ENDPOINT")
    project = settings.get("FOUNDRY_PROJECT")

    if not endpoint:
        raise ValueError("FOUNDRY_ENDPOINT not configured in AppConfiguration.")
    if not project:
        raise ValueError("FOUNDRY_PROJECT not configured in AppConfiguration.")

    # Construct the full agents endpoint
    endpoint = endpoint.rstrip("/")
    return f"{endpoint}/api/projects/{project}"


def _get_client() -> AgentsClient:
    """Create and return an AgentsClient."""
    credential = get_credential()
    endpoint = _get_agents_endpoint()
    return AgentsClient(endpoint=endpoint, credential=credential)


def _get_or_create_agent(
    client: AgentsClient,
    name: str,
    description: str,
    instructions: str,
    toolset: ToolSet,
) -> str:
    """Get existing agent by name or create a new one if none exists.

    Args:
        client: The AgentsClient to use
        name: Agent name to search for / create
        description: Agent description
        instructions: Agent instructions
        toolset: Tools available to the agent

    Returns:
        The agent ID
    """
    settings = SettingsManager()
    model_deployment_name = settings.get("FOUNDRY_KERNEL_MODEL")
    if not model_deployment_name:
        raise ValueError("FOUNDRY_KERNEL_MODEL not configured in AppConfiguration.")

    # Search for existing agent by name
    logger.info("Searching for existing agent by name '%s'...", name)
    try:
        existing_agents = list(client.list_agents())
        for agent in existing_agents:
            if agent.name == name:
                logger.info("Found existing agent: %s", agent.id)
                return agent.id
    except Exception as e:
        logger.warning("Error listing agents: %s, will create new one", e)

    # Create new agent only if none exists with that name
    logger.info("No existing agent found, creating new agent '%s'...", name)
    try:
        agent = client.create_agent(
            name=name,
            description=description,
            model=model_deployment_name,
            instructions=instructions,
            toolset=toolset,
        )
        logger.info("Created agent: %s", agent.id)
        return agent.id
    except Exception as e:
        error_msg = str(e)
        if "timed out" in error_msg.lower():
            raise RuntimeError(
                "Failed to create agent: Azure Agents service timed out. "
                "This may be a temporary service issue. Please try again in a moment."
            ) from e
        else:
            raise RuntimeError(f"Failed to create agent: {error_msg}") from e


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
def get_readwrite_agent():
    """Get or create the read-write APIView Copilot agent.

    The agent is retrieved by name if it exists, or created if not.
    Each chat session creates a new thread with this persistent agent.
    """
    client = _get_client()

    ai_instructions = """
Your job is to receive a request from the user, determine their intent, and pass the request to the
appropriate agent or agents for processing. You will then return the response from that agent to the user.
If there's no agent that can handle the request, you will respond with a message indicating that you cannot
process the request. You will also handle any errors that occur during the processing of the request and return
an appropriate error message to the user.
"""

    toolset = ToolSet()
    tools = SearchTools().all_tools() + ApiReviewTools().all_tools() + UtilityTools().all_tools()
    toolset.add(FunctionTool(tools))

    agent_id = _get_or_create_agent(
        client=client,
        name="APIView Copilot Readwrite Agent",
        description="A read-write agent that can perform searches and trigger allowed actions.",
        instructions=ai_instructions,
        toolset=toolset,
    )

    # enable all tools by default
    client.enable_auto_function_calls(tools=toolset)

    yield client, agent_id
    # Note: We don't delete the agent - it persists for reuse


@contextmanager
def get_readonly_agent():
    """Get or create a read-only APIView Copilot agent (no mutating tools).

    The agent is retrieved by name if it exists, or created if not.
    Each chat session creates a new thread with this persistent agent.
    """
    client = _get_client()

    ai_instructions = """
You are a READ-ONLY assistant.
- You may retrieve, search, and summarize information.
- You MUST NOT perform any operation that mutates data or triggers reindexing/admin actions.
- If asked to update/create/delete/link data or run indexers, refuse and explain alternatives.
"""

    toolset = ToolSet()

    # Exclude tools that can mutate state or trigger background operations.
    safe_search_tools = [
        t for t in SearchTools().all_tools() if getattr(t, "__name__", "") not in ("run_indexer", "run_all_indexers")
    ]
    tools = safe_search_tools + ApiReviewTools().all_tools() + UtilityTools().all_tools()
    toolset.add(FunctionTool(tools))

    agent_id = _get_or_create_agent(
        client=client,
        name="APIView Copilot Readonly Agent",
        description="A read-only agent for search/retrieve/summarize without side effects.",
        instructions=ai_instructions,
        toolset=toolset,
    )

    # enable all tools by default
    client.enable_auto_function_calls(tools=toolset)

    yield client, agent_id
    # Note: We don't delete the agent - it persists for reuse
