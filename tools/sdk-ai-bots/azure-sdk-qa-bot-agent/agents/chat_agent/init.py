"""Azure SDK QA Bot — Hosted Chat Agent.

Self-contained entrypoint for the hosted agent container.
Runs as an HTTP server on port 8088 using the Responses protocol.
Deployed to Microsoft Foundry as a container agent.
"""

import asyncio
import logging
import sys
from pathlib import Path

# ---------------------------------------------------------------------------
# 1. Logging — configure first so every subsequent step is observable.
# ---------------------------------------------------------------------------
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s %(levelname)s %(name)s: %(message)s",
    stream=sys.stdout,
)
logger = logging.getLogger(__name__)
logger.info("Agent container starting...")

# ---------------------------------------------------------------------------
# 2. sys.path — add the project root so top-level packages (config, tools,
#    utils, …) are importable both locally (via agentdev) and in the
#    container (where cwd is /app).
# ---------------------------------------------------------------------------
_project_root = str(Path(__file__).resolve().parent.parent.parent)
if _project_root not in sys.path:
    sys.path.insert(0, _project_root)

# ---------------------------------------------------------------------------
# 3. Environment — load .env before config/app_config.py is imported,
#    since it reads AZURE_APPCONFIG_ENDPOINT at module level.
# ---------------------------------------------------------------------------
from dotenv import load_dotenv  # noqa: E402

load_dotenv(override=False)

# ---------------------------------------------------------------------------
# 4. Application imports — these depend on steps 2 & 3 above.
# ---------------------------------------------------------------------------
from agent_framework import Agent  # noqa: E402
from agent_framework.azure import AzureAIAgentClient  # noqa: E402
from azure.ai.agentserver.agentframework import from_agent_framework  # noqa: E402
from azure.identity.aio import DefaultAzureCredential  # noqa: E402

from config.app_config import get as cfg  # noqa: E402
from tools.knowledge_tools import KnowledgeTools  # noqa: E402
from tools.pipeline_tools import PipelineTools  # noqa: E402

logger.info("Configuration loaded successfully")

def _load_instructions(file_path: Path) -> str:
    """Load agent instructions from the instructions markdown file."""
    if not file_path.exists():
        raise FileNotFoundError(
            f"Agent instructions file not found: {file_path}"
        )
    return file_path.read_text(encoding="utf-8").strip()


async def main() -> None:
    """Start the hosted Chat Agent as an HTTP server."""
    instructions = _load_instructions(Path(__file__).parent / "instruction.md")

    knowledge_tools = KnowledgeTools()
    pipeline_tools = PipelineTools()

    async with (
        DefaultAzureCredential() as credential,
        AzureAIAgentClient(
            project_endpoint=cfg("AI_FOUNDRY_PROJECT_ENDPOINT"),
            model_deployment_name=cfg("AI_FOUNDRY_AGENT_COMPLETION_MODEL", "gpt-5.1"),
            credential=credential,
        ) as client,
    ):
        agent = Agent(
            client,
            name="azure-sdk-qa-bot-agent",
            instructions=instructions,
            tools=[
                knowledge_tools.search_knowledge_base,
                knowledge_tools.get_document_context,
                knowledge_tools.list_knowledge_sources,
                pipeline_tools.analyze_pipeline_failure,
            ],
        )

        model = cfg("AI_FOUNDRY_AGENT_COMPLETION_MODEL", "gpt-5.1")
        logger.info(f"Azure SDK QA Bot Agent running — model: {model}")
        logger.info("Listening on http://localhost:8088")

        server = from_agent_framework(agent)
        await server.run_async()


if __name__ == "__main__":
    asyncio.run(main())
