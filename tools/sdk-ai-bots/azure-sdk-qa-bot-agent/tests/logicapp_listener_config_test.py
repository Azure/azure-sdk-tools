"""Static listener parameter, template and deployment-pipeline regression tests."""

import json
from pathlib import Path

import pytest


AGENT_ROOT = Path(__file__).resolve().parents[1]
LOGICAPP_ROOT = AGENT_ROOT / "pipelines" / "logicapp"
BOARD_TARGETS = {
    "dev": (
        "7ccc31f0-b371-450b-a73c-48f5a31a9b96",
        "19:17b23ff1d5d54cc293dcf673d32a5b7f@thread.tacv2",
    ),
    "test": (
        "39910aef-85da-4e30-b5e3-35f04ef38648",
        "19:1897c1bd6c6145efb62167733e1b8c82@thread.tacv2",
    ),
    "prod": (
        "1a10b50c-e870-4fe0-8483-bf5542a8d2d8",
        "19:3ebb18fded0e47938f998e196a52952f@thread.tacv2",
    ),
}
EXISTING_TEAMS = ("azure_sdk", "azure_mcp_server")
ENVIRONMENTS = ("dev", "test", "prod")
BOARD_NAMES = {
    "dev": "api-stewardship-board-qabot-dev-logicapp",
    "test": "api-stewardship-board-qabot-test-logicapp",
    "prod": "api-stewardship-board-qabot-logicapp",
}


def load_parameters(team: str, environment: str) -> dict:
    content = json.loads(
        (LOGICAPP_ROOT / f"parameters.{team}.{environment}.json").read_text()
    )
    return {key: parameter["value"] for key, parameter in content["parameters"].items()}


@pytest.mark.parametrize("environment", ENVIRONMENTS)
def test_board_listener_is_disabled_with_known_team_and_safe_channels(environment):
    board = load_parameters("api_stewardship_board", environment)
    team, channel = BOARD_TARGETS[environment]
    assert board["teamsGroupId"] == team
    assert board["teamsChannelIds"] == [channel]
    assert board["workflowState"] == "Disabled"
    assert board["logicAppName"] == BOARD_NAMES[environment]


@pytest.mark.parametrize("environment", ENVIRONMENTS)
def test_board_reuses_matching_environment_infrastructure(environment):
    board = load_parameters("api_stewardship_board", environment)
    sdk = load_parameters("azure_sdk", environment)
    infrastructure_keys = set(sdk) - {"logicAppName", "teamsGroupId", "teamsChannelIds"}
    assert set(board) == set(sdk) | {"workflowState"}
    assert {key: board[key] for key in infrastructure_keys} == {
        key: sdk[key] for key in infrastructure_keys
    }


def test_listener_names_are_unique_across_all_teams_and_environments():
    names = [
        load_parameters(team, environment)["logicAppName"]
        for team in (*EXISTING_TEAMS, "api_stewardship_board")
        for environment in ENVIRONMENTS
    ]
    assert len(names) == len(set(names)) == 9


def test_template_preserves_existing_enabled_default_and_team_binding():
    template = json.loads((LOGICAPP_ROOT / "template.json").read_text())
    assert template["parameters"]["workflowState"]["defaultValue"] == "Enabled"
    assert template["parameters"]["workflowState"]["allowedValues"] == ["Enabled", "Disabled"]
    workflow = template["resources"][0]
    assert workflow["properties"]["state"] == "[parameters('workflowState')]"
    trigger = workflow["properties"]["definition"]["triggers"][
        "When_a_new_message_is_added_to_a_chat_or_channel"
    ]
    assert trigger["inputs"]["body"]["groupId"] == "[parameters('teamsGroupId')]"
    assert trigger["inputs"]["body"]["channels"] == "[parameters('teamsChannelIds')]"


@pytest.mark.parametrize("team", EXISTING_TEAMS)
@pytest.mark.parametrize("environment", ENVIRONMENTS)
def test_existing_teams_retain_enabled_default_and_exclude_board_channels(team, environment):
    existing = load_parameters(team, environment)
    assert "workflowState" not in existing
    assert set(existing["teamsChannelIds"]).isdisjoint(
        channel for _, channel in BOARD_TARGETS.values()
    )


def test_pipeline_keeps_original_bash_deployment_with_new_team_choice():
    pipeline = (AGENT_ROOT / "pipelines" / "logicapp-cd.yml").read_text()
    assert "default: azure_sdk" in pipeline
    assert "default: dev" in pipeline
    assert "- azure_sdk\n      - azure_mcp_server\n      - api_stewardship_board" in pipeline
    assert pipeline.count("task: AzureCLI@2") == 1
    assert pipeline.count("task:") == 1
    assert "scriptType: bash" in pipeline
    assert "scriptLocation: inlineScript" in pipeline
    assert 'set -euo pipefail' in pipeline
    assert 'PARAMS_FILE="pipelines/logicapp/parameters.${{ parameters.team }}.${{ parameters.environment }}.json"' in pipeline
    assert 'az deployment group create \\' in pipeline
    assert '--resource-group "$(RESOURCE_GROUP)"' in pipeline
    assert '--template-file "$TEMPLATE_FILE"' in pipeline
    assert '--parameters "@$PARAMS_FILE"' in pipeline
    assert '--name "logicapp-${{ parameters.team }}-$(Build.BuildId)"' in pipeline
    assert "activateBoardListener" not in pipeline
    assert "workflowState=Enabled" not in pipeline
    assert "preflight" not in pipeline.lower()
    assert "deploy.ps1" not in pipeline


def test_no_new_deployment_script_exists():
    assert not (LOGICAPP_ROOT / "deploy.ps1").exists()
