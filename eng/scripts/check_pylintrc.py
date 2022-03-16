import os
import requests
import sys

# This script ensures that the `pylintrc` file in the azure-sdk-for-python repo matches the
# `azure_sdk_pylintrc` file stored in this repo.

SDK_FILE_URL = "https://raw.github.com/Azure/azure-sdk-for-python/main/pylintrc"
ROOT_PATH = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
LOCAL_FILE_PATH = os.path.join(ROOT_PATH, "packages", "python-packages", "api-stub-generator", "azure_sdk_pylintrc")

sdk_file = requests.get(SDK_FILE_URL).content.decode()
with open(LOCAL_FILE_PATH, "r") as f:
    local_file = f.read()

if hash(sdk_file) == hash(local_file):
    sys.exit(0)
else:
    print("The `pylintrc` file in the azure-sdk-for-python repo does match the copy in the azure-sdk-tools repo! Please update the local copy.")
    sys.exit(1)
