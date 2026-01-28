# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""
Module for calling Azure AI Foundry Models (chat completions).

This module handles direct model inference via the Foundry Models endpoint.
For agent-based interactions with tools, see src/agent/_agent.py (Foundry Agent).

Key functions:
- parse_prompty_file: Parse .prompty files into PromptyConfig
- build_messages_from_prompty: Build messages from config + inputs
- call_foundry_model: Call the Foundry Models endpoint
- run_prompty: Convenience function combining parse + build + call
"""

from __future__ import annotations

import json
import logging
import os
import re
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Dict, List, Optional

import yaml
from jinja2 import Template

logger = logging.getLogger(__name__)


@dataclass
class PromptyConfig:
    """Configuration parsed from a prompty file."""

    name: str = ""
    description: str = ""
    authors: List[str] = field(default_factory=list)
    version: str = "1.0.0"

    # Model configuration
    api: str = "chat"
    model_type: str = "azure_openai"
    azure_endpoint: str = ""
    azure_deployment: str = ""
    api_version: str = "2025-03-01-preview"

    # Model parameters (raw dict from prompty file)
    # Keys depend on the model - may include: max_tokens, temperature, top_p,
    # frequency_penalty, presence_penalty, reasoning_effort, response_format, etc.
    model_parameters: Dict[str, Any] = field(default_factory=dict)

    # Sample data (for testing)
    sample: Dict[str, Any] = field(default_factory=dict)

    # Message templates
    system_template: Optional[str] = None
    user_template: Optional[str] = None

    # Raw content (if no system/user split)
    content_template: Optional[str] = None

    # Source file path
    source_path: str = ""


def _resolve_env_vars(value: str) -> str:
    """Resolve ${env:VAR_NAME} placeholders in a string."""
    pattern = r"\$\{env:([^}]+)\}"

    def replacer(match):
        var_name = match.group(1)
        return os.environ.get(var_name, "")

    return re.sub(pattern, replacer, value)


def _resolve_file_ref(value: str, base_path: Path) -> Optional[Dict]:
    """Resolve ${file:filename.json} references to load JSON schema files."""
    if not isinstance(value, str):
        return value

    pattern = r"\$\{file:([^}]+)\}"
    match = re.match(pattern, value)
    if match:
        filename = match.group(1)
        file_path = base_path / filename
        if file_path.exists():
            with open(file_path, "r", encoding="utf-8") as f:
                return json.load(f)
        else:
            logger.warning(f"Referenced file not found: {file_path}")
            return None
    return value


def parse_prompty_file(file_path: str) -> PromptyConfig:
    """
    Parse a .prompty file and extract its configuration.

    Args:
        file_path: Path to the .prompty file.

    Returns:
        PromptyConfig with parsed settings and templates.
    """
    path = Path(file_path)
    if not path.exists():
        raise FileNotFoundError(f"Prompty file not found: {file_path}")

    logger.debug("Parsing prompty file: %s", path)

    with open(path, "r", encoding="utf-8") as f:
        content = f.read()

    # Split frontmatter from content
    # Format: ---\nyaml\n---\ncontent
    parts = content.split("---", 2)
    if len(parts) < 3:
        raise ValueError(f"Invalid prompty format - missing YAML frontmatter: {file_path}")

    yaml_content = parts[1].strip()
    body_content = parts[2].strip()

    # Parse YAML frontmatter
    frontmatter = yaml.safe_load(yaml_content)
    if not frontmatter:
        frontmatter = {}

    config = PromptyConfig(source_path=str(path))

    # Basic metadata
    config.name = frontmatter.get("name", "")
    config.description = frontmatter.get("description", "")
    config.authors = frontmatter.get("authors", [])
    config.version = frontmatter.get("version", "1.0.0")

    # Model configuration
    model = frontmatter.get("model", {})
    config.api = model.get("api", "chat")

    model_config = model.get("configuration", {})
    config.model_type = model_config.get("type", "azure_openai")
    config.azure_endpoint = _resolve_env_vars(model_config.get("azure_endpoint", ""))
    config.azure_deployment = model_config.get("azure_deployment", "")
    config.api_version = model_config.get("api_version", "2025-03-01-preview")

    # Model parameters - store as dict, resolve file refs for response_format
    params = dict(model.get("parameters", {}))  # Copy to avoid modifying original

    # Resolve response_format file reference if present
    if "response_format" in params:
        resolved = _resolve_file_ref(params["response_format"], path.parent)
        if isinstance(resolved, dict):
            params["response_format"] = resolved

    config.model_parameters = params

    # Sample data
    config.sample = frontmatter.get("sample", {})

    # Parse body content for system/user templates
    # Check if body has explicit system: and user: sections
    if "system:" in body_content or "user:" in body_content:
        # Parse structured format
        # This is a simplified parser - may need enhancement for complex cases
        system_match = re.search(r"^system:\s*\n(.*?)(?=^user:|\Z)", body_content, re.MULTILINE | re.DOTALL)
        user_match = re.search(r"^user:\s*\n(.*?)(?=^system:|\Z)", body_content, re.MULTILINE | re.DOTALL)

        if system_match:
            config.system_template = system_match.group(1).strip()
        if user_match:
            config.user_template = user_match.group(1).strip()
    else:
        # Treat entire body as a single content template (typically user message)
        config.content_template = body_content

    # Log parsed configuration at debug level
    logger.debug("  Name: %s", config.name or "(not set)")
    logger.debug("  Model: %s", config.azure_deployment or "(not set - must be provided at call time)")
    if config.model_parameters:
        logger.debug("  Parameters: %s", config.model_parameters)
    if config.system_template:
        logger.debug("  System template: %d chars", len(config.system_template))
    if config.user_template:
        logger.debug("  User template: %d chars", len(config.user_template))
    elif config.content_template:
        logger.debug("  Content template: %d chars", len(config.content_template))

    return config


def render_template(template: str, inputs: Dict[str, Any]) -> str:
    """Render a Jinja2 template with the given inputs."""
    jinja_template = Template(template)
    return jinja_template.render(**inputs)


def build_messages_from_prompty(config: PromptyConfig, inputs: Dict[str, Any]) -> List[Any]:
    """
    Build chat messages from a parsed prompty config and inputs.

    Args:
        config: Parsed PromptyConfig from a .prompty file.
        inputs: Dictionary of inputs for template rendering.

    Returns:
        List of SystemMessage/UserMessage objects ready for the API.

    Raises:
        ValueError: If no message templates are found in the config.
    """
    from azure.ai.inference.models import SystemMessage, UserMessage

    messages = []

    if config.system_template:
        system_content = render_template(config.system_template, inputs)
        messages.append(SystemMessage(content=system_content))

    if config.user_template:
        user_content = render_template(config.user_template, inputs)
        messages.append(UserMessage(content=user_content))
    elif config.content_template:
        # Single content template - treat as user message
        user_content = render_template(config.content_template, inputs)
        messages.append(UserMessage(content=user_content))

    if not messages:
        raise ValueError(f"No message templates found in prompty config: {config.source_path}")

    return messages


def call_foundry_model(
    *,
    messages: List[Any] = None,
    model: str = None,
    prompt_file: Optional[str] = None,
    inputs: Optional[Dict[str, Any]] = None,
    endpoint: Optional[str] = None,
    response_format: Optional[Dict] = None,
    parse_json: bool = False,
    **kwargs,
) -> Any:
    """
    Call the Azure AI Foundry Models endpoint (chat completions).

    This is for direct model inference. For agent-based interactions with tools,
    use src/agent/_agent.py instead.

    Can be called two ways:
    1. With explicit messages: call_foundry_model(messages=..., model=...)
    2. With a prompty file: call_foundry_model(prompt_file="path/to/file.prompty", inputs={...})

    Args:
        messages: List of SystemMessage/UserMessage objects. Required if prompt_file not provided.
        model: Model deployment name. If prompt_file provided, defaults to config value.
        prompt_file: Path to a .prompty file. If provided, parses config and builds messages.
        inputs: Dictionary of inputs for template rendering. Used with prompt_file.
        endpoint: Azure AI Foundry endpoint. If not provided, uses foundry_endpoint from settings.
        response_format: JSON schema for structured output.
        parse_json: If True and response_format is set, parse response as JSON.
        **kwargs: Model parameters (max_tokens, temperature, top_p, frequency_penalty,
                  presence_penalty, etc.). These override prompty config values.

    Returns:
        The response content (parsed JSON if parse_json=True and response_format is set).
    """
    from azure.ai.inference import ChatCompletionsClient
    from azure.ai.inference.models import JsonSchemaFormat
    from azure.identity import DefaultAzureCredential
    from src._settings import SettingsManager

    # Build model params from prompty config (if provided) as defaults
    model_params = {}
    config = None
    if prompt_file:
        config = parse_prompty_file(prompt_file)
        messages = build_messages_from_prompty(config, inputs or {})
        model = model or config.azure_deployment

        # Use model_parameters from prompty as defaults (copy to avoid mutation)
        model_params = dict(config.model_parameters)

        # Extract response_format separately (handled specially below)
        response_format = response_format or model_params.pop("response_format", None)
        parse_json = parse_json or bool(response_format)

    # kwargs override prompty config values
    model_params.update(kwargs)

    # Validate required params
    if not messages:
        raise ValueError("Either 'messages' or 'prompt_file' must be provided.")
    if not model:
        raise ValueError("'model' must be provided (or defined in prompt_file).")

    settings = SettingsManager()

    # Determine endpoint
    foundry_endpoint = endpoint or settings.get("foundry_endpoint")
    if not foundry_endpoint:
        raise ValueError("foundry_endpoint not configured. Set it in AppConfiguration or pass endpoint parameter.")

    # Ensure endpoint ends with /models for chat completions
    if not foundry_endpoint.endswith("/models"):
        foundry_endpoint = foundry_endpoint.rstrip("/") + "/models"

    # Build completion parameters - pass all model params via model_extras
    # so they go directly to the API without SDK interference
    completion_params = {
        "messages": messages,
        "model": model,
    }
    if model_params:
        completion_params["model_extras"] = model_params

    # Handle response format (JSON schema)
    if response_format:
        completion_params["response_format"] = JsonSchemaFormat(
            name="response",
            schema=response_format,
            strict=True,
        )

    # Create client and make request
    credential = DefaultAzureCredential()
    client = ChatCompletionsClient(
        endpoint=foundry_endpoint,
        credential=credential,
        credential_scopes=["https://cognitiveservices.azure.com/.default"],
    )

    logger.debug("Calling Foundry model (model: %s)", model)

    try:
        response = client.complete(**completion_params)
        result_content = response.choices[0].message.content

        # Parse JSON if requested and response format was specified
        if parse_json and response_format and result_content:
            try:
                return json.loads(result_content)
            except json.JSONDecodeError:
                logger.warning("Failed to parse response as JSON, returning raw content")
                return result_content

        return result_content

    except Exception as e:
        logger.error("Error calling Azure AI Foundry: %s", e)
        raise


def run_prompty(
    *,
    folder: str,
    filename: str,
    inputs: Dict[str, Any] = None,
    endpoint: Optional[str] = None,
    model: Optional[str] = None,
    max_tokens: Optional[int] = None,
    **kwargs,
) -> Any:
    """
    Parse a prompty file and execute it via Azure AI Foundry.

    Convenience function that combines parse_prompty_file, build_messages_from_prompty,
    and call_foundry_chat.

    Args:
        folder: Folder containing the prompty file (relative to prompts/).
        filename: Name of the prompty file (without extension).
        inputs: Dictionary of inputs for template rendering.
        endpoint: Override the Azure AI Foundry endpoint.
        model: Override the model/deployment name.
        max_tokens: Override the max_tokens value from the prompty file.
        **kwargs: Additional parameters passed to the chat completion call.

    Returns:
        The response from the model (parsed JSON if response_format is set, otherwise string).
    """
    from src._utils import get_prompt_path

    inputs = inputs or {}

    # Parse the prompty file
    prompt_path = get_prompt_path(folder=folder, filename=filename)
    config = parse_prompty_file(prompt_path)

    # Build messages
    messages = build_messages_from_prompty(config, inputs)

    # Determine model - log override if provided
    model_name = model or config.azure_deployment
    if not model_name:
        raise ValueError("No model specified in prompty file or as parameter.")
    if model and config.azure_deployment and model != config.azure_deployment:
        logger.debug("Model override: %s -> %s", config.azure_deployment, model)

    # Build model params from config, with optional max_tokens override
    model_params = dict(config.model_parameters)
    response_format = model_params.pop("response_format", None)
    if max_tokens is not None:
        config_max = model_params.get("max_tokens")
        if config_max is not None and max_tokens != config_max:
            logger.debug("Max tokens override: %d -> %d", config_max, max_tokens)
        model_params["max_tokens"] = max_tokens

    # Call Foundry model
    return call_foundry_model(
        messages=messages,
        model=model_name,
        endpoint=endpoint,
        response_format=response_format,
        parse_json=bool(response_format),
        **model_params,
        **kwargs,
    )


async def call_foundry_model_async(
    *,
    messages: List[Any] = None,
    model: str = None,
    prompt_file: Optional[str] = None,
    inputs: Optional[Dict[str, Any]] = None,
    endpoint: Optional[str] = None,
    response_format: Optional[Dict] = None,
    parse_json: bool = False,
    **kwargs,
) -> Any:
    """
    Async version of call_foundry_model.

    This is for direct model inference. For agent-based interactions with tools,
    use src/agent/_agent.py instead.

    Can be called two ways:
    1. With explicit messages: call_foundry_model_async(messages=..., model=...)
    2. With a prompty file: call_foundry_model_async(prompt_file="path/to/file.prompty", inputs={...})

    Args:
        messages: List of SystemMessage/UserMessage objects. Required if prompt_file not provided.
        model: Model deployment name. If prompt_file provided, defaults to config value.
        prompt_file: Path to a .prompty file. If provided, parses config and builds messages.
        inputs: Dictionary of inputs for template rendering. Used with prompt_file.
        endpoint: Azure AI Foundry endpoint. If not provided, uses foundry_endpoint from settings.
        response_format: JSON schema for structured output.
        parse_json: If True and response_format is set, parse response as JSON.
        **kwargs: Model parameters (max_tokens, temperature, top_p, frequency_penalty,
                  presence_penalty, etc.). These override prompty config values.

    Returns:
        The response content (parsed JSON if parse_json=True and response_format is set).
    """
    from azure.ai.inference.aio import ChatCompletionsClient
    from azure.ai.inference.models import JsonSchemaFormat
    from azure.identity.aio import DefaultAzureCredential
    from src._settings import SettingsManager

    # Build model params from prompty config (if provided) as defaults
    model_params = {}
    config = None
    if prompt_file:
        config = parse_prompty_file(prompt_file)
        messages = build_messages_from_prompty(config, inputs or {})
        model = model or config.azure_deployment

        # Use model_parameters from prompty as defaults (copy to avoid mutation)
        model_params = dict(config.model_parameters)

        # Extract response_format separately (handled specially below)
        response_format = response_format or model_params.pop("response_format", None)
        parse_json = parse_json or bool(response_format)

    # kwargs override prompty config values
    model_params.update(kwargs)

    # Validate required params
    if not messages:
        raise ValueError("Either 'messages' or 'prompt_file' must be provided.")
    if not model:
        raise ValueError("'model' must be provided (or defined in prompt_file).")

    settings = SettingsManager()

    # Determine endpoint
    foundry_endpoint = endpoint or settings.get("foundry_endpoint")
    if not foundry_endpoint:
        raise ValueError("foundry_endpoint not configured. Set it in AppConfiguration or pass endpoint parameter.")

    # Ensure endpoint ends with /models for chat completions
    if not foundry_endpoint.endswith("/models"):
        foundry_endpoint = foundry_endpoint.rstrip("/") + "/models"

    # Build completion parameters - pass all model params via model_extras
    # so they go directly to the API without SDK interference
    completion_params = {
        "messages": messages,
        "model": model,
    }
    if model_params:
        completion_params["model_extras"] = model_params

    # Handle response format (JSON schema)
    if response_format:
        completion_params["response_format"] = JsonSchemaFormat(
            name="response",
            schema=response_format,
            strict=True,
        )

    # Create async client and make request
    credential = DefaultAzureCredential()
    async with ChatCompletionsClient(
        endpoint=foundry_endpoint,
        credential=credential,
        credential_scopes=["https://cognitiveservices.azure.com/.default"],
    ) as client:
        logger.debug("Calling Foundry model async (model: %s)", model)

        try:
            response = await client.complete(**completion_params)
            result_content = response.choices[0].message.content

            if parse_json and response_format and result_content:
                try:
                    return json.loads(result_content)
                except json.JSONDecodeError:
                    logger.warning("Failed to parse response as JSON, returning raw content")
                    return result_content

            return result_content

        except Exception as e:
            logger.error("Error calling Azure AI Foundry: %s", e)
            raise


async def run_prompty_async(
    *,
    folder: str,
    filename: str,
    inputs: Dict[str, Any] = None,
    endpoint: Optional[str] = None,
    model: Optional[str] = None,
    max_tokens: Optional[int] = None,
    **kwargs,
) -> Any:
    """
    Async version of run_prompty.

    Args:
        folder: Folder containing the prompty file (relative to prompts/).
        filename: Name of the prompty file (without extension).
        inputs: Dictionary of inputs for template rendering.
        endpoint: Override the Azure AI Foundry endpoint.
        model: Override the model/deployment name.
        max_tokens: Override the max_tokens value from the prompty file.
        **kwargs: Additional parameters passed to the chat completion call.

    Returns:
        The response from the model.
    """
    from src._utils import get_prompt_path

    inputs = inputs or {}

    # Parse the prompty file
    prompt_path = get_prompt_path(folder=folder, filename=filename)
    config = parse_prompty_file(prompt_path)

    # Build messages
    messages = build_messages_from_prompty(config, inputs)

    # Determine model - log override if provided
    model_name = model or config.azure_deployment
    if not model_name:
        raise ValueError("No model specified in prompty file or as parameter.")
    if model and config.azure_deployment and model != config.azure_deployment:
        logger.debug("Model override: %s -> %s", config.azure_deployment, model)

    # Build model params from config, with optional max_tokens override
    model_params = dict(config.model_parameters)
    response_format = model_params.pop("response_format", None)
    if max_tokens is not None:
        config_max = model_params.get("max_tokens")
        if config_max is not None and max_tokens != config_max:
            logger.debug("Max tokens override: %d -> %d", config_max, max_tokens)
        model_params["max_tokens"] = max_tokens

    # Call Foundry model
    return await call_foundry_model_async(
        messages=messages,
        model=model_name,
        endpoint=endpoint,
        response_format=response_format,
        parse_json=bool(response_format),
        **model_params,
        **kwargs,
    )
