#!/usr/bin/env bash

set -euo pipefail

: "${AGENT_PRINCIPAL_ID:?AGENT_PRINCIPAL_ID is required}"
: "${AZURE_SUBSCRIPTION_ID:?AZURE_SUBSCRIPTION_ID is required}"
: "${AZURE_RESOURCE_GROUP:?AZURE_RESOURCE_GROUP is required}"
: "${APP_CONFIG_NAME:?APP_CONFIG_NAME is required}"

access_profile="${1:-candidate}"
if [[ "$access_profile" != "candidate" && "$access_profile" != "primary" ]]; then
  echo "Usage: $0 <candidate|primary>" >&2
  exit 2
fi

app_config_scope="/subscriptions/${AZURE_SUBSCRIPTION_ID}/resourceGroups/${AZURE_RESOURCE_GROUP}/providers/Microsoft.AppConfiguration/configurationStores/${APP_CONFIG_NAME}"

grant_role() {
  local role_id="$1"
  local scope="$2"
  local output
  if output=$(az role assignment create \
    --assignee-object-id "$AGENT_PRINCIPAL_ID" \
    --assignee-principal-type ServicePrincipal \
    --role "$role_id" \
    --scope "$scope" \
    --output none 2>&1); then
    return
  fi
  if grep -Eqi 'RoleAssignmentExists|already exists' <<< "$output"; then
    return
  fi
  printf '%s\n' "$output" >&2
  return 1
}

grant_role 516239f1-63e1-4d78-a4de-a74fb236a071 "$app_config_scope"

read_setting() {
  local key="$1"
  local value=""
  for _ in $(seq 1 12); do
    value=$(az appconfig kv show \
      --name "$APP_CONFIG_NAME" \
      --auth-mode login \
      --key "$key" \
      --query value \
      --output tsv 2>/dev/null || true)
    [[ -n "$value" ]] && break
    sleep 10
  done
  if [[ -z "$value" ]]; then
    echo "App Configuration key '$key' is missing or unreadable." >&2
    exit 1
  fi
  printf '%s' "$value"
}

storage_scope=$(read_setting STORAGE_ACCOUNT_RESOURCE_ID)
search_service_name=$(read_setting AI_SEARCH_SERVICE_NAME)
openai_endpoint=$(read_setting AZURE_OPENAI_ENDPOINT)
ai_resource_name=${openai_endpoint#https://}
ai_resource_name=${ai_resource_name%%.*}

search_scope="/subscriptions/${AZURE_SUBSCRIPTION_ID}/resourceGroups/${AZURE_RESOURCE_GROUP}/providers/Microsoft.Search/searchServices/${search_service_name}"
ai_scope="/subscriptions/${AZURE_SUBSCRIPTION_ID}/resourceGroups/${AZURE_RESOURCE_GROUP}/providers/Microsoft.CognitiveServices/accounts/${ai_resource_name}"

grant_role ba92f5b4-2d11-453d-a403-e96b0029c9fe "$storage_scope"
grant_role 7ca78c08-252a-4471-8644-bb5ff32d4ba0 "$search_scope"
grant_role 8ebe5a00-799e-43f5-93ac-243d3dce84a7 "$search_scope"
grant_role 5e0bd9bd-7b93-4f28-af87-19fc36ad61bd "$ai_scope"
grant_role 53ca6127-db72-4b80-b1b0-d745d6d5456d "$ai_scope"

if [[ "$access_profile" == "primary" ]]; then
  key_vault_endpoint=$(read_setting KEYVAULT_ENDPOINT)
  key_vault_name=${key_vault_endpoint#https://}
  key_vault_name=${key_vault_name%%.*}
  key_vault_scope="/subscriptions/${AZURE_SUBSCRIPTION_ID}/resourceGroups/${AZURE_RESOURCE_GROUP}/providers/Microsoft.KeyVault/vaults/${key_vault_name}"
  grant_role 4633458b-17de-408a-b874-0445c86b69e6 "$key_vault_scope"

  app_insights_scope=$(read_setting AGENT_APPLICATIONINSIGHTS_RESOURCE_ID)
  grant_role 43d0d8ad-25c7-4714-9337-8ba259a9fe05 "$app_insights_scope"

  github_key_vault_endpoint=$(read_setting GITHUB_APP_KEYVAULT_URL)
  github_key_vault_name=${github_key_vault_endpoint#https://}
  github_key_vault_name=${github_key_vault_name%%.*}
  github_key_vault_scope=$(az resource list \
    --name "$github_key_vault_name" \
    --resource-type Microsoft.KeyVault/vaults \
    --query '[0].id' \
    --output tsv)
  if [[ -z "$github_key_vault_scope" ]]; then
    echo "GitHub signing vault '$github_key_vault_name' was not found in the primary subscription." >&2
    exit 1
  fi
  grant_role 12338af0-0e69-4776-bea7-57ae8d297424 "$github_key_vault_scope"

  cosmos_endpoint=$(read_setting AZURE_COSMOSDB_ENDPOINT)
  cosmos_account_name=${cosmos_endpoint#https://}
  cosmos_account_name=${cosmos_account_name%%.*}
  cosmos_role_id="/subscriptions/${AZURE_SUBSCRIPTION_ID}/resourceGroups/${AZURE_RESOURCE_GROUP}/providers/Microsoft.DocumentDB/databaseAccounts/${cosmos_account_name}/sqlRoleDefinitions/00000000-0000-0000-0000-000000000002"
  existing_assignment=$(az cosmosdb sql role assignment list \
    --account-name "$cosmos_account_name" \
    --resource-group "$AZURE_RESOURCE_GROUP" \
    --query "[?principalId=='${AGENT_PRINCIPAL_ID}' && roleDefinitionId=='${cosmos_role_id}'].id | [0]" \
    --output tsv)
  if [[ -z "$existing_assignment" ]]; then
    az cosmosdb sql role assignment create \
      --account-name "$cosmos_account_name" \
      --resource-group "$AZURE_RESOURCE_GROUP" \
      --scope / \
      --principal-id "$AGENT_PRINCIPAL_ID" \
      --role-definition-id "$cosmos_role_id" \
      --output none
  fi
fi

echo "Granted $access_profile data access to hosted agent identity $AGENT_PRINCIPAL_ID."