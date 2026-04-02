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
from opentelemetry.sdk.trace import SpanProcessor
from starlette.middleware.base import BaseHTTPMiddleware
from starlette.requests import Request as StarletteRequest
from starlette.responses import JSONResponse as StarletteJSONResponse

import config.app_config as app_config
from config.app_config import get as cfg
from tools.knowledge_tools import KnowledgeTools
from tools.ado_mcp_tools import create_ado_mcp_tool
from tools.azsdk_mcp_tools import create_azsdk_mcp_tool
from tools.github_mcp_tools import create_github_mcp_tool
from tools.skills import create_tenant_skills
from utils.azure_ai_foundry import get_agent_client

logger = logging.getLogger(__name__)


class _FoundryProjectIdProcessor(SpanProcessor):
    """Injects microsoft.foundry.project.id as a span attribute.

    The Foundry Traces tab queries customDimensions for this attribute.
    The platform's server-side trace already has it, but the Agent
    Framework trace (inside the container) does not — this processor
    fills that gap so both traces satisfy the Foundry query filters.
    """

    def __init__(self, project_id: str) -> None:
        self._project_id = project_id

    def on_start(self, span, parent_context=None) -> None:
        span.set_attribute("microsoft.foundry.project.id", self._project_id)

    def on_end(self, span) -> None:
        pass

    def shutdown(self) -> None:
        pass

    def force_flush(self, timeout_millis=None) -> bool:
        return True


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
    knowledge_tools = KnowledgeTools()
    ado_mcp_tool = await create_ado_mcp_tool()
    azsdk_mcp_tool = await create_azsdk_mcp_tool()
    github_mcp_tool = await create_github_mcp_tool(agent_client)
    web_search_tool = AzureOpenAIResponsesClient.get_web_search_tool(
        search_context_size="medium",
    )
    # Build tenant skills for progressive domain expertise disclosure
    skills = create_tenant_skills()
    skills_provider = SkillsProvider(skills=skills)

    tools = [
        knowledge_tools.search_knowledge_base,
        ado_mcp_tool,
        azsdk_mcp_tool,
        github_mcp_tool,
        web_search_tool,
    ]

    reasoning_effort = cfg("AI_FOUNDRY_AGENT_REASONING_EFFORT")
    agent = Agent(
        agent_client,
        name=agent_name,
        id=agent_id,
        instructions=instructions,
        tools=tools,
        context_providers=[skills_provider],
        default_options={
            "reasoning": ReasoningOptions(effort=reasoning_effort),
            "truncation": "auto",
        },
    )

    server = from_agent_framework(agent)

    # Let the agent-server create the TracerProvider & App Insights exporter.
    server.init_tracing()

    # Inject microsoft.foundry.project.id as a span attribute so the
    # Foundry Traces tab can find these spans.  Foundry injects
    # AGENT_PROJECT_NAME into the container env; fall back to our own var.
    foundry_project_id = os.environ.get("AI_FOUNDRY_PROJECT_RESOURCE_ID", "")
    if foundry_project_id:
        provider = otel_trace.get_tracer_provider()
        if hasattr(provider, "add_span_processor"):
            provider.add_span_processor(_FoundryProjectIdProcessor(foundry_project_id))

    # run_async() calls init_tracing() again — it's a no-op because the
    # provider is already configured (_executed_setup=True).
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
        "azure.monitor.opentelemetry.exporter",  # telemetry transmission
        "httpx",  # outgoing HTTP to OpenAI
        "uvicorn.access",  # health-probe GET /readiness /liveness
        "agent_framework",  # full prompt + message dumps
    ]:
        logging.getLogger(noisy_logger).setLevel(logging.WARNING)

    logger.info("Agent container starting...")

    asyncio.run(main())
