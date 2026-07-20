"""TypeSpec Authoring Skill — Self-Evolving Agent (hosted).

Self-contained entrypoint for the hosted agent container. Runs as an HTTP
server using the Responses protocol and is deployed to Microsoft Foundry as a
container agent.

Mirrors the structure of tools/sdk-ai-bots/azure-sdk-qa-bot-agent/agents/chat_agent
but is self-contained: configuration is read from environment variables instead
of Azure App Configuration.

Required environment variables:
    AI_FOUNDRY_PROJECT_ENDPOINT   https://{account}.services.ai.azure.com/api/projects/{project}
    AI_FOUNDRY_AGENT_MODEL        model deployment name, e.g. gpt-5.4-1
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

from tools.skill_evolution_tools import (
    compute_pass_rate,
    fetch_documentation,
    open_draft_pr_if_benchmark_passed,
    read_prompt_excel,
    run_benchmark,
    summarize_benchmark_results,
)
from tools.github_tools import (
    read_repo_file,
    list_repo_dir,
    push_skill_changes,
    open_draft_pr,
    open_skill_pull_request,
    dispatch_workflow,
    get_latest_workflow_run,
    download_workflow_artifact,
)
from tools.github_mcp_tools import create_github_mcp_tool
from tools.foundry_toolbox_tools import create_foundry_toolbox_tool
from tools.ado_pipeline_tools import (
    trigger_ado_pipeline,
    get_ado_pipeline_run,
    wait_for_ado_pipeline,
    download_ado_pipeline_results,
)

logger = logging.getLogger(__name__)

MAX_TOOL_CALL_ITERATIONS = 40
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


def build_agent(
    *,
    project_endpoint: str,
    model: str,
    reasoning_effort: str = "medium",
    agent_name: str,
    agent_id: str,
    instructions: str | None = None,
) -> Agent:
    """Construct the Self-Evolving Agent (tools + options).

    Shared by ``main()`` (serves the agent over the Responses protocol) and
    ``workflow.py`` (drives the agent through the coded 5-step workflow in
    process), so both paths get the exact same tool set and configuration.
    """
    agent_client = FoundryChatClient(
        project_endpoint=project_endpoint,
        model=model,
        credential=DefaultAzureCredential(),
    )
    agent_client.function_invocation_configuration["max_iterations"] = (
        MAX_TOOL_CALL_ITERATIONS
    )

    # Built-in web search complements the deterministic fetch_documentation tool.
    web_search_tool = agent_client.get_web_search_tool(search_context_size="medium")

    # Reads go through GitHub's official MCP server (PAT from .env). It is pinned
    # read-only, so it can never open a non-draft PR. Writes stay on the custom
    # tools below, which force draft PRs. If the MCP tool can't be built (no PAT
    # or endpoint down) we fall back to the custom REST read tools.
    github_mcp_tool = create_github_mcp_tool()

    # Foundry toolbox (WorkIQ) MCP tool: reads INTERNAL documents online, including
    # private SharePoint/OneDrive Excel workbooks that read_prompt_excel cannot
    # download. Enabled when a toolbox URL/name is configured; None otherwise.
    foundry_toolbox_tool = create_foundry_toolbox_tool()

    tools = [
        # Writes (custom, remote): draft-only PR + CI dispatch. Kept as custom
        # tools so "draft PRs only" cannot be bypassed via MCP create_pull_request.
        # Gated workflow: push_skill_changes commits to a branch (no PR); a draft
        # PR is opened later only if the benchmark clears the pass-rate threshold.
        push_skill_changes,
        open_draft_pr,
        open_skill_pull_request,
        dispatch_workflow,
        # Step 2 (remote): trigger + poll + read the ADO code-quality benchmark
        # pipeline (8178). trigger returns the benchmark run link (web_url).
        trigger_ado_pipeline,
        get_ado_pipeline_run,
        wait_for_ado_pipeline,
        download_ado_pipeline_results,
        # Grounding + result summarization (mode-agnostic).
        fetch_documentation,
        read_prompt_excel,
        summarize_benchmark_results,
        compute_pass_rate,
        # GATE: open a draft PR only when the benchmark pass rate exceeds the
        # threshold (default 75%); otherwise no PR is opened.
        open_draft_pr_if_benchmark_passed,
        # Local mode only (requires a mounted repo + vally CLI).
        run_benchmark,
        web_search_tool,
    ]

    if github_mcp_tool is not None:
        # Reads via GitHub MCP (repo browse, search, Actions runs/logs).
        tools.append(github_mcp_tool)
    else:
        # Fallback: custom REST read tools when the MCP server is unavailable.
        logger.warning("GitHub MCP tool unavailable; using custom REST read tools.")
        tools.extend([
            read_repo_file,
            list_repo_dir,
            get_latest_workflow_run,
            download_workflow_artifact,
        ])

    if foundry_toolbox_tool is not None:
        # Reads internal/SharePoint docs (e.g. the telemetry Excel) online.
        tools.append(foundry_toolbox_tool)

    return Agent(
        agent_client,
        name=agent_name,
        id=agent_id,
        instructions=instructions if instructions is not None else _load_instructions(),
        tools=tools,
        default_options={
            "reasoning": {"effort": reasoning_effort},
            "max_tool_calls": MAX_TOOL_CALLS_PER_TURN,
            "include": ["web_search_call.action.sources"],
        },
    )


async def main() -> None:
    """Start the hosted Self-Evolving Agent as an HTTP server."""
    project_endpoint = _require("AI_FOUNDRY_PROJECT_ENDPOINT")
    model = _require("AI_FOUNDRY_AGENT_MODEL")
    reasoning_effort = os.environ.get("AI_FOUNDRY_AGENT_REASONING_EFFORT", "medium")

    with open(_AGENT_DIR / "agent.yaml", encoding="utf-8") as f:
        agent_config = yaml.safe_load(f)
    agent_name = agent_config["name"]
    agent_version = os.environ.get("APP_VERSION")
    agent_id = f"{agent_name}:{agent_version}" if agent_version else agent_name

    agent = build_agent(
        project_endpoint=project_endpoint,
        model=model,
        reasoning_effort=reasoning_effort,
        agent_name=agent_name,
        agent_id=agent_id,
    )

    logger.info("Starting hosted agent '%s' (model=%s)", agent_id, model)
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
    logger.info("Agent container starting...")
    asyncio.run(main())
