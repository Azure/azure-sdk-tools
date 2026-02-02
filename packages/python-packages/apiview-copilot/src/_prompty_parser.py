# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""
Module for parsing and executing .prompty files using Azure AI Projects.

This module replaces the prompty library with direct Azure OpenAI calls,
allowing us to keep .prompty files as a human-readable prompt format while
using azure-ai-projects for LLM inference.
"""

import json
import os
import re
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Optional

import yaml


@dataclass
class PromptyConfig:
    """Configuration parsed from a .prompty file."""

    name: str = ""
    description: str = ""
    model_api: str = "chat"
    azure_endpoint: str = ""
    azure_deployment: str = ""
    api_version: str = "2025-03-01-preview"
    parameters: dict = field(default_factory=dict)
    sample: dict = field(default_factory=dict)
    system_template: str = ""
    user_template: str = ""
    response_format: Optional[dict] = None


def _resolve_env_vars(value: str) -> str:
    """Resolve ${env:VAR_NAME} patterns in a string."""
    pattern = r"\$\{env:([^}]+)\}"

    def replacer(match):
        var_name = match.group(1)
        return os.environ.get(var_name, "")

    return re.sub(pattern, replacer, value)


def _load_file_reference(base_path: Path, value: str) -> Any:
    """Load a file reference like ${file:schema.json}."""
    pattern = r"\$\{file:([^}]+)\}"
    match = re.match(pattern, value)
    if match:
        file_name = match.group(1)
        file_path = base_path / file_name
        if file_path.exists():
            with open(file_path, "r", encoding="utf-8") as f:
                if file_name.endswith(".json"):
                    return json.load(f)
                return f.read()
    return value


def _render_template(template: str, variables: dict) -> str:
    """Render a Jinja2-style template with simple {{variable}} substitution."""
    result = template
    for key, value in variables.items():
        # Handle both {{key}} and {{ key }} patterns
        patterns = [f"{{{{{key}}}}}", f"{{{{ {key} }}}}"]
        for pattern in patterns:
            result = result.replace(pattern, str(value) if value is not None else "")
    return result


def parse_prompty(file_path: str | Path) -> PromptyConfig:
    """Parse a .prompty file and extract configuration and templates.

    Args:
        file_path: Path to the .prompty file.

    Returns:
        PromptyConfig with parsed configuration and templates.
    """
    file_path = Path(file_path)
    if not file_path.exists():
        raise FileNotFoundError(f"Prompty file not found: {file_path}")

    with open(file_path, "r", encoding="utf-8") as f:
        content = f.read()

    # Split front matter from content
    # Format: ---\nyaml\n---\ncontent
    parts = content.split("---", 2)
    if len(parts) < 3:
        raise ValueError(f"Invalid prompty format in {file_path}: missing YAML front matter")

    yaml_content = parts[1].strip()
    template_content = parts[2].strip()

    # Parse YAML front matter
    front_matter = yaml.safe_load(yaml_content) or {}

    config = PromptyConfig()
    config.name = front_matter.get("name", "")
    config.description = front_matter.get("description", "")

    # Parse model configuration
    model = front_matter.get("model", {})
    config.model_api = model.get("api", "chat")

    model_config = model.get("configuration", {})
    config.azure_endpoint = _resolve_env_vars(model_config.get("azure_endpoint", ""))
    config.azure_deployment = model_config.get("azure_deployment", "")
    config.api_version = model_config.get("api_version", "2025-03-01-preview")

    # Parse parameters
    params = model.get("parameters", {})
    config.parameters = {k: v for k, v in params.items() if k != "response_format"}

    # Handle response_format - could be a file reference
    response_format = params.get("response_format")
    if isinstance(response_format, str):
        config.response_format = _load_file_reference(file_path.parent, response_format)
    elif isinstance(response_format, dict):
        config.response_format = response_format

    # Parse sample inputs
    config.sample = front_matter.get("sample", {})

    # Parse template sections
    # Look for system: and user: sections
    system_match = re.search(r"^system:\s*\n(.*?)(?=^user:|^assistant:|$)", template_content, re.MULTILINE | re.DOTALL)
    user_match = re.search(r"^user:\s*\n(.*?)(?=^system:|^assistant:|$)", template_content, re.MULTILINE | re.DOTALL)

    if system_match:
        config.system_template = system_match.group(1).strip()
    if user_match:
        config.user_template = user_match.group(1).strip()

    return config


def execute_prompty(
    file_path: str | Path,
    inputs: dict = None,
    configuration: dict = None,
    **kwargs,
) -> Any:
    """Execute a .prompty file using Azure AI Projects.

    Args:
        file_path: Path to the .prompty file.
        inputs: Dictionary of input variables for template rendering.
        configuration: Optional configuration dict (reserved for future use).
        **kwargs: Additional arguments (for compatibility).

    Returns:
        The response from the model, parsed as JSON if possible.

    Raises:
        ValueError: If FOUNDRY_ENDPOINT or FOUNDRY_PROJECT are not configured.
    """
    from azure.ai.projects import AIProjectClient
    from azure.identity import DefaultAzureCredential
    from src._settings import SettingsManager

    config = parse_prompty(file_path)
    inputs = inputs or {}

    # Merge sample inputs with provided inputs (provided inputs take precedence)
    merged_inputs = {**config.sample, **inputs}

    # Render templates
    system_content = _render_template(config.system_template, merged_inputs)
    user_content = _render_template(config.user_template, merged_inputs)

    # Get settings
    settings = SettingsManager()

    # Use Azure AI Projects client for inference
    foundry_endpoint = settings.get("FOUNDRY_ENDPOINT")
    foundry_project = settings.get("FOUNDRY_PROJECT")

    if not foundry_endpoint or not foundry_project:
        raise ValueError(
            "FOUNDRY_ENDPOINT and FOUNDRY_PROJECT must be configured in AppConfiguration to execute prompty files."
        )

    credential = DefaultAzureCredential()
    client = AIProjectClient(
        endpoint=foundry_endpoint,
        project_name=foundry_project,
        credential=credential,
    )

    # Build messages
    messages = []
    if system_content:
        messages.append({"role": "system", "content": system_content})
    messages.append({"role": "user", "content": user_content})

    # Build completion parameters
    completion_params = {
        "model": config.azure_deployment,
        "messages": messages,
    }

    # Add optional parameters
    if config.parameters.get("max_completion_tokens"):
        completion_params["max_tokens"] = config.parameters["max_completion_tokens"]
    if config.parameters.get("temperature") is not None:
        completion_params["temperature"] = config.parameters["temperature"]
    if config.parameters.get("frequency_penalty") is not None:
        completion_params["frequency_penalty"] = config.parameters["frequency_penalty"]
    if config.parameters.get("presence_penalty") is not None:
        completion_params["presence_penalty"] = config.parameters["presence_penalty"]

    # Handle response format
    if config.response_format:
        completion_params["response_format"] = {"type": "json_object"}

    # Make the inference call
    response = client.inference.chat.completions.create(**completion_params)

    # Extract content from response
    result_content = response.choices[0].message.content

    # Try to parse as JSON if we have a response format
    if config.response_format:
        try:
            return json.loads(result_content)
        except json.JSONDecodeError:
            pass

    return result_content
