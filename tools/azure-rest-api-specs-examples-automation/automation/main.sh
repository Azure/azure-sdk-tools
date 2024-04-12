#!/bin/sh

# sudo apt -y install python3-pip

pip3 install -r automation/requirements.txt

python3 automation/main.py "$@"
