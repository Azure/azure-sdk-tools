"""Deploy a hosted agent version to Azure AI Foundry.

Builds the container image, pushes to ACR, and deploys as a hosted agent.
All config (project endpoint, ACR login server, etc.) is loaded from
Azure App Configuration. The App Configuration endpoint comes from the
.env file or can be passed as a CLI argument.

Usage:
    python scripts/deploy_hosted_agent.py chat_agent
    python scripts/deploy_hosted_agent.py chat_agent --tag v1.2.3
    python scripts/deploy_hosted_agent.py chat_agent --appconfig-endpoint https://...
"""

import argparse
import asyncio
import os
import subprocess
import sys
from pathlib import Path

from dotenv import load_dotenv

_PROJECT_DIR = Path(__file__).resolve().parent.parent

load_dotenv(_PROJECT_DIR / ".env", override=False)

# Ensure project root is on sys.path for config imports
if str(_PROJECT_DIR) not in sys.path:
    sys.path.insert(0, str(_PROJECT_DIR))

from azure.ai.projects import AIProjectClient
from azure.ai.projects.models import (
    AgentProtocol,
    HostedAgentDefinition,
    ProtocolVersionRecord,
)
from azure.identity import DefaultAzureCredential

import config.app_config as app_config
from config.app_config import get as cfg


def _run(cmd: list[str], **kwargs) -> None:
    print(f"  $ {' '.join(cmd)}")
    subprocess.run(cmd, check=True, **kwargs)


def _git_short_sha() -> str:
    try:
        r = subprocess.run(
            ["git", "rev-parse", "--short", "HEAD"],
            capture_output=True, text=True, check=True,
        )
        return r.stdout.strip()
    except (subprocess.CalledProcessError, FileNotFoundError):
        return "latest"


def main() -> None:
    parser = argparse.ArgumentParser(description="Build, push, and deploy a hosted agent.")
    parser.add_argument("agent_name", help="Agent directory name under agents/, e.g. chat_agent")
    parser.add_argument("--tag", default=None, help="Image tag (default: git short SHA)")
    parser.add_argument("--appconfig-endpoint", default=os.environ.get("AZURE_APPCONFIG_ENDPOINT"),
                        help="Override App Configuration endpoint")
    parser.add_argument("--client_id", default=os.environ.get("AZURE_CLIENT_ID"),
                        help="Override Azure client ID")
    args = parser.parse_args()

    asyncio.run(app_config.init())

    registry = cfg("ACR_LOGIN_SERVER")
    project_endpoint = cfg("AI_FOUNDRY_PROJECT_ENDPOINT")
    appconfig_endpoint = args.appconfig_endpoint
    client_id = args.client_id
    if not registry:
        sys.exit("ERROR: ACR_LOGIN_SERVER not found in App Configuration")
    if not project_endpoint:
        sys.exit("ERROR: AI_FOUNDRY_PROJECT_ENDPOINT not found in App Configuration")
    if not appconfig_endpoint:
        sys.exit("ERROR: AZURE_APPCONFIG_ENDPOINT not set in .env or --appconfig-endpoint")

    # Read agent name from agent.yaml metadata
    agent_metadata = _PROJECT_DIR / "agents" / args.agent_name / "agent.yaml"
    if not agent_metadata.exists():
        sys.exit(f"ERROR: {agent_metadata} not found")
    image_name = None
    for line in agent_metadata.read_text().splitlines():
        if line.startswith("name:"):
            image_name = line.split(":", 1)[1].strip()
            break
    if not image_name:
        sys.exit(f"ERROR: Could not read 'name' from {agent_metadata}")

    tag = args.tag or _git_short_sha()
    image = f"{registry}/{image_name}:{tag}"
    dockerfile = _PROJECT_DIR / "agents" / args.agent_name / "Dockerfile"

    # Login to ACR, build & push
    acr_name = registry.split(".")[0]
    print(f"Logging in to ACR: {acr_name}")
    _run(["az", "acr", "login", "--name", acr_name], shell=True)
    print(f"Building: {image}")
    _run(["docker", "build", "--platform", "linux/amd64", "-t", image, "-f", str(dockerfile), str(_PROJECT_DIR)])
    print(f"Pushing: {image}")
    _run(["docker", "push", image])

    # Deploy
    print(f"Deploying: {image_name}")
    project = AIProjectClient(
        endpoint=project_endpoint,
        credential=DefaultAzureCredential(),
        allow_preview=True,
        headers={"Foundry-Features": "HostedAgents=V1Preview"},
    )
    agent = project.agents.create_version(
        agent_name=image_name,
        definition=HostedAgentDefinition(
            container_protocol_versions=[
                ProtocolVersionRecord(protocol=AgentProtocol.RESPONSES, version="v1")
            ],
            cpu="1",
            memory="2Gi",
            image=image,
            environment_variables={
                "AZURE_APPCONFIG_ENDPOINT": appconfig_endpoint,
                "AZURE_CLIENT_ID": client_id,
            },
        ),
    )
    print(f"Created — agent: {agent.name}, version: {agent.version}")

    # Start the new agent version
    account_name = cfg("AI_FOUNDRY_ACCOUNT_NAME")
    project_name = cfg("AI_FOUNDRY_PROJECT")
    if not account_name or not project_name:
        sys.exit(
            "ERROR: AI_FOUNDRY_ACCOUNT_NAME and AI_FOUNDRY_PROJECT "
            "must be set in App Configuration to start the agent."
        )

    print(f"Starting agent: {agent.name} version {agent.version}")
    _run([
        "az", "cognitiveservices", "agent", "start",
        "--account-name", account_name,
        "--project-name", project_name,
        "--name", agent.name,
        "--agent-version", str(agent.version),
        "--min-replicas", "1",
        "--max-replicas", "2",
    ], shell=True)
    print(f"Done — agent {agent.name} v{agent.version} is starting.")


if __name__ == "__main__":
    main()
