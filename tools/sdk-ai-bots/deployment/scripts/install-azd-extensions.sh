#!/usr/bin/env bash

set -euo pipefail

manifest_path="${1:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)/azure.yaml}"
extension_versions=$(yq -o=json '.requiredVersions.extensions' "$manifest_path")
required_extensions=(
    azure.ai.inspector
    azure.ai.projects
    azure.ai.connections
    azure.ai.toolboxes
    azure.ai.agents
)
for extension_id in "${required_extensions[@]}"; do
    constraint=$(jq -r --arg id "$extension_id" '.[$id] // ""' <<< "$extension_versions")
    if [[ "$constraint" != =* || "$constraint" == "=" ]]; then
        echo "Expected an exact $extension_id version in $manifest_path, got '$constraint'." >&2
        exit 1
    fi
done
for extension_id in "${required_extensions[@]}"; do
    constraint=$(jq -r --arg id "$extension_id" '.[$id]' <<< "$extension_versions")
    azd extension install "$extension_id" \
        --source azd \
        --version "${constraint#=}" \
        --no-prompt
done

installed_extensions=$(azd extension list --installed --output json)
for extension_id in "${required_extensions[@]}"; do
    constraint=$(jq -r --arg id "$extension_id" '.[$id]' <<< "$extension_versions")
    jq -e --arg id "$extension_id" --arg version "${constraint#=}" \
        'any(.[]; .id == $id and .installedVersion == $version)' \
        <<< "$installed_extensions" >/dev/null || {
        echo "Required azd extension '$extension_id' is missing or has an unexpected installed version." >&2
        exit 1
    }
done
