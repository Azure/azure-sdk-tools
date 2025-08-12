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

import prompty
import prompty.azure_beta
import requests
from semantic_kernel.functions import kernel_function
from src._diff import create_diff_with_line_numbers
from src._utils import get_prompt_path


class UtilityPlugin:
    """Utility plugin for APIView Copilot."""

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

    @kernel_function(description="Summarize the provided API.")
    async def summarize_api(self, api: str, language: str):
        """
        Summarize the provided API.
        Args:
            api (str): The API to summarize.
            language (str): The programming language of the API.
        """
        prompt_path = get_prompt_path(folder="summarize", filename="summarize_api.prompty")
        if not os.path.exists(prompt_path):
            raise FileNotFoundError(f"Prompt file not found: {prompt_path}")
        response = prompty.execute(prompt_path, inputs={"content": api, "language": language}, configuration={})
        return response

    @kernel_function(description="Summarize the differences between the provided APIs.")
    async def summarize_api_diff(self, target: str, base: str, language: str):
        """
        Summarize the differences between the provided APIs.
        Args:
            target (str): The target (new) API to compare.
            base (str): The base (old) API to compare against.
            language (str): The programming language of the APIs.
        """
        prompt_path = get_prompt_path(folder="summarize", filename="summarize_diff.prompty")
        if not os.path.exists(prompt_path):
            raise FileNotFoundError(f"Prompt file not found: {prompt_path}")
        api_diff = create_diff_with_line_numbers(old=base, new=target)
        response = prompty.execute(prompt_path, inputs={"content": api_diff, "language": language}, configuration={})
        return response

    @kernel_function(description="Load a JSON file from the specified path or URL.")
    async def load_json_file(self, file_path: str):
        """
        Load a JSON file from the specified path or URL.
        Args:
            file_path (str): The path or URL to the JSON file.
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

    @kernel_function(description="Load a text file from the specified path or URL.")
    async def load_text_file(self, file_path: str):
        """
        Load a text file from the specified path or URL.
        Args:
            file_path (str): The path or URL to the text file.
        """
        file_path = self._download_if_url(file_path)
        if not os.path.exists(file_path):
            raise FileNotFoundError(f"File not found: {file_path}")
        try:
            with open(file_path, "r", encoding="utf-8") as file:
                return file.read()
        except Exception as e:
            raise ValueError(f"Error reading text file {file_path}.") from e
