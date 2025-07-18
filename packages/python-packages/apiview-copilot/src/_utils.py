# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""
Module of utility functions for APIView Copilot.
"""

import os


def get_language_pretty_name(language: str) -> str:
    """
    Returns a pretty name for the language.
    """
    language_pretty_names = {
        "android": "Android",
        "cpp": "C++",
        "dotnet": "C#",
        "golang": "Go",
        "ios": "Swift",
        "java": "Java",
        "python": "Python",
        "typescript": "TypeScript",
    }
    return language_pretty_names.get(language, language.capitalize())


def get_prompt_path(*, folder: str, filename: str) -> str:
    """
    Returns the full path to a prompt file.
    Args:
        folder (str): The folder containing the prompt.
        filename (str): The name of the prompt file.
    """
    # if filename doesn't end with .prompty, append it
    if not filename.endswith(".prompty"):
        filename += ".prompty"

    # Set up paths
    package_root = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
    prompts_folder = os.path.join(package_root, "prompts")

    prompt_path = os.path.abspath(os.path.join(prompts_folder, folder, filename))
    if not os.path.exists(prompt_path):
        raise FileNotFoundError(f"Prompt file not found: {prompt_path}")
    return prompt_path
