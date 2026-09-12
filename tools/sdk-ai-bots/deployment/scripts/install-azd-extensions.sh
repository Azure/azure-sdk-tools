#!/usr/bin/env bash

set -euo pipefail

manifest_path="${1:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)/azure.yaml}"
extension_constraint=$(yq -r '.requiredVersions.extensions."azure.ai.agents"' "$manifest_path")
if [[ "$extension_constraint" != =* ]]; then
    echo "Expected an exact azure.ai.agents version in $manifest_path, got '$extension_constraint'." >&2
    exit 1
fi

azd extension install azure.ai.agents \
    --source azd \
    --version "${extension_constraint#=}" \
    --no-prompt

required_extensions=(
    azure.ai.agents
    azure.ai.inspector
    azure.ai.projects
)
installed_extensions=$(azd extension list --installed --output json)
for extension_id in "${required_extensions[@]}"; do
    jq -e --arg id "$extension_id" \
        'any(.[]; .id == $id and .installedVersion != "")' \
        <<< "$installed_extensions" >/dev/null || {
        echo "Required azd extension '$extension_id' is not installed." >&2
        exit 1
    }
done
