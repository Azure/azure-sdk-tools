"""Azure MCP Server hosted agent entrypoint."""

import asyncio
import logging
import os
import sys
from pathlib import Path

import yaml
from dotenv import load_dotenv

_project_root = str(Path(__file__).resolve().parent.parent.parent)
if _project_root not in sys.path:
    sys.path.insert(0, _project_root)

load_dotenv(override=False)
os.environ.setdefault("ENABLE_SENSITIVE_DATA", "true")

from agent_framework import Agent
from agent_framework import CompactionProvider
from agent_framework import SkillsProvider
from agent_framework import ToolResultCompactionStrategy
from agent_framework_foundry_hosting import ResponsesHostServer

import config.app_config as app_config
from config.app_config import get as cfg
from skills.tenant_skills import create_tenant_skills
from tools.github_mcp_tools import create_github_mcp_tool
from tools.knowledge_tools import KnowledgeTools
from tools.web_tools import WebTools
from utils.azure_ai_foundry import get_agent_client, get_project_client
from utils.azure_memory_store import ensure_user_memory_store
from utils.memory_context_provider import MemoryContextProvider
from utils.tool_security import ToolOutputSecurityMiddleware

logger = logging.getLogger(__name__)

MAX_TOOL_CALL_ITERATIONS = 5
MAX_TOOL_CALLS_PER_TURN = 10
_background_tasks: set[asyncio.Task] = set()


def _load_instructions(file_path: Path) -> str:
    if not file_path.exists():
        raise FileNotFoundError(f"Agent instructions file not found: {file_path}")
    return file_path.read_text(encoding="utf-8").strip()


async def main() -> None:
    """Start the Azure MCP Server hosted agent."""
    await app_config.init()

    agent_client = get_agent_client()
    agent_client.function_invocation_configuration["max_iterations"] = (
        MAX_TOOL_CALL_ITERATIONS
    )

    agent_dir = Path(__file__).parent
    instructions = _load_instructions(agent_dir / "instruction.md")
    with open(agent_dir / "agent.yaml", encoding="utf-8") as config_file:
        agent_config = yaml.safe_load(config_file)
    agent_name = agent_config["name"]
    agent_version = os.environ.get("APP_VERSION")
    agent_id = f"{agent_name}:{agent_version}" if agent_version else agent_name

    project_client = get_project_client()
    knowledge_tools = KnowledgeTools()
    web_tools = WebTools()
    web_search_tool = agent_client.get_web_search_tool(search_context_size="medium")
    tools = [
        knowledge_tools.search_knowledge_base,
        web_tools.web_fetch,
        web_search_tool,
    ]

    async def _init_memory() -> None:
        try:
            await ensure_user_memory_store(project_client)
        except Exception:
            logger.exception("Memory store initialization failed, skipped")

    memory_init_task = asyncio.create_task(_init_memory())
    _background_tasks.add(memory_init_task)
    memory_init_task.add_done_callback(_background_tasks.discard)

    try:
        github_tool = await create_github_mcp_tool()
    except Exception:
        logger.exception("GitHub MCP failed to initialize, skipped")
    else:
        tools.append(github_tool)

    skills = create_tenant_skills(agent_name)
    if not skills:
        raise RuntimeError(f"No tenant skills configured for agent {agent_name!r}")

    memory_provider = MemoryContextProvider(project_client)
    compaction_provider = CompactionProvider(
        before_strategy=ToolResultCompactionStrategy(keep_last_tool_call_groups=2),
        after_strategy=ToolResultCompactionStrategy(keep_last_tool_call_groups=1),
    )

    agent = Agent(
        agent_client,
        name=agent_name,
        id=agent_id,
        instructions=instructions,
        tools=tools,
        context_providers=[
            SkillsProvider(skills),
            memory_provider,
            compaction_provider,
        ],
        middleware=[ToolOutputSecurityMiddleware()],
        default_options={
            "reasoning": {"effort": cfg("AI_FOUNDRY_AGENT_REASONING_EFFORT")},
            "max_tool_calls": MAX_TOOL_CALLS_PER_TURN,
            "include": ["web_search_call.action.sources"],
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
    for noisy_logger, level in [
        ("azure.core.pipeline.policies.http_logging_policy", logging.WARNING),
        ("azure.cosmos._cosmos_http_logging_policy", logging.WARNING),
        ("azure.monitor.opentelemetry.exporter", logging.WARNING),
        ("uvicorn.access", logging.WARNING),
        ("uvicorn", logging.WARNING),
        (
            "microsoft.opentelemetry.a365.core.exporters.agent365_exporter",
            logging.CRITICAL,
        ),
        ("microsoft.opentelemetry._distro", logging.ERROR),
    ]:
        logging.getLogger(noisy_logger).setLevel(level)

    logger.info("Azure MCP Server agent container starting...")
    asyncio.run(main())
