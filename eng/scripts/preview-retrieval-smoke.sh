#!/usr/bin/env bash
set -euo pipefail
python3 "$(dirname "$0")/preview_retrieval_smoke.py" "$1"
