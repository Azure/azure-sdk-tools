#!/usr/bin/env bash

set -euo pipefail

if [[ $# -ne 1 ]]; then
  echo "Usage: $0 <app-configuration-indexer-key>" >&2
  exit 2
fi

: "${APP_CONFIG_NAME:?APP_CONFIG_NAME must name the deployment App Configuration store}"

indexer_key="$1"
search_base_url=$(az appconfig kv show \
  --name "$APP_CONFIG_NAME" \
  --auth-mode login \
  --key AI_SEARCH_BASE_URL \
  --query value \
  --output tsv)
indexer_name=$(az appconfig kv show \
  --name "$APP_CONFIG_NAME" \
  --auth-mode login \
  --key "$indexer_key" \
  --query value \
  --output tsv)

if [[ -z "$search_base_url" || -z "$indexer_name" ]]; then
  echo "Search endpoint or indexer setting '$indexer_key' is empty." >&2
  exit 1
fi

search_token=$(az account get-access-token \
  --resource https://search.azure.com \
  --query accessToken \
  --output tsv)
response_file=$(mktemp)
trap 'rm -f "$response_file"' EXIT

status_code=$(curl --silent --show-error \
  --output "$response_file" \
  --write-out '%{http_code}' \
  --request POST \
  --header "Authorization: Bearer $search_token" \
  "${search_base_url}/indexers/${indexer_name}/run?api-version=2026-04-01")

if [[ "$status_code" != 202 && "$status_code" != 409 ]]; then
  cat "$response_file" >&2
  echo "Failed to start Search indexer '$indexer_name' (HTTP $status_code)." >&2
  exit 1
fi

echo "Search indexer '$indexer_name' accepted (HTTP $status_code)."