import json
import os
import prompty
import prompty.azure_beta
from semantic_kernel.functions import kernel_function

from src._diff import create_diff_with_line_numbers

# Set up paths
_PACKAGE_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
_PROMPTS_FOLDER = os.path.join(_PACKAGE_ROOT, "prompts")


class UtilityPlugin:

    @kernel_function(description="Summarize the provided API.")
    async def summarize_api(self, api: str, language: str):
        """
        Summarize the provided API.
        Args:
            api (str): The API to summarize.
            language (str): The programming language of the API.
        """
        prompt_path = os.path.join(_PROMPTS_FOLDER, "summarize_api.prompty")
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
        prompt_path = os.path.join(_PROMPTS_FOLDER, "summarize_diff.prompty")
        if not os.path.exists(prompt_path):
            raise FileNotFoundError(f"Prompt file not found: {prompt_path}")
        api_diff = create_diff_with_line_numbers(old=base, new=target)
        response = prompty.execute(prompt_path, inputs={"content": api_diff, "language": language}, configuration={})
        return response

    @kernel_function(description="Load a JSON file from the specified path.")
    async def load_json_file(self, file_path: str):
        """
        Load a JSON file from the specified path.
        Args:
            file_path (str): The path to the JSON file.
        """
        if not os.path.exists(file_path):
            raise FileNotFoundError(f"File not found: {file_path}")
        try:
            with open(file_path, "r", encoding="utf-8") as file:
                json_content = json.load(file)
                return json.dumps(json_content, indent=2)
        except json.JSONDecodeError as e:
            raise ValueError(f"Error decoding JSON from file {file_path}: {e}")

    @kernel_function(description="Load a text file from the specified path.")
    async def load_text_file(self, file_path: str):
        """
        Load a text file from the specified path.
        Args:
            file_path (str): The path to the text file.
        """
        if not os.path.exists(file_path):
            raise FileNotFoundError(f"File not found: {file_path}")
        try:
            with open(file_path, "r", encoding="utf-8") as file:
                return file.read()
        except Exception as e:
            raise ValueError(f"Error reading text file {file_path}: {e}")
