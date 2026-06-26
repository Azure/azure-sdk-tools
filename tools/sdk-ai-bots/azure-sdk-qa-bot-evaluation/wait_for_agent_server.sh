#!/usr/bin/env bash
# Wait until the local agent /completion endpoint is ready (HTTP 200), warming the
# server so the evaluation's first concurrent burst doesn't hit cold-start 500s.
#
# Usage: wait_for_agent_server.sh [server_log_path]
#   server_log_path - optional; tailed to the build log if readiness times out.
set -uo pipefail

LOG="${1:-}"
URL="http://localhost:8089/completion"
MAX_ATTEMPTS=20
SLEEP_SECONDS=10

echo "Waiting for agent server readiness at ${URL} ..."
for i in $(seq 1 "${MAX_ATTEMPTS}"); do
  code=$(curl -s -o /dev/null -w "%{http_code}" --max-time 150 -X POST "${URL}" \
    -H "Content-Type: application/json" \
    -d '{"tenant_id":"general_qa_bot","message":{"role":"user","content":"readiness ping"},"with_full_context":true}' \
    || echo 000)
  if [ "${code}" = "200" ]; then
    echo "Agent server ready (attempt ${i})."
    exit 0
  fi
  echo "Not ready yet (HTTP ${code}); attempt ${i}/${MAX_ATTEMPTS}, retrying in ${SLEEP_SECONDS}s."
  sleep "${SLEEP_SECONDS}"
done

echo "##[error]Agent server did not become ready after ${MAX_ATTEMPTS} attempts."
if [ -n "${LOG}" ] && [ -f "${LOG}" ]; then
  echo "----- last 200 lines of ${LOG} -----"
  tail -n 200 "${LOG}" || true
fi
exit 1
