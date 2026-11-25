#!/usr/bin/env bash

set -euo pipefail

: "${1:?Manifest path is required}"
: "${2:?Job-local installation directory is required}"
manifest_path="$1"
install_directory="$2"
constraint=$(yq -r '.requiredVersions.azd' "$manifest_path")
if [[ ! "$constraint" =~ ^=[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    echo "Expected an exact stable azd version in $manifest_path, got '$constraint'." >&2
    exit 1
fi
expected_version="${constraint#=}"

read_version() {
    "$install_directory/azd" version |
        sed -n 's/^azd version \([^ ]*\).*/\1/p'
}

installed_version=""
if [[ -x "$install_directory/azd" ]]; then
    installed_version=$(read_version)
fi
if [[ "$installed_version" != "$expected_version" ]]; then
    mkdir -p "$install_directory"
    curl -fsSL https://aka.ms/install-azd.sh |
        bash -s -- --version "$expected_version" \
            --install-folder "$install_directory" \
            --symlink-folder "$install_directory"
fi

installed_version=$(read_version)
if [[ "$installed_version" != "$expected_version" ]]; then
    echo "Expected azd $expected_version after installation, got '$installed_version'." >&2
    exit 1
fi
"$install_directory/azd" version
if [[ -n "${TF_BUILD:-}" ]]; then
    echo "##vso[task.prependpath]$install_directory"
fi
