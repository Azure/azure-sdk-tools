"""Deploy and manage the Teams channel collection workflow.

The hosted agent must exist before this script runs because its instance
identity is used by the Logic App access policy and Cosmos data-plane roles.
Deployment creates or updates the Routine in the disabled state. Separate
commands enable, disable, or manually dispatch the deployed Routine.
"""

from __future__ import annotations

import argparse
import asyncio
import json
import re
import shutil
import subprocess
import sys
from collections.abc import Mapping
from pathlib import Path
from urllib.parse import parse_qs, quote, urlencode, urlsplit, urlunsplit
from uuid import UUID

import httpx
from azure.ai.projects import AIProjectClient
from azure.identity import AzureCliCredential
from azure.identity.aio import AzureCliCredential as AsyncAzureCliCredential

PROJECT = Path(__file__).resolve().parents[1]
if str(PROJECT) not in sys.path:
    sys.path.insert(0, str(PROJECT))

from services.teams_collection_service import validate_channels

AGENT_NAME = "azure-sdk-teams-collection-agent"
APP_CONFIG_READER_ROLE = "App Configuration Data Reader"
LOGIC_APP_URL_KEY = "TEAMS_COLLECTION_LOGIC_APP_URL"
ROUTINES_API_VERSION = "v1"
ROUTINES_FEATURE = "Routines=V2Preview"


def _run_az(arguments: list[str]) -> str:
    executable = shutil.which("az") or shutil.which("az.cmd")
    if executable is None:
        raise RuntimeError("Azure CLI was not found on PATH.")
    result = subprocess.run(
        [executable, *arguments],
        capture_output=True,
        text=True,
        encoding="utf-8",
        check=False,
    )
    if result.returncode != 0:
        operation = " ".join(arguments[:3])
        detail = (result.stderr or result.stdout).strip()
        if len(detail) > 2000:
            detail = detail[-2000:]
        raise RuntimeError(
            f"Azure CLI operation failed: az {operation}. "
            f"{detail or 'No diagnostic output was returned.'}"
        )
    return result.stdout.strip()


def _collector_principal_id(agent) -> str:
    identity = getattr(agent, "instance_identity", None)
    if isinstance(identity, Mapping):
        value = identity.get("principal_id")
    else:
        value = getattr(identity, "principal_id", None)
    try:
        return str(UUID(value))
    except (AttributeError, TypeError, ValueError):
        raise RuntimeError(
            f"Hosted agent {AGENT_NAME!r} does not expose a valid instance principal ID."
        ) from None


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
    queries = parse_qs(address.query)
    api_versions = queries.get("api-version", [])
    if (address.scheme != "https" or not address.hostname
            or not address.hostname.endswith(".logic.azure.com")
            or address.username or address.password or address.port not in (None, 443)
            or address.fragment
            or not address.path.endswith("/triggers/manual/paths/invoke")
            or len(api_versions) != 1 or not api_versions[0]):
        raise ValueError("Logic App returned an unexpected manual trigger callback URL.")
    return urlunsplit((
        "https",
        address.hostname,
        address.path,
        urlencode({"api-version": api_versions[0]}),
        "",
    ))


def _project_endpoint(appconfig_endpoint: str) -> str:
    endpoint = _run_az([
        "appconfig", "kv", "show",
        "--endpoint", appconfig_endpoint,
        "--auth-mode", "login",
        "--key", "AI_FOUNDRY_PROJECT_ENDPOINT",
        "--query", "value",
        "--output", "tsv",
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
    project = AIProjectClient(
        endpoint=project_endpoint,
        credential=credential,
        allow_preview=True,
    )
    try:
        return _collector_principal_id(project.agents.get(AGENT_NAME))
    finally:
        project.close()
        credential.close()


def routine_definition(config: dict) -> dict:
    routine = config["routine"]
    if not re.fullmatch(r"teams-channel-collection[-a-zA-Z0-9]*", routine["name"]):
        raise ValueError("Routine name must start with teams-channel-collection.")
    if len(routine["cron"].split()) != 5 or not routine["timeZone"]:
        raise ValueError(
            "Routine requires a five-field cron expression and timeZone; "
            "minimum interval is five minutes."
        )
    return {
        "description": "Collect, process, and archive configured Teams channels and their replies.",
        "enabled": False,
        "authorization": {"identity": "agent"},
        "triggers": {
            "schedule": {
                "type": "schedule",
                "cron_expression": routine["cron"],
                "time_zone": routine["timeZone"],
            }
        },
        "action": {
            "type": "invoke_agent_responses_api",
            "agent_name": AGENT_NAME,
            "input": "Collect configured Teams channels.",
        },
    }


async def routine_request(config, command, endpoint, credential, client) -> dict:
    definition = routine_definition(config)
    address = urlsplit(endpoint)
    if (address.scheme != "https" or not address.hostname
            or not address.hostname.endswith(".services.ai.azure.com")
            or address.username or address.password or address.port not in (None, 443)
            or not re.fullmatch(r"/api/projects/[^/]+/?", address.path)
            or address.query or address.fragment):
        raise ValueError("Expected a Foundry project endpoint in Azure public cloud.")
    url = endpoint.rstrip("/") + "/routines/" + quote(config["routine"]["name"], safe="")
    token = await credential.get_token("https://ai.azure.com/.default")
    headers = {
        "Authorization": f"Bearer {token.token}",
        "Foundry-Features": ROUTINES_FEATURE,
    }
    query = {"api-version": ROUTINES_API_VERSION}
    existing = await client.get(
        url, params=query, headers=headers, follow_redirects=False
    )
    if existing.status_code == 200:
        if existing.json().get("action", {}).get("agent_name") != AGENT_NAME:
            raise ValueError("Refusing to modify a Routine targeting a different agent.")
    elif existing.status_code != 404 or command != "routine-create":
        raise RuntimeError(f"Routine lookup returned HTTP {existing.status_code}.")
    if command == "routine-create":
        response = await client.put(
            url, params=query, headers=headers, json=definition, follow_redirects=False
        )
    else:
        action = {
            "routine-enable": "enable",
            "routine-disable": "disable",
            "routine-dispatch": "dispatch_async",
        }[command]
        response = await client.post(
            url + ":" + action,
            params=query,
            headers=headers,
            json={},
            follow_redirects=False,
        )
    if response.status_code not in (200, 201, 202):
        raise RuntimeError(
            f"Routine operation returned HTTP {response.status_code}; "
            "no response content was logged."
        )
    result = response.json()
    return {
        key: result[key]
        for key in ("name", "enabled", "dispatch_id", "task_id")
        if key in result
    }


async def execute_routine(arguments, config) -> dict:
    from dotenv import load_dotenv
    from config import app_config
    from utils.azure_credential import close_credential, get_credential

    load_dotenv(PROJECT / ".env", override=False)
    try:
        if arguments.project_endpoint:
            endpoint = arguments.project_endpoint
        elif arguments.appconfig_endpoint:
            endpoint = _project_endpoint(arguments.appconfig_endpoint)
        else:
            await app_config.init()
            endpoint = app_config.get("AI_FOUNDRY_PROJECT_ENDPOINT", "")
        async with httpx.AsyncClient(timeout=60) as client:
            return await routine_request(
                config, arguments.command, endpoint, get_credential(), client
            )
    finally:
        await close_credential()


def _deploy_infrastructure(
    environment: str,
    resource_group: str,
    appconfig_endpoint: str,
    collector_principal_id: str,
) -> str:
    parameters_file = (
        PROJECT / "pipelines" / "teams-collection"
        / f"parameters.azure_sdk.{environment}.json"
    )
    if not parameters_file.is_file():
        raise ValueError(f"No Teams collection parameters exist for {environment!r}.")
    workflow_resource_id = _run_az([
        "deployment", "group", "create",
        "--resource-group", resource_group,
        "--name", f"teams-collection-{environment}",
        "--mode", "Incremental",
        "--template-file", str(
            PROJECT / "pipelines" / "teams-collection" / "template.json"
        ),
        "--parameters", f"@{parameters_file}",
        f"collectorPrincipalId={collector_principal_id}",
        "--query", "properties.outputs.workflowResourceId.value",
        "--output", "tsv",
    ])
    if not workflow_resource_id.startswith("/subscriptions/"):
        raise RuntimeError("Teams collection deployment returned no workflow resource ID.")

    callback_url = _run_az([
        "rest",
        "--method", "post",
        "--url", (
            f"https://management.azure.com{workflow_resource_id}"
            "/triggers/manual/listCallbackUrl?api-version=2019-05-01"
        ),
        "--query", "value",
        "--output", "tsv",
    ])
    logic_app_url = _sas_free_callback_url(callback_url)

    appconfig_name = _appconfig_name(appconfig_endpoint)
    appconfig_resource_id = _run_az([
        "appconfig", "show",
        "--name", appconfig_name,
        "--query", "id",
        "--output", "tsv",
    ])
    if not appconfig_resource_id.startswith("/subscriptions/"):
        raise RuntimeError("App Configuration lookup returned no resource ID.")
    _run_az([
        "role", "assignment", "create",
        "--assignee-object-id", collector_principal_id,
        "--assignee-principal-type", "ServicePrincipal",
        "--role", APP_CONFIG_READER_ROLE,
        "--scope", appconfig_resource_id,
        "--output", "none",
    ])
    _run_az([
        "appconfig", "kv", "set",
        "--endpoint", appconfig_endpoint,
        "--auth-mode", "login",
        "--key", LOGIC_APP_URL_KEY,
        "--value", logic_app_url,
        "--yes",
        "--output", "none",
    ])
    return workflow_resource_id


async def deploy(arguments) -> dict:
    config = json.loads(arguments.config.read_text(encoding="utf-8"))
    validate_channels(config["channels"])
    project_endpoint = _project_endpoint(arguments.appconfig_endpoint)
    collector_principal_id = _resolve_collector_principal(project_endpoint)
    workflow_resource_id = _deploy_infrastructure(
        arguments.environment,
        arguments.resource_group,
        arguments.appconfig_endpoint,
        collector_principal_id,
    )

    credential = AsyncAzureCliCredential()
    try:
        async with httpx.AsyncClient(timeout=60) as client:
            routine = await routine_request(
                config, "routine-create", project_endpoint, credential, client
            )
    finally:
        await credential.close()

    return {
        "agentName": AGENT_NAME,
        "collectorPrincipalId": collector_principal_id,
        "workflowResourceId": workflow_resource_id,
        "appConfigurationEndpoint": arguments.appconfig_endpoint,
        "appConfigurationKey": LOGIC_APP_URL_KEY,
        "routine": routine,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "command",
        choices=(
            "deploy",
            "routine-definition",
            "routine-create",
            "routine-enable",
            "routine-disable",
            "routine-dispatch",
        ),
    )
    parser.add_argument("--environment", choices=("dev", "test", "prod"))
    parser.add_argument("--resource-group")
    parser.add_argument("--appconfig-endpoint")
    parser.add_argument("--project-endpoint")
    parser.add_argument(
        "--config",
        type=Path,
        default=PROJECT / "config" / "teams_collection_config.json",
    )
    arguments = parser.parse_args()
    try:
        config = json.loads(arguments.config.read_text(encoding="utf-8"))
        validate_channels(config["channels"])
        if arguments.command == "deploy":
            for name in ("environment", "resource_group", "appconfig_endpoint"):
                if not getattr(arguments, name):
                    parser.error(
                        "--" + name.replace("_", "-") + " is required for deploy."
                    )
            result = asyncio.run(deploy(arguments))
        elif arguments.command == "routine-definition":
            result = routine_definition(config)
        else:
            result = asyncio.run(execute_routine(arguments, config))
        print(json.dumps(result, indent=2))
        return 0
    except Exception as error:
        print(
            f"Teams collection deployment failed ({type(error).__name__}); "
            f"{error}",
            file=sys.stderr,
        )
        return 1


if __name__ == "__main__":
    sys.exit(main())
