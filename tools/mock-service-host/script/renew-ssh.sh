#!/bin/bash

BASEDIR="$( cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )"
openssl req -new -newkey rsa:4096 -days 3650 -nodes -x509 -subj "/CN=127.0.0.1/ST=Shanghai/L=Shanghai/O=ame" -keyout ${BASEDIR}/../.ssh/127-0-0-1-ca.pem -out ${BASEDIR}/../.ssh/127-0-0-1-ca.crt
openssl req -new -newkey rsa:4096 -days 3650 -nodes -x509 -subj "/CN=localhost/ST=Shanghai/L=Shanghai/O=ame" -keyout ${BASEDIR}/../.ssh/localhost-ca.pem -out ${BASEDIR}/../.ssh/localhost-ca.crt
