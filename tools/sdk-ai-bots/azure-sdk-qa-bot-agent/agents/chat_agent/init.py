"""Azure SDK QA Bot — Hosted Chat Agent.

Self-contained entrypoint for the hosted agent container.
Runs as an HTTP server on port 8088 using the Responses protocol.
Deployed to Microsoft Foundry as a container agent.
"""

import asyncio
import logging
import os
import sys
from pathlib import Path

import yaml
from dotenv import load_dotenv

# sys.path — add the project root so top-level packages
_project_root = str(Path(__file__).resolve().parent.parent.parent)
if _project_root not in sys.path:
    sys.path.insert(0, _project_root)

# Environment
load_dotenv(override=False)

from agent_framework import Agent
from agent_framework import SkillsProvider
from agent_framework.azure import AzureOpenAIResponsesClient
from agent_framework.openai._responses_client import ReasoningOptions
from azure.ai.agentserver.agentframework import from_agent_framework
from opentelemetry import trace as otel_trace
from opentelemetry._logs import get_logger_provider
from opentelemetry.sdk._logs import LoggingHandler

import config.app_config as app_config
from config.app_config import get as cfg
from tools.knowledge_tools import KnowledgeTools
from tools.web_tools import WebTools
from tools.ado_mcp_tools import create_ado_mcp_tool
from tools.azsdk_mcp_tools import create_azsdk_mcp_tool
from tools.github_mcp_tools import create_github_mcp_tool
from skills.tenant_skills import create_tenant_skills
from utils.azure_ai_foundry import (
    FoundryAgentSpanEnricher,
    SpanAttributeTruncator,
    get_agent_client,
    get_project_client,
)
from utils.azure_memory_store import (
    ensure_user_memory_store,
)
from utils.memory_context_provider import MemoryContextProvider

logger = logging.getLogger(__name__)


def _load_instructions(file_path: Path) -> str:
    """Load agent instructions from the instructions markdown file."""
    if not file_path.exists():
        raise FileNotFoundError(f"Agent instructions file not found: {file_path}")
    return file_path.read_text(encoding="utf-8").strip()


async def main() -> None:
    """Start the hosted Chat Agent as an HTTP server."""
    await app_config.init()
    agent_client = get_agent_client()
    # Limit tool-call loop iterations to prevent infinite loops.
    agent_client.function_invocation_configuration["max_iterations"] = 3
    agent_dir = Path(__file__).parent
    instructions = _load_instructions(agent_dir / "instruction.md")
    with open(agent_dir / "agent.yaml", encoding="utf-8") as f:
        agent_config = yaml.safe_load(f)
    agent_name = agent_config["name"]

    # Append agent version so Foundry can filter traces per version.
    agent_version = os.environ.get("AGENT_VERSION")
    agent_id = f"{agent_name}:{agent_version}" if agent_version else agent_name
    project_client = get_project_client()

    # Memory stores — ensure user store exists and create context provider
    await ensure_user_memory_store(project_client)
    memory_provider = MemoryContextProvider(project_client)

    # Init Tools
    knowledge_tools = KnowledgeTools()
    web_tools = WebTools()
    ado_mcp_tool = await create_ado_mcp_tool()
    azsdk_mcp_tool = await create_azsdk_mcp_tool()
    github_mcp_tool = await create_github_mcp_tool(agent_client)
    web_search_tool = agent_client.get_web_search_tool(
        search_context_size="medium",
    )

    tools = [
        knowledge_tools.search_knowledge_base,
        web_tools.web_fetch,
        ado_mcp_tool,
        azsdk_mcp_tool,
        github_mcp_tool,
        web_search_tool,
    ]

    # Init Skills
    skills = create_tenant_skills()
    skills_provider = SkillsProvider(skills=skills)

    reasoning_effort = cfg("AI_FOUNDRY_AGENT_REASONING_EFFORT")
    agent = Agent(
        agent_client,
        name=agent_name,
        id=agent_id,
        instructions=instructions,
        tools=tools,
        context_providers=[skills_provider, memory_provider],
        default_options={
            "reasoning": ReasoningOptions(effort=reasoning_effort),
            "truncation": "auto",
            "include": ["web_search_call.action.sources"],
        },
    )

    server = from_agent_framework(agent)

    # Init TracerProvider
    server.init_tracing()
    foundry_project_id = os.environ.get("AI_FOUNDRY_PROJECT_RESOURCE_ID", "")
    provider = otel_trace.get_tracer_provider()
    if hasattr(provider, "add_span_processor"):
        # Truncate oversized span attributes so App Insights doesn't
        # silently drop spans that exceed the 65 KB item limit.
        provider.add_span_processor(SpanAttributeTruncator())
        if foundry_project_id:
            provider.add_span_processor(
                FoundryAgentSpanEnricher(foundry_project_id, agent_name, agent_id)
            )

    # Init LoggerProvider
    try:
        otel_log_handler = LoggingHandler(
            level=logging.INFO,
            logger_provider=get_logger_provider(),
        )
        logging.getLogger().addHandler(otel_log_handler)
        logger.info(
            "OTel logging bridge attached — Python logs will export to Application Insights"
        )
    except Exception as exc:
        logger.warning("Failed to attach OTel logging bridge: %s", exc)

    await server.run_async()


if __name__ == "__main__":
    # Logging — configure first so every subsequent step is observable.
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s %(levelname)s %(name)s: %(message)s",
        stream=sys.stdout,
    )
    # Silence noisy loggers that flood container logs.
    for noisy_logger in [
        "azure.core.pipeline.policies.http_logging_policy",  # HTTP request/response headers
        "azure.cosmos._cosmos_http_logging_policy",  # Cosmos DB request/response logging
        "azure.monitor.opentelemetry.exporter",  # telemetry transmission
        "uvicorn.access",  # health-probe GET /readiness /liveness
        "uvicorn",  # uvicorn root logger (also emits access logs)
    ]:
        logging.getLogger(noisy_logger).setLevel(logging.WARNING)

    logger.info("Agent container starting...")

    asyncio.run(main())
