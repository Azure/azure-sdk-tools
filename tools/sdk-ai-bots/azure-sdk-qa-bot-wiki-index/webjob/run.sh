#!/bin/sh
set -eu
JOB_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
PYTHON="$JOB_ROOT/runtime/bin/python3"
[ "$(uname -s)" = Linux ] && [ "$(uname -m)" = x86_64 ] || { echo 'Linux x64 is required' >&2; exit 1; }
[ -x "$PYTHON" ] || { echo 'Bundled Python is not executable' >&2; exit 1; }
export PATH="$JOB_ROOT/runtime/bin:$PATH"
export PYTHONPATH="$JOB_ROOT/site-packages:$JOB_ROOT"
: "${AZURE_APPCONFIG_ENDPOINT:?App Configuration endpoint is required}"
cd "$JOB_ROOT"
exec "$PYTHON" -m wiki_index.main --min-docs "${WIKI_MIN_DOCS:-2}"
