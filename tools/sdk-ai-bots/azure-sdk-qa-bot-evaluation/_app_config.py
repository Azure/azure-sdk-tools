"""Minimal Azure App Configuration reader for the evaluation project.

Loads key-values from the App Configuration store named by the
``AZURE_APPCONFIG_ENDPOINT`` environment variable and caches them, so shared
infrastructure settings (e.g. ``STORAGE_BASE_URL``) come from the same source of
truth the bot backend and agent already read from App Configuration.
"""

from __future__ import annotations

import logging
import os
from typing import Any

logger = logging.getLogger(__name__)

_settings: dict[str, str] | None = None


def load(credential: Any) -> dict[str, str]:
    """Load and cache every setting from Azure App Configuration."""
    global _settings
    if _settings is not None:
        return _settings

    endpoint = os.environ.get("AZURE_APPCONFIG_ENDPOINT")
    if not endpoint:
        raise RuntimeError("AZURE_APPCONFIG_ENDPOINT environment variable is required.")

    from azure.appconfiguration import AzureAppConfigurationClient

    client = AzureAppConfigurationClient(base_url=endpoint, credential=credential)
    config: dict[str, str] = {}
    try:
        for item in client.list_configuration_settings():
            if item.value is not None:
                config[item.key] = item.value
    finally:
        client.close()

    _settings = config
    logger.info("Loaded %d settings from App Configuration", len(_settings))
    return _settings


def get(key: str, credential: Any, default: str | None = None) -> str | None:
    """Return a single value from App Configuration, falling back to *default*."""
    return load(credential).get(key, default)


def storage_base_url(credential: Any) -> str:
    """Return the blob storage base URL from App Configuration (``STORAGE_BASE_URL``).

    A ``STORAGE_BASE_URL`` environment variable, when set, takes precedence for
    local development so the app can run without hitting App Configuration.
    """
    override = os.environ.get("STORAGE_BASE_URL")
    if override:
        return override
    url = get("STORAGE_BASE_URL", credential)
    if not url:
        raise RuntimeError("STORAGE_BASE_URL not found in App Configuration.")
    return url
