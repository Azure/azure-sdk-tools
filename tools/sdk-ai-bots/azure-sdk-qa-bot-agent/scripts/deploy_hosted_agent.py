"""Deploy hosted agents and manage the Teams collection workflow.

Builds the container image, pushes to ACR, and deploys as a hosted agent.
All config (project endpoint, ACR login server, etc.) is loaded from
Azure App Configuration. The App Configuration endpoint comes from the
.env file or can be passed as a CLI argument.

Usage:
    python scripts/deploy_hosted_agent.py chat_agent
    python scripts/deploy_hosted_agent.py chat_agent --tag v1.2.3
    python scripts/deploy_hosted_agent.py chat_agent --appconfig-endpoint https://...
    python scripts/deploy_hosted_agent.py teams-collection deploy --environment dev ...
    python scripts/deploy_hosted_agent.py teams-collection routine-dispatch --project-endpoint https://...
"""

import argparse
import asyncio
import json
import os
import re
import shutil
import subprocess
import sys
import time
from collections.abc import Mapping
from pathlib import Path
from urllib.parse import parse_qs, quote, urlencode, urlsplit, urlunsplit
from uuid import UUID

os.environ.setdefault("AZURE_CORE_WELCOME_MESSAGE", "false")

from dotenv import load_dotenv
import httpx

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
from azure.identity.aio import AzureCliCredential as AsyncAzureCliCredential
from azure.core.exceptions import ResourceNotFoundError

import config.app_config as app_config
from config.app_config import get as cfg
from services.conversation_service import validate_channels
from services.teams_operations import SUMMARIZE, operation_input
from utils.azure_credential import close_credential

AGENT_NAME = "azure-sdk-teams-collection-agent"
APP_CONFIG_READER_ROLE = "App Configuration Data Reader"
LOGIC_APP_URL_KEY = "TEAMS_COLLECTION_LOGIC_APP_URL"
ROUTINES_API_VERSION = "v1"
ROUTINES_FEATURE = "Routines=V2Preview"
ROUTINE_DESCRIPTION = "Summarize stored Teams threads into reusable Q&A records."


def _resolve_command(cmd: list[str]) -> list[str]:
    if cmd and cmd[0] == "az":
        executable = shutil.which("az") or shutil.which("az.cmd")
        if executable is None:
            raise RuntimeError("Azure CLI was not found on PATH.")
        return [executable, *cmd[1:]]
    return cmd


def _utf8_environment(overrides: dict[str, str] | None = None) -> dict[str, str]:
    environment = {**os.environ, **(overrides or {})}
    environment.setdefault("PYTHONIOENCODING", "utf-8")
    environment.setdefault("PYTHONUTF8", "1")
    return environment


def _run(cmd: list[str], **kwargs) -> None:
    resolved = _resolve_command(cmd)
    environment = _utf8_environment(kwargs.pop("env", None))
    print(f"  $ {' '.join(cmd)}")
    subprocess.run(resolved, check=True, env=environment, **kwargs)


def _run_quiet(cmd: list[str], **kwargs) -> subprocess.CompletedProcess:
    """Run a command and return the result without raising on failure."""
    environment = _utf8_environment(kwargs.pop("env", None))
    return subprocess.run(
        _resolve_command(cmd),
        capture_output=True,
        text=True,
        encoding="utf-8",
        env=environment,
        **kwargs,
    )


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


def main() -> None:
    if len(sys.argv) > 1 and sys.argv[1] == "teams-collection":
        sys.exit(collection_main(sys.argv[2:]))

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
    if os.environ.get("ENABLE_AI_FOUNDRY_AGENT_ROUTING") == "true":
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
        if args.agent_name == "teams_collection_agent":
            env_vars["ENABLE_INSTRUMENTATION"] = "false"
            env_vars["ENABLE_SENSITIVE_DATA"] = "false"
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

        metadata = {"enableVnextExperience": "true"}
        if branch is not None:
            metadata["branchname"] = branch

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
            metadata=metadata,
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
            raise RuntimeError(
                f"Agent {agent.name} v{agent_version} did not reach active status. "
                "Check the Azure portal or run: az rest --method GET "
                f'--url "{project_endpoint}/agents/{agent.name}/versions/{agent_version}" '
                '--resource "https://ai.azure.com"'
            )
    finally:
        project.close()
        credential.close()


def _run_az(arguments: list[str]) -> str:
    result = _run_quiet(["az", *arguments])
    if result.returncode != 0:
        detail = (result.stderr or result.stdout).strip()
        raise RuntimeError(
            f"Azure CLI operation failed: az {' '.join(arguments[:3])}. "
            f"{detail[-2000:] or 'No diagnostic output was returned.'}"
        )
    return result.stdout.strip()


def _collector_principal_id(agent) -> str:
    identity = getattr(agent, "instance_identity", None)
    value = identity.get("principal_id") if isinstance(identity, Mapping) else getattr(identity, "principal_id", None)
    try:
        return str(UUID(value))
    except (AttributeError, TypeError, ValueError):
        raise RuntimeError(
            f"Hosted agent {AGENT_NAME!r} does not expose a valid instance principal ID."
        ) from None


def _principal_id(value: str, option: str) -> str:
    try:
        return str(UUID(value))
    except (AttributeError, TypeError, ValueError):
        raise ValueError(f"{option} must be a managed identity object ID.") from None


def _appconfig_name(endpoint: str) -> str:
    address = urlsplit(endpoint)
    suffix = ".azconfig.io"
    if (address.scheme != "https" or not address.hostname
            or not address.hostname.endswith(suffix)
            or address.username or address.password or address.port not in (None, 443)
            or address.path not in ("", "/") or address.query or address.fragment):
        raise ValueError("Expected an Azure App Configuration HTTPS endpoint.")
    return address.hostname.removesuffix(suffix)


def _sas_free_callback_url(callback_url: str) -> str:
    address = urlsplit(callback_url)
    versions = parse_qs(address.query).get("api-version", [])
    if (address.scheme != "https" or not address.hostname
            or not address.hostname.endswith(".logic.azure.com")
            or address.username or address.password or address.port not in (None, 443)
            or address.fragment or not address.path.endswith("/triggers/manual/paths/invoke")
            or len(versions) != 1 or not versions[0]):
        raise ValueError("Logic App returned an unexpected manual trigger callback URL.")
    return urlunsplit(("https", address.hostname, address.path,
                       urlencode({"api-version": versions[0]}), ""))


def _project_endpoint(appconfig_endpoint: str) -> str:
    endpoint = _run_az([
        "appconfig", "kv", "show", "--endpoint", appconfig_endpoint,
        "--auth-mode", "login", "--key", "AI_FOUNDRY_PROJECT_ENDPOINT",
        "--query", "value", "--output", "tsv",
    ])
    address = urlsplit(endpoint)
    if (address.scheme != "https" or not address.hostname
            or not address.hostname.endswith(".services.ai.azure.com")
            or not address.path.startswith("/api/projects/")
            or address.query or address.fragment):
        raise ValueError("AI_FOUNDRY_PROJECT_ENDPOINT in App Configuration is invalid.")
    return endpoint.rstrip("/")


def _resolve_collector_principal(project_endpoint: str) -> str:
    credential = AzureCliCredential()
    project = AIProjectClient(endpoint=project_endpoint, credential=credential, allow_preview=True)
    try:
        return _collector_principal_id(project.agents.get(AGENT_NAME))
    finally:
        project.close()
        credential.close()


def routine_config(config: dict) -> dict:
    routine = config.get("routine")
    if not isinstance(routine, Mapping):
        raise ValueError("No summarization routine is configured.")
    return routine


def routine_definition(config: dict) -> dict:
    routine = routine_config(config)
    if not re.fullmatch(r"teams-channel-[a-zA-Z0-9][-a-zA-Z0-9]*", routine["name"]):
        raise ValueError("Routine name must start with teams-channel-.")
    if len(routine["cron"].split()) != 5 or not routine["timeZone"]:
        raise ValueError("Routine requires a five-field cron expression and timeZone; minimum interval is five minutes.")
    return {
        "description": ROUTINE_DESCRIPTION,
        "enabled": False,
        "authorization": {"identity": "agent"},
        "triggers": {"schedule": {"type": "schedule", "cron_expression": routine["cron"],
                                  "time_zone": routine["timeZone"]}},
        "action": {"type": "invoke_agent_responses_api", "agent_name": AGENT_NAME,
                   "input": operation_input({"operation": SUMMARIZE})},
    }


def _validate_project_endpoint(endpoint: str) -> str:
    address = urlsplit(endpoint)
    if (address.scheme != "https" or not address.hostname
            or not address.hostname.endswith(".services.ai.azure.com")
            or address.username or address.password or address.port not in (None, 443)
            or not re.fullmatch(r"/api/projects/[^/]+/?", address.path)
            or address.query or address.fragment):
        raise ValueError("Expected a Foundry project endpoint in Azure public cloud.")
    return endpoint.rstrip("/")


async def routine_request(config, command, endpoint, credential, client) -> dict:
    definition = routine_definition(config)
    url = _validate_project_endpoint(endpoint) + "/routines/" + quote(routine_config(config)["name"], safe="")
    token = await credential.get_token("https://ai.azure.com/.default")
    headers = {"Authorization": f"Bearer {token.token}", "Foundry-Features": ROUTINES_FEATURE}
    query = {"api-version": ROUTINES_API_VERSION}
    existing = await client.get(url, params=query, headers=headers, follow_redirects=False)
    if existing.status_code == 200:
        if existing.json().get("action", {}).get("agent_name") != AGENT_NAME:
            raise ValueError("Refusing to modify a Routine targeting a different agent.")
    elif existing.status_code != 404 or command != "routine-create":
        raise RuntimeError(f"Routine lookup returned HTTP {existing.status_code}.")
    if command == "routine-create":
        response = await client.put(url, params=query, headers=headers, json=definition,
                                    follow_redirects=False)
    else:
        action = {"routine-enable": "enable", "routine-disable": "disable",
                  "routine-dispatch": "dispatch_async"}[command]
        response = await client.post(url + ":" + action, params=query, headers=headers,
                                     json={}, follow_redirects=False)
    if response.status_code not in (200, 201, 202):
        raise RuntimeError(f"Routine operation returned HTTP {response.status_code}; no response content was logged.")
    result = response.json()
    return {key: result[key] for key in ("name", "enabled", "dispatch_id", "task_id") if key in result}


async def _resolve_project_endpoint(arguments) -> str:
    if arguments.project_endpoint:
        return arguments.project_endpoint
    if arguments.appconfig_endpoint:
        return _project_endpoint(arguments.appconfig_endpoint)
    await app_config.init()
    return app_config.get("AI_FOUNDRY_PROJECT_ENDPOINT", "")


async def execute_routine(arguments, config) -> dict:
    from utils.azure_credential import close_credential as close_routine_credential, get_credential

    load_dotenv(_PROJECT_DIR / ".env", override=False)
    try:
        endpoint = await _resolve_project_endpoint(arguments)
        async with httpx.AsyncClient(timeout=60) as client:
            return await routine_request(config, arguments.command, endpoint, get_credential(), client)
    finally:
        await close_routine_credential()


def _deploy_infrastructure(
    environment: str,
    resource_group: str,
    appconfig_endpoint: str,
    collector_principal_id: str,
    backfill_principal_id: str,
) -> str:
    parameters_file = (_PROJECT_DIR / "pipelines" / "teams-collection"
                       / f"parameters.azure_sdk.{environment}.json")
    if not parameters_file.is_file():
        raise ValueError(f"No Teams collection parameters exist for {environment!r}.")
    workflow_resource_id = _run_az([
        "deployment", "group", "create", "--resource-group", resource_group,
        "--name", f"teams-collection-{environment}", "--mode", "Incremental",
        "--template-file", str(_PROJECT_DIR / "pipelines" / "teams-collection" / "template.json"),
        "--parameters", f"@{parameters_file}",
        f"collectorPrincipalId={collector_principal_id}",
        f"backfillPrincipalId={backfill_principal_id}",
        "--query", "properties.outputs.workflowResourceId.value", "--output", "tsv",
    ])
    if not workflow_resource_id.startswith("/subscriptions/"):
        raise RuntimeError("Teams collection deployment returned no workflow resource ID.")

    callback_url = _run_az([
        "rest", "--method", "post", "--url",
        f"https://management.azure.com{workflow_resource_id}"
        "/triggers/manual/listCallbackUrl?api-version=2019-05-01",
        "--query", "value", "--output", "tsv",
    ])
    logic_app_url = _sas_free_callback_url(callback_url)
    appconfig_name = _appconfig_name(appconfig_endpoint)
    appconfig_resource_id = _run_az([
        "appconfig", "show", "--name", appconfig_name,
        "--query", "id", "--output", "tsv",
    ])
    if not appconfig_resource_id.startswith("/subscriptions/"):
        raise RuntimeError("App Configuration lookup returned no resource ID.")
    _run_az([
        "role", "assignment", "create", "--assignee-object-id", collector_principal_id,
        "--assignee-principal-type", "ServicePrincipal", "--role", APP_CONFIG_READER_ROLE,
        "--scope", appconfig_resource_id, "--output", "none",
    ])
    _run_az([
        "appconfig", "kv", "set", "--endpoint", appconfig_endpoint,
        "--auth-mode", "login", "--key", LOGIC_APP_URL_KEY,
        "--value", logic_app_url, "--yes", "--output", "none",
    ])
    return workflow_resource_id


async def deploy_collection(arguments) -> dict:
    config = json.loads(arguments.config.read_text(encoding="utf-8"))
    validate_channels(config["channels"])
    project_endpoint = _project_endpoint(arguments.appconfig_endpoint)
    collector_principal_id = _resolve_collector_principal(project_endpoint)
    backfill_principal_id = _principal_id(arguments.backfill_principal_id, "--backfill-principal-id")
    workflow_resource_id = _deploy_infrastructure(
        arguments.environment, arguments.resource_group, arguments.appconfig_endpoint,
        collector_principal_id, backfill_principal_id,
    )
    credential = AsyncAzureCliCredential()
    try:
        async with httpx.AsyncClient(timeout=60) as client:
            routine = await routine_request(config, "routine-create", project_endpoint, credential, client)
    finally:
        await credential.close()
    return {
        "agentName": AGENT_NAME,
        "collectorPrincipalId": collector_principal_id,
        "backfillPrincipalId": backfill_principal_id,
        "workflowResourceId": workflow_resource_id,
        "appConfigurationEndpoint": arguments.appconfig_endpoint,
        "appConfigurationKey": LOGIC_APP_URL_KEY,
        "routine": routine,
    }


def collection_main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description="Deploy and manage the Teams collection workflow.")
    parser.add_argument("command", choices=(
        "deploy", "routine-definition", "routine-create", "routine-enable",
        "routine-disable", "routine-dispatch",
    ))
    parser.add_argument("--environment", choices=("dev", "test", "prod"))
    parser.add_argument("--resource-group")
    parser.add_argument("--appconfig-endpoint")
    parser.add_argument("--project-endpoint")
    parser.add_argument("--backfill-principal-id")
    parser.add_argument("--config", type=Path,
                        default=_PROJECT_DIR / "config" / "teams_collection_config.json")
    arguments = parser.parse_args(argv)
    try:
        config = json.loads(arguments.config.read_text(encoding="utf-8"))
        validate_channels(config["channels"])
        if arguments.command != "deploy" and arguments.backfill_principal_id:
            parser.error("--backfill-principal-id applies only to deploy.")
        if arguments.command == "deploy":
            for name in ("environment", "resource_group", "appconfig_endpoint",
                         "backfill_principal_id"):
                if not getattr(arguments, name):
                    parser.error("--" + name.replace("_", "-") + " is required for deploy.")
            result = asyncio.run(deploy_collection(arguments))
        elif arguments.command == "routine-definition":
            result = routine_definition(config)
        else:
            result = asyncio.run(execute_routine(arguments, config))
        print(json.dumps(result, indent=2))
        return 0
    except Exception as error:
        print(f"Teams collection deployment failed ({type(error).__name__}); {error}",
              file=sys.stderr)
        return 1


if __name__ == "__main__":
    try:
        main()
    finally:
        asyncio.run(close_credential())
