# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""Plugin for utility functions in the APIView Copilot."""

import json
import os
import tempfile
from urllib.parse import urlparse

import requests
from src._diff import create_diff_with_line_numbers
from src._utils import run_prompty
from src.agent.tools._base import Tool


class UtilityTools(Tool):
    """Utility tools for APIView Copilot."""

    def _download_if_url(self, file_path: str) -> str:
        """
        If file_path is a URL, download it to a temp file and return the local path.
        Otherwise, return the original file_path.
        """
        parsed = urlparse(file_path)
        if parsed.scheme in ("http", "https"):
            response = requests.get(file_path, timeout=30)
            response.raise_for_status()
            suffix = os.path.splitext(parsed.path)[-1]
            with tempfile.NamedTemporaryFile(delete=False, suffix=suffix, mode="wb") as tmp:
                tmp.write(response.content)
                return tmp.name
        return file_path

    async def summarize_api(self, api: str, language: str):
        """
        Summarize the provided API.
        :param api: The API content to summarize.
        :param language: The programming language of the API.
        :return: Summary of the API.
        """
        response = run_prompty(
            folder="summarize", filename="summarize_api", inputs={"content": api, "language": language}
        )
        return response

    async def summarize_api_diff(self, target: str, base: str, language: str):
        """
        Summarize the differences between the provided APIs.
        :param target: The target (new) API to compare.
        :param base: The base (old) API to compare against.
        :param language: The programming language of the APIs.
        :return: Summary of the differences.
        """
        api_diff = create_diff_with_line_numbers(old=base, new=target)
        response = run_prompty(
            folder="summarize", filename="summarize_diff", inputs={"content": api_diff, "language": language}
        )
        return response

    async def load_json_file(self, file_path: str):
        """
        Load a JSON file from the specified path or URL.
        :param file_path: The path or URL to the JSON file.
        :return: The JSON content as a pretty-printed string.
        """
        file_path = self._download_if_url(file_path)
        if not os.path.exists(file_path):
            raise FileNotFoundError(f"File not found: {file_path}")
        try:
            with open(file_path, "r", encoding="utf-8") as file:
                json_content = json.load(file)
                return json.dumps(json_content, indent=2)
        except json.JSONDecodeError as e:
            raise ValueError(f"Error decoding JSON from file {file_path}.") from e
        except Exception as e:
            raise ValueError(f"Error reading JSON file {file_path}.") from e

    async def load_text_file(self, file_path: str):
        """
        Load a text file from the specified path or URL.
        :param file_path: The path or URL to the text file.
        :return: The content of the text file as a string.
        """
        file_path = self._download_if_url(file_path)
        if not os.path.exists(file_path):
            raise FileNotFoundError(f"File not found: {file_path}")
        try:
            with open(file_path, "r", encoding="utf-8") as file:
                return file.read()
        except Exception as e:
            raise ValueError(f"Error reading text file {file_path}.") from e
