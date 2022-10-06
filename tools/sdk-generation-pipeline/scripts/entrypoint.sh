#!/usr/bin/env bash
set -e
dockerd > /dev/null 2>&1 &
sh /change-owner.sh &
run-mock-host "$@" &
docker-cli "$@"
if [ -f "/tmp/notExit" ]; then
    bash
fi