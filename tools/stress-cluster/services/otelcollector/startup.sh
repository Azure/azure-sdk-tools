#!/bin/sh

set -ex

source $ENV_FILE
/otelcol-contrib --config otel-collector-config.yml $@
