"""Manage the deployed Teams collection Routine and ARM deployment inputs.

Deploy agents/teams_collection_agent with scripts/deploy_hosted_agent.py.
Generate ARM parameters, deploy pipelines/teams-collection/template.json into the
resource group containing the existing Cosmos account/database, and configure
TEAMS_COLLECTION_LOGIC_APP_URL in App Configuration with the SAS-free trigger URL.
Grant the agent App Configuration Data Reader. The ARM template grants its
collectorPrincipalId Cosmos container data access and account-level readMetadata.
Create the Routine paused and use routine-dispatch to verify the
deployed flow before enabling its schedule. Collection runs only in the hosted
agent through the Logic App. Routine delivery status is not Cosmos run status.
"""

import argparse
import asyncio
import json
import logging
import re
import sys
from pathlib import Path
from urllib.parse import quote, urlsplit
from uuid import UUID

PROJECT = Path(__file__).resolve().parents[1]
if str(PROJECT) not in sys.path:
    sys.path.insert(0, str(PROJECT))

from services.teams_collection_service import validate_channels

AGENT_NAME = "azure-sdk-teams-collection-agent"


def routine_definition(config):
    routine = config["routine"]
    if not re.fullmatch(r"teams-channel-collection[-a-zA-Z0-9]*", routine["name"]):
        raise ValueError("Routine name must start with teams-channel-collection.")
    if len(routine["cron"].split()) != 5 or not routine["timeZone"]:
        raise ValueError("Routine requires a five-field cron expression and timeZone; minimum interval is five minutes.")
    return {
        "description": "Collect configured Teams channels and their replies into the dedicated Cosmos archive.",
        "enabled": False,
        "authorization": {"identity": "agent"},
        "triggers": {"schedule": {"type": "schedule", "cron_expression": routine["cron"],
                                   "time_zone": routine["timeZone"]}},
        "action": {"type": "invoke_agent_responses_api", "agent_name": AGENT_NAME,
                   "input": "Collect configured Teams channels."},
    }


def deployment_parameters(config, arguments):
    validate_channels(config["channels"])
    UUID(config["tenantId"])
    UUID(arguments.collector_principal_id)
    if not re.fullmatch(r"[a-zA-Z0-9-]*teams-collection[a-zA-Z0-9-]*", arguments.logic_app_name):
        raise ValueError("Use a dedicated logic app name containing teams-collection.")
    connection = arguments.teams_connection_resource_id
    if not re.fullmatch(r"/subscriptions/[0-9a-fA-F-]{36}/resourceGroups/[^/?#]+/providers/Microsoft.Web/connections/[^/?#]+", connection):
        raise ValueError("Expected an existing Teams API connection resource ID.")
    return {
        "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#",
        "contentVersion": "1.0.0.0",
        "parameters": {key: {"value": value} for key, value in {
            "logicAppName": arguments.logic_app_name, "location": arguments.location,
            "teamsConnectionResourceId": connection, "tenantId": config["tenantId"],
            "collectorPrincipalId": arguments.collector_principal_id,
            "cosmosAccountName": arguments.cosmos_account_name,
            "allowedChannels": [f"{channel['teamId']}|{channel['channelId']}" for channel in config["channels"]],
        }.items()},
    }


async def routine_request(config, command, endpoint, credential, client):
    definition = routine_definition(config)
    address = urlsplit(endpoint)
    if (address.scheme != "https" or not address.hostname or not address.hostname.endswith(".services.ai.azure.com")
            or address.username or address.password or address.port not in (None, 443)
            or not re.fullmatch(r"/api/projects/[^/]+/?", address.path)
            or address.query or address.fragment):
        raise ValueError("Expected a Foundry project endpoint in Azure public cloud.")
    url = endpoint.rstrip("/") + "/routines/" + quote(config["routine"]["name"], safe="")
    token = await credential.get_token("https://ai.azure.com/.default")
    headers = {"Authorization": f"Bearer {token.token}"}
    existing = await client.get(url, headers=headers, follow_redirects=False)
    if existing.status_code == 200:
        if existing.json().get("action", {}).get("agent_name") != AGENT_NAME:
            raise ValueError("Refusing to modify a Routine targeting a different agent.")
    elif existing.status_code != 404 or command != "routine-create":
        raise RuntimeError(f"Routine lookup returned HTTP {existing.status_code}.")
    if command == "routine-create":
        response = await client.put(url, headers=headers, json=definition, follow_redirects=False)
    else:
        action = {"routine-enable": "enable", "routine-disable": "disable",
                  "routine-dispatch": "dispatch_async"}[command]
        response = await client.post(url + ":" + action, headers=headers, json={}, follow_redirects=False)
    if response.status_code not in (200, 201, 202):
        raise RuntimeError(f"Routine operation returned HTTP {response.status_code}; no response content was logged.")
    result = response.json()
    return {key: result[key] for key in ("name", "enabled", "dispatch_id", "task_id") if key in result}


async def execute(arguments, config):
    import httpx
    from dotenv import load_dotenv
    from config import app_config
    from utils.azure_credential import close_credential, get_credential

    load_dotenv(PROJECT / ".env", override=False)
    try:
        if not arguments.project_endpoint:
            await app_config.init()
        endpoint = arguments.project_endpoint or app_config.get("AI_FOUNDRY_PROJECT_ENDPOINT", "")
        async with httpx.AsyncClient(timeout=60) as client:
            return await routine_request(config, arguments.command, endpoint, get_credential(), client)
    finally:
        await close_credential()


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("command", choices=("render-parameters", "routine-definition", "routine-create",
                                           "routine-enable", "routine-disable", "routine-dispatch"))
    parser.add_argument("--config", type=Path, default=PROJECT / "config/teams_collection.json")
    parser.add_argument("--project-endpoint")
    parser.add_argument("--logic-app-name", default="azuresdkqabot-dev-teams-collection")
    parser.add_argument("--location", default="westus2")
    parser.add_argument("--teams-connection-resource-id",
                        help="Existing Teams connection ARM ID used by render-parameters for the deployed Logic App.")
    parser.add_argument("--collector-principal-id")
    parser.add_argument("--cosmos-account-name")
    arguments = parser.parse_args()
    try:
        config = json.loads(arguments.config.read_text(encoding="utf-8"))
        validate_channels(config["channels"])
        if arguments.command == "render-parameters":
            for name in ("teams_connection_resource_id", "collector_principal_id", "cosmos_account_name"):
                if not getattr(arguments, name):
                    parser.error("--" + name.replace("_", "-") + " is required for render-parameters.")
            result = deployment_parameters(config, arguments)
        elif arguments.command == "routine-definition":
            result = routine_definition(config)
        else:
            result = asyncio.run(execute(arguments, config))
        print(json.dumps(result, indent=2))
        return 0
    except Exception as error:
        print(f"Collection command failed ({type(error).__name__}); check configuration, permissions and collection run status.",
              file=sys.stderr)
        return 1


if __name__ == "__main__":
    logging.basicConfig(level=logging.WARNING)
    sys.exit(main())