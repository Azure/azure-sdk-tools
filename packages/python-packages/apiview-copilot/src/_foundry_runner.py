# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""
Module for running prompty files using Azure AI Foundry Models instead of Azure OpenAI.

This module provides a parallel path to the prompty library, parsing .prompty files
and executing them using the Azure AI Inference SDK (azure-ai-inference).
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

    # Model parameters
    frequency_penalty: float = 0
    presence_penalty: float = 0
    max_completion_tokens: int = 80000
    reasoning_effort: Optional[str] = None
    temperature: Optional[float] = None
    top_p: Optional[float] = None
    response_format: Optional[Dict] = None

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

    # Model parameters
    params = model.get("parameters", {})
    config.frequency_penalty = params.get("frequency_penalty", 0)
    config.presence_penalty = params.get("presence_penalty", 0)
    config.max_completion_tokens = params.get("max_completion_tokens", 80000)
    config.reasoning_effort = params.get("reasoning_effort")
    config.temperature = params.get("temperature")
    config.top_p = params.get("top_p")

    # Response format (may be a file reference)
    response_format = params.get("response_format")
    if response_format:
        resolved = _resolve_file_ref(response_format, path.parent)
        if isinstance(resolved, dict):
            config.response_format = resolved

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

    return config


def render_template(template: str, inputs: Dict[str, Any]) -> str:
    """Render a Jinja2 template with the given inputs."""
    jinja_template = Template(template)
    return jinja_template.render(**inputs)


def run_prompty_foundry(
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
    Run a prompty file using Azure AI Foundry Models.

    This is a parallel implementation to run_prompty that uses the Azure AI Inference SDK
    instead of the prompty library with Azure OpenAI.

    Args:
        folder: Folder containing the prompty file (relative to prompts/).
        filename: Name of the prompty file (without extension).
        inputs: Dictionary of inputs for template rendering.
        endpoint: Override the Azure AI Foundry endpoint (defaults to FOUNDRY_ENDPOINT setting).
        model: Override the model/deployment name.
        max_tokens: Override the max_tokens value from the prompty file.
        **kwargs: Additional parameters passed to the chat completion call.

    Returns:
        The response from the model (parsed JSON if response_format is set, otherwise string).
    """
    from azure.ai.inference import ChatCompletionsClient
    from azure.ai.inference.models import (
        JsonSchemaFormat,
        SystemMessage,
        UserMessage,
    )
    from azure.identity import DefaultAzureCredential
    from src._settings import SettingsManager
    from src._utils import get_prompt_path

    inputs = inputs or {}
    settings = SettingsManager()

    # Get the prompty file path
    prompt_path = get_prompt_path(folder=folder, filename=filename)
    config = parse_prompty_file(prompt_path)

    # Determine endpoint and model
    foundry_endpoint = endpoint or settings.get("foundry_endpoint")
    if not foundry_endpoint:
        raise ValueError("foundry_endpoint not configured in AppConfiguration. Set it or pass endpoint parameter.")

    # Ensure endpoint ends with /models for chat completions
    if not foundry_endpoint.endswith("/models"):
        foundry_endpoint = foundry_endpoint.rstrip("/") + "/models"

    model_name = model or config.azure_deployment
    if not model_name:
        raise ValueError("No model specified in prompty file or as parameter.")

    # Build messages
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
        raise ValueError(f"No message templates found in prompty file: {prompt_path}")

    # Build completion parameters
    effective_max_tokens = max_tokens if max_tokens is not None else config.max_completion_tokens
    completion_params = {
        "messages": messages,
        "model": model_name,
        "max_tokens": effective_max_tokens,
    }

    # Add optional parameters
    if config.frequency_penalty:
        completion_params["frequency_penalty"] = config.frequency_penalty
    if config.presence_penalty:
        completion_params["presence_penalty"] = config.presence_penalty
    if config.temperature is not None:
        completion_params["temperature"] = config.temperature
    if config.top_p is not None:
        completion_params["top_p"] = config.top_p

    # Handle response format (JSON schema)
    if config.response_format:
        completion_params["response_format"] = JsonSchemaFormat(
            name="response",
            schema=config.response_format,
            strict=True,
        )

    # Add any extra kwargs
    completion_params.update(kwargs)

    # Create client and make request with proper Azure AI Foundry authentication
    from azure.ai.inference import ChatCompletionsClient
    from azure.identity import DefaultAzureCredential

    credential = DefaultAzureCredential()
    client = ChatCompletionsClient(
        endpoint=foundry_endpoint,
        credential=credential,
        credential_scopes=["https://cognitiveservices.azure.com/.default"],
    )

    logger.info(f"Running prompty via Foundry: {config.name} (model: {model_name})")

    try:
        response = client.complete(**completion_params)
        result_content = response.choices[0].message.content

        # Parse JSON if response format was specified
        if config.response_format and result_content:
            try:
                return json.loads(result_content)
            except json.JSONDecodeError:
                logger.warning("Failed to parse response as JSON, returning raw content")
                return result_content

        return result_content

    except Exception as e:
        logger.error(f"Error calling Azure AI Foundry: {e}")
        raise


async def run_prompty_foundry_async(
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
    Async version of run_prompty_foundry.

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
    from azure.ai.inference.aio import ChatCompletionsClient
    from azure.ai.inference.models import (
        JsonSchemaFormat,
        SystemMessage,
        UserMessage,
    )
    from azure.identity.aio import DefaultAzureCredential
    from src._settings import SettingsManager
    from src._utils import get_prompt_path

    inputs = inputs or {}
    settings = SettingsManager()

    # Get the prompty file path
    prompt_path = get_prompt_path(folder=folder, filename=filename)
    config = parse_prompty_file(prompt_path)

    # Determine endpoint and model
    foundry_endpoint = endpoint or settings.get("foundry_endpoint")
    if not foundry_endpoint:
        raise ValueError("foundry_endpoint not configured in AppConfiguration.")

    # Ensure endpoint ends with /models for chat completions
    if not foundry_endpoint.endswith("/models"):
        foundry_endpoint = foundry_endpoint.rstrip("/") + "/models"

    model_name = model or config.azure_deployment

    # Build messages
    messages = []

    if config.system_template:
        system_content = render_template(config.system_template, inputs)
        messages.append(SystemMessage(content=system_content))

    if config.user_template:
        user_content = render_template(config.user_template, inputs)
        messages.append(UserMessage(content=user_content))
    elif config.content_template:
        user_content = render_template(config.content_template, inputs)
        messages.append(UserMessage(content=user_content))

    if not messages:
        raise ValueError(f"No message templates found in prompty file: {prompt_path}")

    # Build completion parameters
    effective_max_tokens = max_tokens if max_tokens is not None else config.max_completion_tokens
    completion_params = {
        "messages": messages,
        "model": model_name,
        "max_tokens": effective_max_tokens,
    }

    if config.frequency_penalty:
        completion_params["frequency_penalty"] = config.frequency_penalty
    if config.presence_penalty:
        completion_params["presence_penalty"] = config.presence_penalty
    if config.temperature is not None:
        completion_params["temperature"] = config.temperature
    if config.top_p is not None:
        completion_params["top_p"] = config.top_p

    if config.response_format:
        completion_params["response_format"] = JsonSchemaFormat(
            name="response",
            schema=config.response_format,
            strict=True,
        )

    completion_params.update(kwargs)

    # Create async client and make request
    credential = DefaultAzureCredential()
    async with ChatCompletionsClient(
        endpoint=foundry_endpoint,
        credential=credential,
        credential_scopes=["https://cognitiveservices.azure.com/.default"],
    ) as client:
        logger.info(f"Running prompty via Foundry (async): {config.name} (model: {model_name})")

        try:
            response = await client.complete(**completion_params)
            result_content = response.choices[0].message.content

            if config.response_format and result_content:
                try:
                    return json.loads(result_content)
                except json.JSONDecodeError:
                    return result_content

            return result_content

        except Exception as e:
            logger.error(f"Error calling Azure AI Foundry: {e}")
            raise
