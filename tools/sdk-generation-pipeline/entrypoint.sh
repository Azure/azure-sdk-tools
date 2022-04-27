#!/usr/bin/env bash
set -e

run-mock-host "$@" &
MOCK_HOST_PID=$!

docker-cli "$@"

if [ -f "/tmp/notExit" ]; then
    wait $MOCK_HOST_PID
fi