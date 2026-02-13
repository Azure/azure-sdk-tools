# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""
Module for parsing and executing .prompty prompt template files using Azure AI Foundry.

This module provides direct Azure AI inference calls using .prompty files
as a human-readable prompt template format, along with retry-based prompt
execution helpers.
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


def _json_default(obj):
    """JSON encoder for non-serializable types."""
    from datetime import date, datetime

    if isinstance(obj, (date, datetime)):
        return obj.isoformat()
    raise TypeError(f"Object of type {type(obj).__name__} is not JSON serializable")


def _render_template(template: str, variables: dict) -> str:
    """Render a Jinja2-style template with simple {{variable}} substitution."""
    result = template
    for key, value in variables.items():
        # Handle both {{key}} and {{ key }} patterns
        patterns = [f"{{{{{key}}}}}", f"{{{{ {key} }}}}"]
        # Convert dicts/lists to JSON for proper formatting
        if isinstance(value, (dict, list)):
            str_value = json.dumps(value, indent=2, default=_json_default)
        elif value is not None:
            str_value = str(value)
        else:
            str_value = ""
        for pattern in patterns:
            result = result.replace(pattern, str_value)
    return result


def _parse_prompty(file_path: str | Path) -> PromptyConfig:
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
    # Note: Use \Z for end-of-string (not $ which matches end-of-line in MULTILINE mode)
    system_match = re.search(r"^system:\s*\n(.*?)(?=^user:|^assistant:|\Z)", template_content, re.MULTILINE | re.DOTALL)
    user_match = re.search(r"^user:\s*\n(.*?)(?=^system:|^assistant:|\Z)", template_content, re.MULTILINE | re.DOTALL)

    if system_match:
        config.system_template = system_match.group(1).strip()
    if user_match:
        config.user_template = user_match.group(1).strip()

    return config


def _execute_prompt_template(  # pylint: disable=unused-argument
    file_path: str | Path,
    inputs: dict = None,
    configuration: dict = None,
    **kwargs,
) -> Any:
    """Execute a .prompty template file using Azure AI Foundry.

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
    # azure.ai.inference is a transitive dependency of azure-ai-projects
    from azure.ai.inference import ChatCompletionsClient
    from azure.ai.inference.models import SystemMessage, UserMessage
    from azure.identity import DefaultAzureCredential
    from src._settings import SettingsManager

    config = _parse_prompty(file_path)
    inputs = inputs or {}

    # Merge sample inputs with provided inputs (provided inputs take precedence)
    merged_inputs = {**config.sample, **inputs}

    # Render templates
    system_content = _render_template(config.system_template, merged_inputs)
    user_content = _render_template(config.user_template, merged_inputs)

    # Get settings
    settings = SettingsManager()

    # Use Azure AI Foundry endpoint for inference
    foundry_endpoint = settings.get("FOUNDRY_ENDPOINT")
    foundry_project = settings.get("FOUNDRY_PROJECT")

    if not foundry_endpoint or not foundry_project:
        raise ValueError(
            "FOUNDRY_ENDPOINT and FOUNDRY_PROJECT must be configured in AppConfiguration to execute prompty files."
        )

    # Construct the inference endpoint (similar to how agents does it)
    # Format: {FOUNDRY_ENDPOINT}/models
    inference_endpoint = f"{foundry_endpoint.rstrip('/')}/models"

    credential = DefaultAzureCredential()
    # Specify the cognitive services scope for Azure AI
    client = ChatCompletionsClient(
        endpoint=inference_endpoint,
        credential=credential,
        credential_scopes=["https://cognitiveservices.azure.com/.default"],
    )

    # Build messages
    messages = []
    if system_content:
        messages.append(SystemMessage(content=system_content))
    messages.append(UserMessage(content=user_content))

    # Separate SDK-supported parameters from model-specific extras
    # The azure-ai-inference SDK accepts these directly:
    sdk_params = {"frequency_penalty", "presence_penalty", "seed"}

    completion_params = {
        "model": config.azure_deployment,
        "messages": messages,
    }
    model_extras = {}

    for key, value in config.parameters.items():
        if key in sdk_params:
            completion_params[key] = value
        else:
            # Pass non-SDK params (like max_completion_tokens, reasoning_effort) as model extras
            model_extras[key] = value

    if model_extras:
        completion_params["model_extras"] = model_extras

    # Handle response format
    # The azure-ai-inference SDK only supports "json_object" or "text" as response_format
    # strings. For json_schema configs, we embed the schema in the system prompt and use
    # "json_object" mode so the model still produces structured output.
    if config.response_format:
        completion_params["response_format"] = "json_object"
        schema_instruction = ""
        if isinstance(config.response_format, dict) and config.response_format.get("type") == "json_schema":
            schema_body = config.response_format.get("json_schema", {}).get("schema", {})
            schema_instruction = (
                "\n\nYou must respond with a JSON object that conforms to the following JSON schema:\n"
                + json.dumps(schema_body, indent=2)
            )
        # The API requires the word "json" in the messages when using json_object response_format.
        if system_content:
            if schema_instruction:
                system_content += schema_instruction
            elif "json" not in system_content.lower():
                system_content += "\n\nYou must respond in JSON format."
            completion_params["messages"][0] = SystemMessage(content=system_content)
        elif "json" not in user_content.lower():
            user_content += schema_instruction or "\n\nYou must respond in JSON format."
            completion_params["messages"][-1] = UserMessage(content=user_content)

    # Make the inference call
    response = client.complete(**completion_params)

    # Extract content from response
    result_content = response.choices[0].message.content

    # Try to parse as JSON if we have a response format
    if config.response_format:
        try:
            return json.loads(result_content)
        except json.JSONDecodeError:
            pass

    return result_content


def _run_prompt_template(*, folder: str, filename: str, inputs: dict = None, **kwargs) -> Any:
    """
    Run a prompt template file with the given inputs.

    :param folder: Folder containing the prompt file.
    :param filename: Name of the prompt file.
    :param inputs: Dictionary of inputs for the prompt.
    :param kwargs: Additional keyword arguments passed to the execute function.
    """
    from src._settings import SettingsManager
    from src._utils import get_prompt_path

    settings = SettingsManager()
    os.environ["OPENAI_ENDPOINT"] = settings.get("OPENAI_ENDPOINT")
    prompt_path = get_prompt_path(folder=folder, filename=filename)
    return _execute_prompt_template(prompt_path, inputs=inputs, **kwargs)


def run_prompt(
    folder: str,
    filename: str,
    inputs: dict,
    settings=None,
    max_retries: int = 5,
    logger: Optional[object] = None,
) -> str:
    """
    Run a prompt with retry logic.

    Args:
        folder: Folder containing the prompt file
        filename: Name of the prompt file
        inputs: Dictionary of inputs for the prompt
        settings: Optional settings object for API keys, etc.
        max_retries: Maximum number of retry attempts (default: 5)
        logger: Optional logger for warnings/errors

    Returns:
        String result of the prompt execution

    Raises:
        Exception: If all retry attempts fail
    """
    from src._credential import in_ci
    from src._retry import retry_with_backoff
    from src._settings import SettingsManager

    def execute_prompt() -> str:
        if in_ci():
            configuration = {"api_key": (settings or SettingsManager()).get("OPENAI_API_KEY")}
        else:
            configuration = {}
        return _run_prompt_template(folder=folder, filename=filename, inputs=inputs, configuration=configuration)

    def on_retry(exception, attempt, max_attempts):
        if logger:
            logger.warning(f"Error executing prompt {filename}, attempt {attempt+1}/{max_attempts}: {str(exception)}")

    def on_failure(exception, attempt):
        if logger:
            logger.error(f"Failed to execute prompt {filename} after {attempt} attempts: {str(exception)}")
        raise exception

    return retry_with_backoff(
        func=execute_prompt,
        max_retries=max_retries,
        retry_exceptions=(json.JSONDecodeError, Exception),
        on_retry=on_retry,
        on_failure=on_failure,
        logger=logger,
        description=f"prompt {filename}",
    )
