#!/usr/bin/env bash
set -e

run-mockhost "$@" >> /tmp/output/mockHost.log &

sleep 5s

docker-cli "$@"