# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""
Parse the Azure SDK guidelines from markdown files into JSON format.
This script reads markdown files from the Azure SDK repository and converts them into JSON format
for easier consumption by tools and applications.
"""

import json
import os
import sys

from scripts._markdown_parser import parse_markdown

# Add the parent directory to sys.path so imports work correctly
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
_PACKAGE_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
_GUIDELINES_FOLDER = os.path.join(_PACKAGE_ROOT, "guidelines")

if __name__ == "__main__":

    azure_sdk_path = os.getenv("AZURE_SDK_REPO_PATH")
    rest_api_guidelines_path = os.getenv("REST_API_GUIDELINES_PATH")
    if not azure_sdk_path:
        raise ValueError("Please set the AZURE_SDK_REPO_PATH environment variable manually or in your .env file.")
    else:
        azure_sdk_path = os.path.normpath(azure_sdk_path)
    if not rest_api_guidelines_path:
        raise ValueError("Please set the REST_API_GUIDELINES_PATH environment variable manually or in your .env file.")
    else:
        rest_api_guidelines_path = os.path.normpath(rest_api_guidelines_path)

    # Generate Azure SDK JSON
    sdk_folders_to_parse = [
        "android",
        "clang",
        "cpp",
        "dotnet",
        "general",
        "golang",
        "ios",
        "java",
        "python",
        "rust",
        "typescript",
    ]
    files_to_parse = [
        "design.md",
        "implementation.md",
        "introduction.md",
        "azurecore.md",
        "compatibility.md",
        "documentation.md",
        "spring.md",
    ]
    for folder in sdk_folders_to_parse:
        for root, dirs, files in os.walk(os.path.join(azure_sdk_path, "docs", folder)):
            for file in files:
                if file in files_to_parse:
                    file_path = os.path.join(root, file)
                    results = parse_markdown(file_path, azure_sdk_path)
                    json_str = json.dumps(results, indent=2)
                    filename = os.path.splitext(os.path.basename(file_path))[0]
                    json_filename = filename + ".json"
                    json_path = os.path.join(_GUIDELINES_FOLDER, folder, json_filename)
                    os.makedirs(os.path.dirname(json_path), exist_ok=True)
                    with open(json_path, "w", encoding="utf-8") as f:
                        f.write(json_str)
    # Generate the REST API Guidelines JSON
    guidelines_path = os.path.join(rest_api_guidelines_path, "azure", "Guidelines.md")
    results = parse_markdown(guidelines_path, rest_api_guidelines_path)
    json_path = os.path.join(_GUIDELINES_FOLDER, "rest", "guidelines.json")
    json_str = json.dumps(results, indent=2)
    os.makedirs(os.path.dirname(json_path), exist_ok=True)
    with open(json_path, "w", encoding="utf-8") as f:
        f.write(json_str)
