#!/bin/sh

# sudo apt -y install golang-go

go version

export GOPATH=$HOME/go
export PATH=$PATH:$GOPATH/bin

python3 go/main.py "$@"
