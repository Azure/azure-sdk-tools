"""Azure SDK QA Bot — Hosted Feedback Agent.

Self-contained entrypoint for the hosted feedback agent container.
Runs as an HTTP server on port 8088 using the Responses protocol.
Deployed to Microsoft Foundry as a container agent.

The Feedback Agent is a KB-quality analyst — it analyzes past chat turns
that received negative feedback (explicit thumbs-down or implicit expert
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
from opentelemetry import trace as otel_trace

import config.app_config as app_config
from config.app_config import get as cfg
from tools.feedback_tools import FeedbackTools
from tools.knowledge_tools import KnowledgeTools
from tools.web_tools import WebTools
from utils.azure_ai_foundry import (
    FoundryAgentSpanEnricher,
    SpanAttributeTruncator,
    get_agent_client,
    get_project_client,
)

logger = logging.getLogger(__name__)

# -- Agent configuration constants ----------------------------------------
# Feedback workflow is more deliberate than chat: allow a few more
# tool-call iterations so the agent can fetch trace, fetch conversation,
# re-search, fetch a source URL, and file an issue in one turn.
MAX_TOOL_CALL_ITERATIONS = 6
MAX_TOOL_CALLS_PER_TURN = 8


def _load_instructions(file_path: Path) -> str:
    if not file_path.exists():
        raise FileNotFoundError(f"Agent instructions file not found: {file_path}")
    return file_path.read_text(encoding="utf-8").strip()


async def main() -> None:
    """Start the hosted Feedback Agent as an HTTP server."""
    await app_config.init()

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
    feedback_tools = FeedbackTools()
    knowledge_tools = KnowledgeTools()
    web_tools = WebTools()

    tools = [
        feedback_tools.fetch_chat_trace,
        feedback_tools.fetch_conversation,
        feedback_tools.resolve_kb_target,
        feedback_tools.create_kb_gap_issue,
        knowledge_tools.search_knowledge_base,
        web_tools.web_fetch,
    ]

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
        default_options={
            "reasoning": {"effort": reasoning_effort},
            "max_tool_calls": MAX_TOOL_CALLS_PER_TURN,
        },
    )

    server = ResponsesHostServer(agent)

    # OTel enrichment — mirror chat_agent so traces are filterable per agent.
    foundry_project_id = os.environ.get("AI_FOUNDRY_PROJECT_RESOURCE_ID", "")
    provider = otel_trace.get_tracer_provider()
    if hasattr(provider, "add_span_processor"):
        provider.add_span_processor(SpanAttributeTruncator())
        if foundry_project_id:
            provider.add_span_processor(
                FoundryAgentSpanEnricher(foundry_project_id, agent_name, agent_id)
            )

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

    logger.info("Feedback agent container starting...")
    asyncio.run(main())
