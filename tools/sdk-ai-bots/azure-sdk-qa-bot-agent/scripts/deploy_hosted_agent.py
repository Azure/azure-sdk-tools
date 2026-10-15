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
    ContainerConfiguration,
    HostedAgentDefinition,
    ProtocolVersionRecord,
    RaiConfig,
)
from azure.identity import AzureCliCredential
from azure.appconfiguration import AzureAppConfigurationClient, ConfigurationSetting
from azure.core import MatchConditions
from azure.core.exceptions import ResourceNotFoundError, ResourceExistsError, ResourceModifiedError

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

    Uses the SDK (``project.agents.get_version``) instead of raw ``az rest`` so that
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
        info = project.agents.get_version(agent_name=agent_name, agent_version=agent_version)
        status = ""
        for attr in ("provisioning_state", "provisioningState", "status"):
            val = getattr(info, attr, None)
            if val:
                status = str(val).lower().rsplit(".", 1)[-1]
                break
        if status in ("active", "succeeded"):
            print(f"  Version status is '{status}'.")
            return True
        if status in ("failed", "error", "deleting", "deleted", "canceled", "cancelled"):
            print(f"  Version entered terminal state: '{status}'.")
            return False
        print(f"  Status: {status or 'unknown'} — retrying in {poll_interval}s...")

        time.sleep(poll_interval)

    print(f"  Timed out after {timeout}s.")
    return False


def _publish_version(
    client: AzureAppConfigurationClient, key: str, version: str
) -> None:
    """Use ETags so a slower deployment cannot overwrite a newer active version."""
    for _ in range(5):
        try:
            current = client.get_configuration_setting(key=key)
        except ResourceNotFoundError:
            current = None
        try:
            if current is None:
                client.add_configuration_setting(ConfigurationSetting(key=key, value=version))
            else:
                if int(current.value or "") >= int(version):
                    print(f"Registry {key} already points to version {current.value}.")
                    return
                current.value = version
                client.set_configuration_setting(current, match_condition=MatchConditions.IfNotModified)
            print(f"Registered {key} -> version {version}.")
            return
        except (ResourceExistsError, ResourceModifiedError):
            continue
    raise RuntimeError("Agent activated, but branch mapping publication conflicted repeatedly.")


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

    branch = None
    if os.environ.get("AI_FOUNDRY_AGENT_ROUTING") == "enable":
        branch = os.environ["BUILD_SOURCEBRANCH"].removeprefix("refs/heads/")

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
    candidate_appconfig_endpoint = os.environ.get("CANDIDATE_APPCONFIG_ENDPOINT")

    if not registry:
        sys.exit("ERROR: ACR_LOGIN_SERVER not found in App Configuration")
    if not project_endpoint:
        sys.exit("ERROR: AI_FOUNDRY_PROJECT_ENDPOINT not found in App Configuration")
    if not appconfig_endpoint:
        sys.exit(
            "ERROR: AZURE_APPCONFIG_ENDPOINT not set in .env or --appconfig-endpoint"
        )
    if (
        args.agent_name == "chatbot_evolution_agent"
        and not candidate_appconfig_endpoint
    ):
        sys.exit("ERROR: CANDIDATE_APPCONFIG_ENDPOINT not set in the environment")

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
    credential = AzureCliCredential()
    project = AIProjectClient(
        endpoint=project_endpoint,
        credential=credential,
        allow_preview=True,
    )
    try:
        # Predict the next version number so the container can include it
        # in telemetry (gen_ai.agent.id = "name:version").

        try:
            existing = project.agents.get(image_name)
            latest_version = int(existing.versions.latest.version)
        except ResourceNotFoundError:
            latest_version = 0
        next_version = str(latest_version + 1)

        env_vars = {
            "AZURE_APPCONFIG_ENDPOINT": appconfig_endpoint,
            "ENABLE_INSTRUMENTATION": "true",
            "APP_VERSION": next_version,
        }
        if candidate_appconfig_endpoint:
            env_vars["CANDIDATE_APPCONFIG_ENDPOINT"] = candidate_appconfig_endpoint

        # Ensure Content-safety guardrail.
        rai_policy_id = os.environ.get("AI_FOUNDRY_RAI_POLICY_ID", "")
        if not rai_policy_id:
            raise RuntimeError(
                "Cannot apply guardrail because AI_FOUNDRY_RAI_POLICY_ID is not set."
            )
        rai_config = RaiConfig(rai_policy_name=rai_policy_id)
        print(f"  Guardrail: {rai_policy_id}")

        agent = project.agents.create_version(
            agent_name=image_name,
            definition=HostedAgentDefinition(
                cpu="2",
                memory="4Gi",
                container_configuration=ContainerConfiguration(image=image),
                protocol_versions=[
                    ProtocolVersionRecord(
                        protocol=AgentProtocol.RESPONSES, version="1.0.0"
                    )
                ],
                environment_variables=env_vars,
                rai_config=rai_config,
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
            if branch is not None:
                with AzureAppConfigurationClient(
                    base_url=args.appconfig_endpoint, credential=credential
                ) as client:
                    _publish_version(
                        client, app_config.get_agent_deployment_key(branch, agent.name),
                        agent_version,
                    )
        else:
            if branch is not None:
                raise RuntimeError(
                    f"Agent {agent.name} v{agent_version} did not reach active status; "
                    "the deployment registry was not updated."
                )
            print(
                f"WARNING: Agent {agent.name} v{agent_version} did not reach active status. "
                "Check the Azure portal or run: az rest --method GET "
                f'--url "{project_endpoint}/agents/{agent.name}/versions/{agent_version}" '
                '--resource "https://ai.azure.com"'
            )
    finally:
        project.close()
        credential.close()


if __name__ == "__main__":
    try:
        main()
    finally:
        asyncio.run(close_credential())
