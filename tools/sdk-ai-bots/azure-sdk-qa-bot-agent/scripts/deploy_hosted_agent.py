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
import json
import os
import re
import subprocess
import sys
import time
from pathlib import Path
from urllib.parse import urlparse

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


def _has_project_user_assigned_identities(project_resource_id: str) -> bool:
    """Check whether the AI project currently has any user-assigned identities."""
    print("Checking for user-assigned identities on project...")
    result = _run_quiet(
        [
            "az",
            "resource",
            "show",
            "--ids",
            project_resource_id,
            "--query",
            "identity.userAssignedIdentities",
            "-o",
            "json",
        ],
    )
    if result.returncode != 0 or not result.stdout.strip():
        return False
    try:
        data = json.loads(result.stdout)
        return isinstance(data, dict) and len(data) > 0
    except (json.JSONDecodeError, TypeError):
        return False


def _resolve_umi_resource_ids() -> list[str]:
    """Resolve UMI ARM resource IDs from client IDs in environment variables."""
    client_ids = []
    for var in ("UMI_BACKEND_CLIENT_ID", "UMI_FRONTEND_CLIENT_ID"):
        cid = os.environ.get(var)
        if cid:
            client_ids.append((var, cid))
        else:
            print(f"  WARNING: {var} not set — skipping")

    resource_ids = []
    for var, cid in client_ids:
        result = _run_quiet(
            [
                "az",
                "identity",
                "list",
                "--query",
                f"[?clientId=='{cid}'].id",
                "-o",
                "tsv",
            ],
        )
        if result.returncode != 0 or not result.stdout.strip():
            print(
                f"  WARNING: Could not resolve resource ID for {var} (clientId={cid})"
            )
            continue
        rid = result.stdout.strip().splitlines()[0]
        print(f"  Resolved {var} → {rid}")
        resource_ids.append(rid)

    return resource_ids


def _set_project_identity_type(project_resource_id: str, identity_type: str) -> None:
    """Set the project identity type (e.g. 'SystemAssigned' or 'SystemAssigned, UserAssigned')."""
    print(f"  Setting project identity type to: {identity_type}")
    if identity_type == "SystemAssigned":
        identity_payload = json.dumps(
            {
                "type": "SystemAssigned",
                "userAssignedIdentities": None,
            }
        )
        _run(
            [
                "az",
                "resource",
                "update",
                "--ids",
                project_resource_id,
                "--set",
                f"identity={identity_payload}",
            ],
        )
        return

    _run(
        [
            "az",
            "resource",
            "update",
            "--ids",
            project_resource_id,
            "--set",
            f"identity.type={identity_type}",
        ],
    )


def _restore_project_user_assigned_identities(
    project_resource_id: str,
    identity_ids: list[str],
) -> None:
    """Restore user-assigned identities on the AI project."""
    # Build the identity body: {"type": "SystemAssigned, UserAssigned", "userAssignedIdentities": {"<id>": {}, ...}}
    umi_dict = {uid: {} for uid in identity_ids}
    identity_payload = json.dumps(
        {
            "type": "SystemAssigned, UserAssigned",
            "userAssignedIdentities": umi_dict,
        }
    )
    print(f"  Restoring {len(identity_ids)} user-assigned identities...")
    _run(
        [
            "az",
            "resource",
            "update",
            "--ids",
            project_resource_id,
            "--set",
            f"identity={identity_payload}",
        ],
    )


def _wait_for_agent_running(
    account_name: str,
    project_name: str,
    agent_name: str,
    agent_version: str,
    timeout: int = 300,
    poll_interval: int = 10,
) -> bool:
    """Poll until the hosted agent reaches a running state."""
    print(
        f"Waiting for agent {agent_name} version {agent_version} to be running (timeout {timeout}s)..."
    )
    deadline = time.time() + timeout
    while time.time() < deadline:
        result = _run_quiet(
            [
                "az",
                "cognitiveservices",
                "agent",
                "status",
                "--account-name",
                account_name,
                "--project-name",
                project_name,
                "--name",
                agent_name,
                "--agent-version",
                agent_version,
                "-o",
                "json",
            ],
        )
        if result.returncode != 0:
            # Command failed — likely project/account not found or not authenticated
            error_msg = (
                result.stderr.strip() if result.stderr else result.stdout.strip()
            )
            print(f"  WARNING: Failed to check agent status: {error_msg}")
            print(
                f"  Ensure you are logged in to the correct subscription and account/project names are correct."
            )
            print(
                f"  Account: {account_name}, Project: {project_name}, Agent: {agent_name}"
            )
            return False
        stdout = result.stdout
        json_start = stdout.find("{")
        if json_start > 0:
            stdout = stdout[json_start:]
        try:
            payload = json.loads(stdout)
        except json.JSONDecodeError:
            print(
                f"  WARNING: Could not parse agent status payload: {result.stdout.strip()[:200]}"
            )
            return False

        status = str(payload.get("status", "")).lower()
        container = payload.get("container") or {}
        container_state = str(container.get("state", "")).lower()
        provisioning_state = str(container.get("provisioning_state", "")).lower()

        if status in ("running", "succeeded"):
            print(
                f"  Agent status is {status} (container={container_state}, provisioning={provisioning_state})."
            )
            return True
        if status in ("failed", "error", "stopped") or provisioning_state in (
            "failed",
            "error",
        ):
            print(
                "  Agent entered a terminal non-running state "
                f"(status={status}, container={container_state}, provisioning={provisioning_state})."
            )
            return False
        print(
            "  Status: "
            f"status={status}, container={container_state}, provisioning={provisioning_state} "
            f"— retrying in {poll_interval}s..."
        )
        time.sleep(poll_interval)
    print(f"  Timed out after {timeout}s.")
    return False


def _wait_for_agent_not_stopping(
    account_name: str,
    project_name: str,
    agent_name: str,
    agent_version: str,
    timeout: int = 300,
    poll_interval: int = 10,
) -> bool:
    """Wait until the hosted agent status is no longer 'Stopping'."""
    print(
        f"Waiting for agent {agent_name} version {agent_version} to leave Stopping state..."
    )
    deadline = time.time() + timeout
    while time.time() < deadline:
        result = _run_quiet(
            [
                "az",
                "cognitiveservices",
                "agent",
                "status",
                "--account-name",
                account_name,
                "--project-name",
                project_name,
                "--name",
                agent_name,
                "--agent-version",
                agent_version,
                "--query",
                "status",
                "-o",
                "tsv",
            ],
        )
        if result.returncode != 0:
            error_msg = (
                result.stderr.strip() if result.stderr else result.stdout.strip()
            )
            print(f"  WARNING: Failed to query agent status while waiting: {error_msg}")
            return False

        status = result.stdout.strip().lower()
        if status != "stopping":
            print(f"  Agent status is now '{status}'.")
            return True

        print(f"  Status is still 'stopping' — retrying in {poll_interval}s...")
        time.sleep(poll_interval)

    print(f"  Timed out after {timeout}s waiting for Stopping to finish.")
    return False


def _start_agent(
    account_name: str, project_name: str, agent_name: str, agent_version: str
) -> None:
    print(f"Starting agent: {agent_name} version {agent_version}")

    # Always ensure the deployment is not in Stopping before start.
    if not _wait_for_agent_not_stopping(
        account_name, project_name, agent_name, agent_version
    ):
        raise RuntimeError(
            "Failed to start agent: deployment remained in Stopping state."
        )

    start_cmd = [
        "az",
        "cognitiveservices",
        "agent",
        "start",
        "--account-name",
        account_name,
        "--project-name",
        project_name,
        "--name",
        agent_name,
        "--agent-version",
        agent_version,
    ]

    result = _run_quiet(start_cmd)
    if result.returncode == 0:
        return

    error_msg = (result.stderr or result.stdout or "").strip()
    raise RuntimeError(f"Failed to start agent: {error_msg}")


def _stop_agent(
    account_name: str, project_name: str, agent_name: str, agent_version: str
) -> None:
    print(f"Stopping agent: {agent_name} version {agent_version}")
    _run(
        [
            "az",
            "cognitiveservices",
            "agent",
            "stop",
            "--account-name",
            account_name,
            "--project-name",
            project_name,
            "--name",
            agent_name,
            "--agent-version",
            agent_version,
        ],
    )


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

    parsed = urlparse(project_endpoint)
    account_name = parsed.hostname.split(".")[0] if parsed.hostname else ""
    path_parts = [p for p in parsed.path.split("/") if p]
    project_name = ""
    if len(path_parts) >= 3 and path_parts[-2] == "projects":
        project_name = path_parts[-1]
    if not account_name or not project_name:
        sys.exit(
            "ERROR: Could not extract account name and project name from "
            f"AI_FOUNDRY_PROJECT_ENDPOINT: {project_endpoint}"
        )

    print(f"Deployment config:")
    print(f"  Account: {account_name}")
    print(f"  Project: {project_name}")
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

    # Prefer env var from pipeline; fall back to az CLI for local dev.
    project_resource_id = os.environ.get("AI_FOUNDRY_PROJECT_RESOURCE_ID", "").strip()
    if not project_resource_id:
        result = _run_quiet(
            [
                "az",
                "resource",
                "list",
                "--name",
                f"{account_name}/{project_name}",
                "--resource-type",
                "Microsoft.CognitiveServices/accounts/projects",
                "--query",
                "[0].id",
                "-o",
                "tsv",
            ],
        )
        if result.returncode == 0:
            for line in result.stdout.strip().splitlines():
                line = line.strip()
                if line.startswith("/subscriptions/"):
                    project_resource_id = line
                    break
    if not project_resource_id:
        sys.exit(
            f"ERROR: Could not resolve a valid ARM resource ID for project '{project_name}' "
            f"under account '{account_name}'. "
            "Set AI_FOUNDRY_PROJECT_RESOURCE_ID env var or ensure you are logged in "
            "to the correct subscription."
        )
    print(f"  Project resource ID: {project_resource_id}")

    acr_name = registry.split(".")[0]
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

    # ── Remove user-assigned identities before deployment ──
    if _has_project_user_assigned_identities(project_resource_id):
        print("Removing user-assigned identities before deployment...")
        _set_project_identity_type(project_resource_id, "SystemAssigned")
    else:
        print("No user-assigned identities found on the project.")

    # ── Deploy ──
    print(f"Deploying: {image_name}")
    project = AIProjectClient(
        endpoint=project_endpoint,
        credential=AzureCliCredential(),
        allow_preview=True,
        headers={"Foundry-Features": "HostedAgents=V1Preview"},
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
            "AGENT_VERSION": next_version,
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
                        protocol=AgentProtocol.RESPONSES, version="v1"
                    )
                ],
                cpu="2",
                memory="4Gi",
                image=image,
                environment_variables=env_vars,
            ),
        )
        print(f"Created — agent: {agent.name}, version: {agent.version}")
        if str(agent.version) != next_version:
            print(
                f"  WARNING: Predicted version {next_version} but got {agent.version}. "
                "Traces will use the predicted version in gen_ai.agent.id."
            )
    finally:
        project.close()

    # ── Start once to let Foundry pull the container image ──
    agent_version = str(agent.version)
    _start_agent(account_name, project_name, agent.name, agent_version)

    # ── Wait for running, then restore identities from env and restart ──
    if project_resource_id:
        if _wait_for_agent_running(
            account_name, project_name, agent.name, agent_version
        ):
            umi_resource_ids = _resolve_umi_resource_ids()
            if umi_resource_ids:
                print(
                    f"Restoring {len(umi_resource_ids)} user-assigned identities from env..."
                )
                _restore_project_user_assigned_identities(
                    project_resource_id, umi_resource_ids
                )
                print("Identities restored successfully.")

                print(
                    "Restarting agent so it runs with the restored user-assigned identities..."
                )
                _stop_agent(account_name, project_name, agent.name, agent_version)
                _start_agent(account_name, project_name, agent.name, agent_version)

                if _wait_for_agent_running(
                    account_name, project_name, agent.name, agent_version
                ):
                    print(
                        f"Done — agent {agent.name} v{agent.version} is running with restored identities."
                    )
                else:
                    print(
                        "WARNING: Agent restart did not reach Running state after identities were restored."
                    )
            else:
                print(
                    "WARNING: No UMI resource IDs resolved from env. "
                    "Set UMI_BACKEND_CLIENT_ID and UMI_FRONTEND_CLIENT_ID in .env."
                )
        else:
            print(
                "WARNING: Agent did not reach Running state. Identities NOT restored."
            )
    else:
        print(f"Done — agent {agent.name} v{agent.version} is starting.")


if __name__ == "__main__":
    try:
        main()
    finally:
        asyncio.run(close_credential())
