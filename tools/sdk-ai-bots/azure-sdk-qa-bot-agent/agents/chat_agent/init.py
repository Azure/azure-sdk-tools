"""Azure SDK QA Bot — Hosted Chat Agent.

Self-contained entrypoint for the hosted agent container.
Runs as an HTTP server on port 8088 using the Responses protocol.
Deployed to Microsoft Foundry as a container agent.
"""

import asyncio
import logging
import sys
from pathlib import Path

from dotenv import load_dotenv

# sys.path — add the project root so top-level packages
_project_root = str(Path(__file__).resolve().parent.parent.parent)
if _project_root not in sys.path:
    sys.path.insert(0, _project_root)

# Environment
load_dotenv(override=False)

from agent_framework import Agent
from agent_framework import SkillsProvider
from azure.ai.agentserver.agentframework import from_agent_framework

import config.app_config as app_config
from config.app_config import get as cfg
from tools.knowledge_tools import KnowledgeTools
from tools.azsdk_mcp_tools import create_azsdk_mcp_tool
from tools.github_mcp_tools import create_github_mcp_tool
from tools.skills import create_tenant_skills
from utils.azure_ai_foundry import get_agent_client

logger = logging.getLogger(__name__)

def _load_instructions(file_path: Path) -> str:
    """Load agent instructions from the instructions markdown file."""
    if not file_path.exists():
        raise FileNotFoundError(
            f"Agent instructions file not found: {file_path}"
        )
    return file_path.read_text(encoding="utf-8").strip()


async def main() -> None:
    """Start the hosted Chat Agent as an HTTP server."""
    await app_config.init()
    agent_client = get_agent_client()
    # Limit tool-call loop iterations to prevent infinite loops.
    agent_client.function_invocation_configuration["max_iterations"] = 5
    instructions = _load_instructions(Path(__file__).parent / "instruction.md")
    knowledge_tools = KnowledgeTools()
    azsdk_mcp_tool = await create_azsdk_mcp_tool()
    github_mcp_tool = await create_github_mcp_tool(agent_client)

    # Build tenant skills for progressive domain expertise disclosure
    skills = create_tenant_skills()
    skills_provider = SkillsProvider(skills=skills)

    tools = [
        knowledge_tools.search_knowledge_base,
        azsdk_mcp_tool,
        github_mcp_tool,
    ]

    agent = Agent(
        agent_client,
        name="azure-sdk-qa-bot-agent",
        instructions=instructions,
        tools=tools,
        context_providers=[skills_provider],
    )

    model = cfg("AI_FOUNDRY_AGENT_COMPLETION_MODEL")
    logger.info(f"Azure SDK QA Bot Agent running — model: {model}")

    server = from_agent_framework(agent)
    await server.run_async()


if __name__ == "__main__":
    # Logging — configure first so every subsequent step is observable.
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s %(levelname)s %(name)s: %(message)s",
        stream=sys.stdout,
    )

    logger.info("Agent container starting...")

    asyncio.run(main())
