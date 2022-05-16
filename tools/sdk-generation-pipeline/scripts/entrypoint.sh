#!/usr/bin/env bash
set -e
sh /change-owner.sh &
run-mock-host "$@" &
docker-cli "$@"
if [ -f "/tmp/notExit" ]; then
    bash
fi