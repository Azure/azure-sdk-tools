#!/usr/bin/env bash

# Run with an authorized administrator's Azure CLI login, not the deployment identity.
# This changes Azure roles, pipeline authorization, and the backend allowlist.

set -euo pipefail

SUB="a18897a6-7e44-457d-9260-f2854c0aca42"
OID="f177a89e-2025-447c-ab31-a4b8dba3751a"
CLIENT_ID="2a0eb32e-233f-4ccf-8977-0588c3d554b5"
SUB_SCOPE="/subscriptions/$SUB"
RG="$SUB_SCOPE/resourceGroups/azure-sdk-qa-bot-dev"
ENDPOINT_ID="5e79584d-12de-4b1a-9637-7897b6d8b2cd"
ADO_RESOURCE="499b84ac-1321-427f-aa17-267ca6975798"

ensure_role() {
    local role="$1"
    local scope="$2"
    local existing

    existing=$(az role assignment list \
        --subscription "$SUB" \
        --assignee-object-id "$OID" \
        --role "$role" \
        --scope "$scope" \
        --include-inherited \
        --fill-principal-name false \
        --query '[0].id' \
        --output tsv)
    if [[ -n "$existing" ]]; then
        printf 'Existing role %s at %s; skipping creation.\n' "$role" "$scope"
        return
    fi
    az role assignment create \
        --subscription "$SUB" \
        --assignee-object-id "$OID" \
        --assignee-principal-type ServicePrincipal \
        --role "$role" \
        --scope "$scope" \
        --output none
}

# Dev RG Contributor already exists. Add scoped RBAC and lock authority.
ensure_role "18d7d88d-d35e-4fb5-a5c3-7773c20a72d9" "$RG"

# Read subscription usage and discover the externally managed signing vault.
ensure_role "acdd72a7-3385-48ef-bd42-f606fba81ae7" "$SUB_SCOPE"

# This role permits resource-group creation/update across the subscription, not just dev.
ROLE_NAME="QA Bot Subscription Deployment"
ROLE_ID=$(az role definition list \
    --subscription "$SUB" \
    --name "$ROLE_NAME" \
    --query '[0].name' \
    --output tsv)
if [[ -z "$ROLE_ID" ]]; then
    ROLE_JSON=$(jq -nc --arg scope "$SUB_SCOPE" '{
        Name: "QA Bot Subscription Deployment",
        IsCustom: true,
        Description: "Run subscription deployments and create/update resource groups.",
        Actions: [
            "Microsoft.Resources/deployments/*",
            "Microsoft.Resources/subscriptions/read",
            "Microsoft.Resources/subscriptions/resourceGroups/read",
            "Microsoft.Resources/subscriptions/resourceGroups/write"
        ],
        NotActions: [],
        DataActions: [],
        NotDataActions: [],
        AssignableScopes: [$scope]
    }')
    ROLE_ID=$(az role definition create \
        --subscription "$SUB" \
        --role-definition "$ROLE_JSON" \
        --query name \
        --output tsv)
else
    printf 'Reusing existing custom role %s.\n' "$ROLE_NAME"
fi
ensure_role "$ROLE_ID" "$SUB_SCOPE"

# The administrator also needs permission to authorize this endpoint in Azure DevOps.
# This authorizes pipeline 8265 only, not all pipelines.
az rest \
    --method PATCH \
    --url "https://dev.azure.com/azure-sdk/internal/_apis/pipelines/pipelinepermissions/endpoint/$ENDPOINT_ID?api-version=7.1-preview.1" \
    --resource "$ADO_RESOURCE" \
    --headers Content-Type=application/json \
    --body '{"pipelines":[{"id":8265,"authorized":true}]}' \
    --output none

# Preserve other backend callers. Auth changes may restart the app.
AUTH_RESOURCE="$RG/providers/Microsoft.Web/sites/azuresdkqabot-dev-server/config/authsettingsV2"
ALLOWED_CLIENTS=$(az rest \
    --method GET \
    --url "https://management.azure.com$AUTH_RESOURCE?api-version=2022-03-01" \
    --query 'properties.identityProviders.azureActiveDirectory.validation.defaultAuthorizationPolicy.allowedApplications' \
    --output json |
    jq -c --arg client "$CLIENT_ID" '(. // []) + [$client] | unique')

az resource update \
    --subscription "$SUB" \
    --ids "$AUTH_RESOURCE" \
    --api-version 2022-03-01 \
    --set "properties.identityProviders.azureActiveDirectory.validation.defaultAuthorizationPolicy.allowedApplications=$ALLOWED_CLIENTS" \
    --output none

# Resource data roles are reconciled by the pipeline, not preassigned here.
# External signing-key policies, Teams consent, feeds and secure-file permissions
# remain integration prerequisites and are not modified by this file.
