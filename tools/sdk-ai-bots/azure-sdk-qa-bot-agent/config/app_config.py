"""Centralized configuration loaded from Azure App Configuration.

On startup, connects to the App Configuration store specified by the
``AZURE_APPCONFIG_ENDPOINT`` environment variable using ``DefaultAzureCredential``
and fetches all key-values. Every other module reads config through the
``settings`` dict exposed here instead of calling ``os.getenv`` directly.
"""

from __future__ import annotations

import os

from azure.appconfiguration import AzureAppConfigurationClient
from azure.identity import DefaultAzureCredential


import logging

_logger = logging.getLogger(__name__)


def _load_settings() -> dict[str, str]:
    endpoint = os.environ.get("AZURE_APPCONFIG_ENDPOINT")
    if not endpoint:
        raise RuntimeError(
            "AZURE_APPCONFIG_ENDPOINT environment variable is required."
        )

    _logger.info("Loading settings from App Configuration: %s", endpoint)
    credential = DefaultAzureCredential()
    client = AzureAppConfigurationClient(base_url=endpoint, credential=credential)

    config: dict[str, str] = {}
    for item in client.list_configuration_settings():
        if item.value is not None:
            config[item.key] = item.value

    _logger.info("Loaded %d settings from App Configuration", len(config))
    return config


_settings: dict[str, str] | None = None


def _ensure_loaded() -> dict[str, str]:
    global _settings
    if _settings is None:
        _settings = _load_settings()
    return _settings


def get(key: str, default: str | None = None) -> str | None:
    """Return a config value, falling back to *default*."""
    return _ensure_loaded().get(key, default)
