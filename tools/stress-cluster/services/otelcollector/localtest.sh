#!/bin/bash

set -ex

docker build --no-cache -t oteltest .

docker run -it \
    -e ENV_FILE=/.env \
    -v `pwd`/.env:/.env \
    -P \
    oteltest

