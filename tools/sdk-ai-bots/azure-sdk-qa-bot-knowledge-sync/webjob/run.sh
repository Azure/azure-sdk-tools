#!/bin/sh
set -eu
JOB_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
NODE="$JOB_ROOT/runtime/bin/node"
# Never fall back to the backend's Node 22 or install anything on the host.
[ "$(uname -s)" = Linux ] && [ "$(uname -m)" = x86_64 ] || { echo 'Linux x64 is required' >&2; exit 1; }
[ -x "$NODE" ] || { echo 'Bundled Node is not executable' >&2; exit 1; }
export PATH="$JOB_ROOT/runtime/bin:$PATH"
[ "$#" -eq 0 ] || { echo 'Sync arguments are not accepted' >&2; exit 1; }
: "${AZURE_APPCONFIG_ENDPOINT:?App Configuration endpoint is required}"
# Use the existing app environment without overriding its identity or configuration.
cd "$JOB_ROOT"
exec "$NODE" "$JOB_ROOT/dist/src/index.js"