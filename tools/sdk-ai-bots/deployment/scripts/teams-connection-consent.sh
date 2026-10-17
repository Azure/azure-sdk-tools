#!/usr/bin/env bash

set -euo pipefail

mode="${1:-}"
connection_name="${2:-${TEAMS_CONNECTION_NAME:-}}"

case "$mode" in
    check|verify) ;;
    *)
        echo "Usage: teams-connection-consent.sh <check|verify> [connection-name]" >&2
        exit 2
        ;;
esac

if [[ -z "${AZURE_SUBSCRIPTION_ID:-}" || -z "${AZURE_RESOURCE_GROUP:-}" ]]; then
    echo "AZURE_SUBSCRIPTION_ID and AZURE_RESOURCE_GROUP are required." >&2
    exit 1
fi

workflow_names=()
workflow_api_ids=()
primary_workflow="${LOGIC_APP_WORKFLOW_NAME:-${LOGIC_APP_WORKFLOW_NAME_OVERRIDE:-}}"
mcp_workflow="${AZURE_MCP_SERVER_LOGIC_APP_WORKFLOW_NAME:-${AZURE_MCP_SERVER_LOGIC_APP_WORKFLOW_NAME_OVERRIDE:-}}"
[[ -z "$primary_workflow" ]] || workflow_names+=("$primary_workflow")
if [[ -n "${AZURE_MCP_TEAMS_GROUP_ID:-}" && -n "${AZURE_MCP_TEAMS_CHANNEL_IDS:-}" ]]; then
    if [[ -z "$mcp_workflow" ]]; then
        echo "The enabled Azure MCP Server workflow name is required to verify its Teams connection." >&2
        exit 1
    fi
    workflow_names+=("$mcp_workflow")
fi

connection_prefix="/subscriptions/$AZURE_SUBSCRIPTION_ID/resourceGroups/$AZURE_RESOURCE_GROUP/providers/Microsoft.Web/connections/"
for workflow_name in "${workflow_names[@]}"; do
    binding=$(az resource show \
        --subscription "$AZURE_SUBSCRIPTION_ID" \
        --resource-group "$AZURE_RESOURCE_GROUP" \
        --resource-type Microsoft.Logic/workflows \
        --name "$workflow_name" \
        --api-version 2019-05-01 \
        --query 'properties.parameters."$connections".value.teams' \
        --output json)
    if ! workflow_connection_id=$(jq -er '.connectionId | select(type == "string" and length > 0)' <<< "$binding") ||
       ! workflow_api_id=$(jq -er '.id | select(type == "string" and length > 0)' <<< "$binding"); then
        echo "Logic App '$workflow_name' has no valid Teams connection binding." >&2
        exit 1
    fi
    if [[ "${workflow_connection_id,,}" != "${connection_prefix,,}"* ]]; then
        echo "Logic App '$workflow_name' references a Teams connection outside the target subscription/resource group: $workflow_connection_id" >&2
        exit 1
    fi
    referenced_name="${workflow_connection_id##*/}"
    if [[ -z "$connection_name" ]]; then
        connection_name="$referenced_name"
    fi
    if [[ "${workflow_connection_id,,}" != "${connection_prefix,,}${connection_name,,}" ]]; then
        echo "Logic App '$workflow_name' references '$workflow_connection_id', not the selected Teams connection '$connection_name'." >&2
        exit 1
    fi
    workflow_api_ids+=("$workflow_api_id")
done

if [[ -z "$connection_name" || "$connection_name" == */* ]]; then
    echo "Specify a connection name, TEAMS_CONNECTION_NAME, or a provisioned Logic App workflow name." >&2
    exit 1
fi

api_id=$(az resource show \
    --subscription "$AZURE_SUBSCRIPTION_ID" \
    --resource-group "$AZURE_RESOURCE_GROUP" \
    --resource-type Microsoft.Web/connections \
    --name "$connection_name" \
    --query properties.api.id \
    --output tsv)
if [[ "${api_id,,}" != */managedapis/teams ]]; then
    echo "Connection '$connection_name' is not a Teams managed API connection: $api_id" >&2
    exit 1
fi
for workflow_api_id in "${workflow_api_ids[@]}"; do
    if [[ "${workflow_api_id,,}" != "${api_id,,}" ]]; then
        echo "The Logic App's Teams managed API '$workflow_api_id' does not match connection '$connection_name': $api_id" >&2
        exit 1
    fi
done

connection_status() {
    local value
    if ! value=$(az resource show \
        --subscription "$AZURE_SUBSCRIPTION_ID" \
        --resource-group "$AZURE_RESOURCE_GROUP" \
        --resource-type Microsoft.Web/connections \
        --name "$1" \
        --query 'properties.statuses[0].status' \
        --output tsv); then
        echo "Unable to read the status of Teams API connection '$1'." >&2
        return 1
    fi
    if [[ -z "$value" || "$value" == "null" ]]; then
        echo "Teams API connection '$1' has no status." >&2
        return 1
    fi
    printf '%s' "$value"
}

case "$mode" in
    check)
        status=$(connection_status "$connection_name")
        resource_id="/subscriptions/$AZURE_SUBSCRIPTION_ID/resourceGroups/$AZURE_RESOURCE_GROUP/providers/Microsoft.Web/connections/$connection_name"
        portal_url="https://portal.azure.com/#resource$resource_id"
        jq -nc \
            --arg connectionName "$connection_name" \
            --arg status "$status" \
            --arg portalUrl "$portal_url" \
            '{connectionName:$connectionName,status:$status,consentRequired:($status != "Connected"),portalUrl:$portalUrl}'
        ;;
    verify)
        attempts="${CONSENT_VERIFY_ATTEMPTS:-12}"
        interval_seconds="${CONSENT_VERIFY_INTERVAL_SECONDS:-10}"
        if [[ ! "$attempts" =~ ^[1-9][0-9]*$ || ! "$interval_seconds" =~ ^[0-9]+$ ]]; then
            echo "Consent verification attempts must be positive and the interval must be non-negative integer seconds." >&2
            exit 1
        fi
        for ((attempt = 1; attempt <= attempts; attempt++)); do
            status=$(connection_status "$connection_name")
            if [[ "$status" == "Connected" ]]; then
                echo "Teams API connection '$connection_name' is Connected."
                exit 0
            fi
            if (( attempt < attempts )); then
                echo "Teams API connection '$connection_name' is '$status'; waiting for consent propagation."
                sleep "$interval_seconds"
            fi
        done
        echo "Teams API connection '$connection_name' is '$status', not Connected." >&2
        exit 1
        ;;
esac