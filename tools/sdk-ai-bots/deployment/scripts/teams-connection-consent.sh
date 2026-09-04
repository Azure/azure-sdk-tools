#!/usr/bin/env bash

set -euo pipefail

mode="${1:-}"
connection_name="${2:-}"

if [[ -z "${AZURE_SUBSCRIPTION_ID:-}" || -z "${AZURE_RESOURCE_GROUP:-}" ]]; then
    echo "AZURE_SUBSCRIPTION_ID and AZURE_RESOURCE_GROUP are required." >&2
    exit 1
fi

find_teams_connection() {
    local candidate_name
    local api_id
    local -a matches=()

    while IFS= read -r candidate_name; do
        [[ -z "$candidate_name" ]] && continue
        api_id=$(az resource show \
            --subscription "$AZURE_SUBSCRIPTION_ID" \
            --resource-group "$AZURE_RESOURCE_GROUP" \
            --resource-type Microsoft.Web/connections \
            --name "$candidate_name" \
            --query properties.api.id \
            --output tsv)
        if [[ "${api_id,,}" == */managedapis/teams ]]; then
            matches+=("$candidate_name")
        fi
    done < <(az resource list \
        --subscription "$AZURE_SUBSCRIPTION_ID" \
        --resource-group "$AZURE_RESOURCE_GROUP" \
        --resource-type Microsoft.Web/connections \
        --query '[].name' \
        --output tsv)

    if (( ${#matches[@]} != 1 )); then
        echo "Expected exactly one Teams API connection; found ${#matches[@]}." >&2
        exit 1
    fi

    printf '%s' "${matches[0]}"
}

connection_status() {
    az resource show \
        --subscription "$AZURE_SUBSCRIPTION_ID" \
        --resource-group "$AZURE_RESOURCE_GROUP" \
        --resource-type Microsoft.Web/connections \
        --name "$1" \
        --query 'properties.statuses[0].status' \
        --output tsv
}

if [[ -z "$connection_name" ]]; then
    connection_name=$(find_teams_connection)
fi

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
    *)
        echo "Usage: teams-connection-consent.sh <check|verify> [connection-name]" >&2
        exit 2
        ;;
esac