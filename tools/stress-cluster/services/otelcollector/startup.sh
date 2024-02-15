#!/bin/sh

set -ex

source $ENV_FILE
echo "    connection_string: \"$APPLICATIONINSIGHTS_CONNECTION_STRING\"" >> /otel-collector-config.yml

cat /otel-collector-config.yml

/otelcol-contrib --config otel-collector-config.yml $@
