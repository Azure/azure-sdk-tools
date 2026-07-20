"""TypeSpec Authoring Skill — Feedback Agent (hosted).

Self-contained entrypoint for the hosted feedback agent container. Runs as an
HTTP server using the Responses protocol and is deployed to Microsoft Foundry as
a container agent.

Its single responsibility is to **collect anonymized user telemetry** from real
azure-typespec-author sessions and persist it (to Application Insights) so the
Self-Evolving Agent can mine it for skill improvements.

Mirrors the structure of the sibling ``agent/`` (self-evolving agent) but with a
telemetry-collection toolset instead of the skill-editing one.

Required environment variables:
    AI_FOUNDRY_PROJECT_ENDPOINT   https://{account}.services.ai.azure.com/api/projects/{project}
    AI_FOUNDRY_AGENT_MODEL        model deployment name, e.g. gpt-5.6-sol
Optional:
    APP_VERSION                   agent version, appended to the id for tracing
    AI_FOUNDRY_AGENT_REASONING_EFFORT   low | medium | high (default: medium)
"""

import asyncio
import logging
import os
import sys
from pathlib import Path

import yaml
from dotenv import load_dotenv

_AGENT_DIR = Path(__file__).resolve().parent
if str(_AGENT_DIR) not in sys.path:
    sys.path.insert(0, str(_AGENT_DIR))

load_dotenv(override=False)

# Capture prompt/response content in agent traces (visible in Foundry Traces tab
# and Application Insights). Must be set before importing agent_framework.
os.environ.setdefault("ENABLE_SENSITIVE_DATA", "true")

from agent_framework import Agent
from agent_framework.foundry import FoundryChatClient
from agent_framework_foundry_hosting import ResponsesHostServer
from azure.identity import DefaultAzureCredential

from tools.feedback_tools import (
    acknowledge_feedback,
    record_session_telemetry,
)

logger = logging.getLogger(__name__)

MAX_TOOL_CALL_ITERATIONS = 8
MAX_TOOL_CALLS_PER_TURN = 12


def _load_instructions() -> str:
    path = _AGENT_DIR / "instruction.md"
    if not path.exists():
        raise FileNotFoundError(f"Agent instructions file not found: {path}")
    return path.read_text(encoding="utf-8").strip()


def _require(name: str) -> str:
    value = os.environ.get(name)
    if not value:
        raise RuntimeError(f"Required environment variable {name} is not set.")
    return value


async def main() -> None:
    """Start the hosted Feedback Agent as an HTTP server."""
    project_endpoint = _require("AI_FOUNDRY_PROJECT_ENDPOINT")
    model = _require("AI_FOUNDRY_AGENT_MODEL")
    reasoning_effort = os.environ.get("AI_FOUNDRY_AGENT_REASONING_EFFORT", "medium")

    with open(_AGENT_DIR / "agent.yaml", encoding="utf-8") as f:
        agent_config = yaml.safe_load(f)
    agent_name = agent_config["name"]
    agent_version = os.environ.get("APP_VERSION")
    agent_id = f"{agent_name}:{agent_version}" if agent_version else agent_name

    agent_client = FoundryChatClient(
        project_endpoint=project_endpoint,
        model=model,
        credential=DefaultAzureCredential(),
    )
    agent_client.function_invocation_configuration["max_iterations"] = (
        MAX_TOOL_CALL_ITERATIONS
    )

    tools = [
        record_session_telemetry,
        acknowledge_feedback,
    ]

    agent = Agent(
        agent_client,
        name=agent_name,
        id=agent_id,
        instructions=_load_instructions(),
        tools=tools,
        default_options={
            "reasoning": {"effort": reasoning_effort},
            "max_tool_calls": MAX_TOOL_CALLS_PER_TURN,
        },
    )

    logger.info("Starting hosted feedback agent '%s' (model=%s)", agent_id, model)
    server = ResponsesHostServer(agent)
    await server.run_async()


if __name__ == "__main__":
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s %(levelname)s %(name)s: %(message)s",
        stream=sys.stdout,
    )
    for noisy, level in [
        ("azure.core.pipeline.policies.http_logging_policy", logging.WARNING),
        ("azure.monitor.opentelemetry.exporter", logging.WARNING),
        ("uvicorn.access", logging.WARNING),
        ("uvicorn", logging.WARNING),
    ]:
        logging.getLogger(noisy).setLevel(level)
    logger.info("Feedback agent container starting...")
    asyncio.run(main())
