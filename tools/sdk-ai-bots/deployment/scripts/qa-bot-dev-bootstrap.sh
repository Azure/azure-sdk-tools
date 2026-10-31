#!/usr/bin/env bash

# Run with an authorized administrator's Azure CLI login, not the deployment identity.
# This changes Azure roles and pipeline authorization only.

set -euo pipefail

SUB="a18897a6-7e44-457d-9260-f2854c0aca42"
OID="f177a89e-2025-447c-ab31-a4b8dba3751a"
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

# The administrator also needs permission to authorize this endpoint in Azure DevOps.
# This authorizes pipeline 8265 only, not all pipelines.
az rest \
    --method PATCH \
    --url "https://dev.azure.com/azure-sdk/internal/_apis/pipelines/pipelinepermissions/endpoint/$ENDPOINT_ID?api-version=7.1-preview.1" \
    --resource "$ADO_RESOURCE" \
    --headers Content-Type=application/json \
    --body '{"pipelines":[{"id":8265,"authorized":true}]}' \
    --output none

# Resource data roles are reconciled by the pipeline, not preassigned here.
# External signing-key policies, Teams consent, feeds and secure-file permissions
# remain integration prerequisites and are not modified by this file.
