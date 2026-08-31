"""Azure SDK QA Bot — Hosted Chatbot Evolution Agent.

Self-contained entrypoint for the hosted chatbot evolution agent container.
Runs as an HTTP server on port 8088 using the Responses protocol.
Deployed to Microsoft Foundry as a container agent.

The Chatbot Evolution Agent is a KB-quality analyst — it analyzes past chat
turns that received negative feedback (explicit thumbs-down or implicit expert
correction) and either files a KB-gap issue or records a structured
classification for downstream Agent Optimizer dataset curation.
"""

import asyncio
import logging
import os
import sys
from pathlib import Path

import yaml
from dotenv import load_dotenv

# sys.path — add the project root so top-level packages resolve.
_project_root = str(Path(__file__).resolve().parent.parent.parent)
if _project_root not in sys.path:
    sys.path.insert(0, _project_root)

# Environment
load_dotenv(override=False)
os.environ.setdefault("ENABLE_SENSITIVE_DATA", "true")

from agent_framework import Agent
from agent_framework import CompactionProvider
from agent_framework import ToolResultCompactionStrategy
from agent_framework_foundry_hosting import ResponsesHostServer

import config.app_config as app_config
from config.app_config import get as cfg
from tools.chatagent_tools import ChatAgentTools
from tools.conversation_tools import ConversationTools
from tools.github_mcp_tools import create_github_mcp_tool
from tools.knowledge_tools import KnowledgeTools
from tools.monitor_tools import MonitorTools
from tools.web_tools import WebTools
from services.chat_service import ChatService
from utils.azure_ai_search import SearchClient
from utils.azure_ai_foundry import (
    create_openai_client,
    create_project_client,
    get_agent_client,
    get_project_client,
    get_openai_client,
)
from utils.azure_storage import create_blob_service_client
from utils.tool_security import ToolOutputSecurityMiddleware

logger = logging.getLogger(__name__)

# -- Agent configuration constants ----------------------------------------
# Feedback workflow is more deliberate than chat: allow more tool-call
# iterations so the agent can fetch trace, fetch conversation, enumerate
# sources, re-search (tenant-scoped and whole-KB), read chat-agent source
# to diagnose system defects, fetch a source URL, and file an issue in one
# turn.
MAX_TOOL_CALLS_PER_TURN = 20
# Reserve a tool-free iteration for the final structured response.
MAX_TOOL_CALL_ITERATIONS = MAX_TOOL_CALLS_PER_TURN + 1


def _load_instructions(file_path: Path) -> str:
    if not file_path.exists():
        raise FileNotFoundError(f"Agent instructions file not found: {file_path}")
    return file_path.read_text(encoding="utf-8").strip()


async def main() -> None:
    """Start the hosted Chatbot Evolution Agent as an HTTP server."""
    await app_config.init()
    candidate_endpoint = os.environ.get("CANDIDATE_APPCONFIG_ENDPOINT")
    if not candidate_endpoint:
        raise RuntimeError(
            "CANDIDATE_APPCONFIG_ENDPOINT is required in the agent environment."
        )
    candidate_config = await app_config.load(candidate_endpoint)

    agent_client = get_agent_client()
    agent_client.function_invocation_configuration["max_iterations"] = (
        MAX_TOOL_CALL_ITERATIONS
    )

    agent_dir = Path(__file__).parent
    instructions = _load_instructions(agent_dir / "instruction.md")
    with open(agent_dir / "agent.yaml", encoding="utf-8") as f:
        agent_config = yaml.safe_load(f)
    agent_name = agent_config["name"]

    agent_version = os.environ.get("APP_VERSION")
    agent_id = f"{agent_name}:{agent_version}" if agent_version else agent_name

    # Tools.
    monitor_tools = MonitorTools()
    conversation_tools = ConversationTools()
    candidate_project_client = create_project_client(candidate_config.get)
    candidate_openai_client = create_openai_client(
        candidate_project_client, candidate_config.get
    )
    candidate_chat_service = ChatService(
        settings=candidate_config.get,
        project_client=candidate_project_client,
        openai_client=candidate_openai_client,
    )
    prod_chat_service = ChatService(
        project_client=get_project_client(),
        openai_client=get_openai_client(),
    )
    chatagent_tools = ChatAgentTools(
        candidate_chat_service=candidate_chat_service,
        prod_chat_service=prod_chat_service,
    )
    knowledge_tools = KnowledgeTools(
        settings=candidate_config.get,
        search_client=SearchClient(candidate_config.get),
        blob_client=create_blob_service_client(candidate_config.get),
    )
    web_tools = WebTools()

    tools = [
        monitor_tools.fetch_chat_trace,
        conversation_tools.resolve_conversation_by_trace_id,
        conversation_tools.fetch_conversation,
        knowledge_tools.list_knowledge_sources,
        knowledge_tools.resolve_kb_source,
        knowledge_tools.search_knowledge_base,
        knowledge_tools.read_knowledge,
        knowledge_tools.update_knowledge,
        chatagent_tools.chat,
        web_tools.web_fetch,
    ]

    # GitHub MCP tool with write access so the agent can file KB-gap issues.
    try:
        github_mcp_tool = await create_github_mcp_tool(
            readonly=False,
            extra_allowed_tools=("add_issue_comment", "issue_write"),
        )
        tools.append(github_mcp_tool)
    except Exception:
        logger.exception("create_github_mcp_tool failed to initialize, skipped")

    # Compaction provider — compact history before and after each turn.
    compaction_provider = CompactionProvider(
        before_strategy=ToolResultCompactionStrategy(keep_last_tool_call_groups=2),
        after_strategy=ToolResultCompactionStrategy(keep_last_tool_call_groups=1),
    )

    reasoning_effort = cfg("AI_FOUNDRY_AGENT_REASONING_EFFORT")
    agent = Agent(
        agent_client,
        name=agent_name,
        id=agent_id,
        instructions=instructions,
        tools=tools,
        context_providers=[compaction_provider],
        middleware=[ToolOutputSecurityMiddleware()],
        default_options={
            "reasoning": {"effort": reasoning_effort},
            "max_tool_calls": MAX_TOOL_CALLS_PER_TURN,
        },
    )

    server = ResponsesHostServer(agent)

    await server.run_async()


if __name__ == "__main__":
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s %(levelname)s %(name)s: %(message)s",
        stream=sys.stdout,
    )
    for noisy_logger in [
        "azure.core.pipeline.policies.http_logging_policy",
        "azure.cosmos._cosmos_http_logging_policy",
        "azure.monitor.opentelemetry.exporter",
        "uvicorn.access",
        "uvicorn",
    ]:
        logging.getLogger(noisy_logger).setLevel(logging.WARNING)

    # Re-use the project_client init pattern so cold-start failures surface
    # before the server starts accepting traffic.
    _ = get_project_client

    logger.info("Chatbot evolution agent container starting...")
    asyncio.run(main())
