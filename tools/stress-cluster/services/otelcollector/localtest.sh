#!/bin/bash

docker build -t oteltest .
docker run -it \
    -e ENV_FILE=/.env \
    -v `pwd`/.env:/.env \
    oteltest

