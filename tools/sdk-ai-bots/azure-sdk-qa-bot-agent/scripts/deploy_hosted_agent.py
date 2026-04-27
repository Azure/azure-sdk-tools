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
import re
import subprocess
import sys
import time
from pathlib import Path

os.environ.setdefault("AZURE_CORE_WELCOME_MESSAGE", "false")

from dotenv import load_dotenv

_PROJECT_DIR = Path(__file__).resolve().parent.parent

load_dotenv(_PROJECT_DIR / ".env", override=False)

if str(_PROJECT_DIR) not in sys.path:
    sys.path.insert(0, str(_PROJECT_DIR))

from azure.ai.projects import AIProjectClient
from azure.ai.projects.models import (
    AgentProtocol,
    HostedAgentDefinition,
    ProtocolVersionRecord,
)
from azure.identity import AzureCliCredential

import config.app_config as app_config
from config.app_config import get as cfg
from utils.azure_credential import close_credential


def _run(cmd: list[str], **kwargs) -> None:
    print(f"  $ {' '.join(cmd)}")
    subprocess.run(cmd, check=True, **kwargs)


def _run_quiet(cmd: list[str], **kwargs) -> subprocess.CompletedProcess:
    """Run a command and return the result without raising on failure."""
    return subprocess.run(cmd, capture_output=True, text=True, **kwargs)


def _git_short_sha() -> str:
    try:
        r = subprocess.run(
            ["git", "rev-parse", "--short", "HEAD"],
            capture_output=True,
            text=True,
            check=True,
        )
        return r.stdout.strip()
    except (subprocess.CalledProcessError, FileNotFoundError):
        return "latest"


def _wait_for_version_active(
    project: "AIProjectClient",
    agent_name: str,
    agent_version: str,
    timeout: int = 600,
    poll_interval: int = 15,
) -> bool:
    """Poll until the hosted agent version reaches 'active' status.

    Uses the SDK (``project.agents.get``) instead of raw ``az rest`` so that
    authentication, api-version, and response parsing are handled
    automatically.

    In the refreshed preview, compute lifecycle is automatic — the platform
    provisions compute when a request arrives and deprovisions it after
    inactivity.  Versions go through: creating → active (or failed).
    """
    print(
        f"Waiting for agent {agent_name} version {agent_version} to become active (timeout {timeout}s)..."
    )
    deadline = time.time() + timeout
    while time.time() < deadline:
        try:
            info = project.agents.get(agent_name)
            latest = info.versions.latest if info.versions else None
            if latest is None:
                print("  WARNING: No version information returned — retrying...")
                time.sleep(poll_interval)
                continue

            if str(latest.version) != agent_version:
                print(
                    f"  Latest version is {latest.version}, waiting for {agent_version}..."
                )
                time.sleep(poll_interval)
                continue

            # Try known attribute names for status
            status = ""
            for attr in ("provisioning_state", "provisioningState", "status"):
                val = getattr(latest, attr, None)
                if val:
                    status = str(val).lower()
                    break

            if status in ("active", "succeeded"):
                print(f"  Version status is '{status}'.")
                return True
            if status in ("failed", "error", "deleted"):
                print(f"  Version entered terminal state: '{status}'.")
                return False

            print(f"  Status: {status or 'unknown'} — retrying in {poll_interval}s...")
        except Exception as exc:
            print(f"  WARNING: Failed to check version status: {exc}")

        time.sleep(poll_interval)

    print(f"  Timed out after {timeout}s.")
    return False


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Build, push, and deploy a hosted agent."
    )
    parser.add_argument(
        "agent_name", help="Agent directory name under agents/, e.g. chat_agent"
    )
    parser.add_argument(
        "--tag", default=None, help="Image tag (default: git short SHA)"
    )
    parser.add_argument(
        "--appconfig-endpoint",
        default=os.environ.get("AZURE_APPCONFIG_ENDPOINT"),
        help="Override App Configuration endpoint",
    )
    args = parser.parse_args()

    asyncio.run(app_config.init())

    # Consume Azure CLI welcome banner before real commands.
    _run_quiet(["az", "version"])

    # Show current subscription context
    sub_result = _run_quiet(["az", "account", "show", "--query", "id", "-o", "tsv"])
    sub_id = ""
    if sub_result.returncode == 0:
        uuid_match = re.search(
            r"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}",
            sub_result.stdout,
            re.IGNORECASE,
        )
        if uuid_match:
            sub_id = uuid_match.group(0)
    if sub_id:
        print(f"Current subscription: {sub_id}")
    else:
        print(
            "WARNING: Could not determine current subscription. Ensure you are logged in: az login"
        )

    registry = cfg("ACR_LOGIN_SERVER")
    project_endpoint = cfg("AI_FOUNDRY_PROJECT_ENDPOINT")
    appconfig_endpoint = args.appconfig_endpoint

    if not registry:
        sys.exit("ERROR: ACR_LOGIN_SERVER not found in App Configuration")
    if not project_endpoint:
        sys.exit("ERROR: AI_FOUNDRY_PROJECT_ENDPOINT not found in App Configuration")
    if not appconfig_endpoint:
        sys.exit(
            "ERROR: AZURE_APPCONFIG_ENDPOINT not set in .env or --appconfig-endpoint"
        )

    print(f"Deployment config:")
    print(f"  Registry: {registry}")

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

    # Project resource ID is used as an env var inside the container for telemetry.
    project_resource_id = os.environ.get("AI_FOUNDRY_PROJECT_RESOURCE_ID", "").strip()

    acr_name = registry.split(".")[0]

    # Check if the image tag already exists in ACR
    tag_check = _run_quiet(
        [
            "az",
            "acr",
            "repository",
            "show-tags",
            "--name",
            acr_name,
            "--repository",
            image_name,
            "--output",
            "tsv",
        ],
    )
    existing_tags = (
        tag_check.stdout.strip().splitlines() if tag_check.returncode == 0 else []
    )
    if tag in existing_tags:
        print(f"Image {image} already exists in ACR — skipping build.")
    else:
        print(f"Building: {image}")
        _run(
            [
                "az",
                "acr",
                "build",
                "--registry",
                acr_name,
                "--image",
                f"{image_name}:{tag}",
                "--file",
                str(dockerfile),
                str(_PROJECT_DIR),
            ],
        )
        print(f"Image pushed: {image}")

    # ── Deploy ──
    print(f"Deploying: {image_name}")
    project = AIProjectClient(
        endpoint=project_endpoint,
        credential=AzureCliCredential(),
        allow_preview=True,
    )
    try:
        # Predict the next version number so the container can include it
        # in telemetry (gen_ai.agent.id = "name:version").

        try:
            existing = project.agents.get(image_name)
            latest_version = int(existing.versions.latest.version)
        except Exception:
            latest_version = 0
        next_version = str(latest_version + 1)

        env_vars = {
            "AZURE_APPCONFIG_ENDPOINT": appconfig_endpoint,
            "ENABLE_INSTRUMENTATION": "true",
            "APP_VERSION": next_version,
            "AI_FOUNDRY_PROJECT_RESOURCE_ID": project_resource_id,
        }
        for key in ("UMI_BACKEND_CLIENT_ID", "UMI_FRONTEND_CLIENT_ID"):
            val = os.environ.get(key, "")
            if val:
                env_vars[key] = val

        agent = project.agents.create_version(
            agent_name=image_name,
            definition=HostedAgentDefinition(
                container_protocol_versions=[
                    ProtocolVersionRecord(
                        protocol=AgentProtocol.RESPONSES, version="1.0.0"
                    )
                ],
                cpu="2",
                memory="4Gi",
                image=image,
                environment_variables=env_vars,
            ),
            metadata={"enableVnextExperience": "true"},
        )
        print(f"Created — agent: {agent.name}, version: {agent.version}")
        if str(agent.version) != next_version:
            print(
                f"  WARNING: Predicted version {next_version} but got {agent.version}. "
                "Traces will use the predicted version in gen_ai.agent.id."
            )

        # ── Wait for version to become active ──
        # In the refreshed preview, compute lifecycle is automatic — no manual
        # start/stop needed. The platform provisions compute on first request
        # and deprovisions after 15 min of inactivity.
        agent_version = str(agent.version)
        if _wait_for_version_active(project, agent.name, agent_version):
            print(f"Done — agent {agent.name} v{agent_version} is active.")
        else:
            print(
                f"WARNING: Agent {agent.name} v{agent_version} did not reach active status. "
                "Check the Azure portal or run: az rest --method GET "
                f'--url "{project_endpoint}/agents/{agent.name}/versions/{agent_version}" '
                '--resource "https://ai.azure.com"'
            )
    finally:
        project.close()


if __name__ == "__main__":
    try:
        main()
    finally:
        asyncio.run(close_credential())
