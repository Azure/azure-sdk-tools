"""Centralized configuration loaded from Azure App Configuration.

On startup, connects to the App Configuration store specified by the
``AZURE_APPCONFIG_ENDPOINT`` environment variable and fetches all key-values.
Reuses the shared async credential from ``utils.azure_credential``.
Every other module reads config through the ``settings`` dict exposed here
instead of calling ``os.getenv`` directly.

Call ``await init()`` once during application startup (inside the async
event loop) before any calls to ``get()``.
"""

from __future__ import annotations

import logging
import os

from azure.appconfiguration.aio import AzureAppConfigurationClient

from utils.azure_credential import get_credential

_logger = logging.getLogger(__name__)

_settings: dict[str, str] | None = None


async def init() -> None:
    """Load all settings from Azure App Configuration.

    Must be awaited once at startup before calling ``get()``.
    """
    global _settings
    if _settings is not None:
        return

    endpoint = os.environ.get("AZURE_APPCONFIG_ENDPOINT")
    if not endpoint:
        raise RuntimeError(
            "AZURE_APPCONFIG_ENDPOINT environment variable is required."
        )

    _logger.info("Loading settings from App Configuration: %s", endpoint)
    credential = get_credential()
    client = AzureAppConfigurationClient(base_url=endpoint, credential=credential)

    config: dict[str, str] = {}
    async for item in client.list_configuration_settings():
        if item.value is not None:
            config[item.key] = item.value
    await client.close()

    _settings = config
    _logger.info("Loaded %d settings from App Configuration", len(_settings))


def get(key: str, default: str | None = None) -> str | None:
    """Return a config value, falling back to *default*.

    Raises if ``init()`` has not been called yet.
    """
    if _settings is None:
        raise RuntimeError(
            "App Configuration not loaded. Call 'await app_config.init()' first."
        )
    return _settings.get(key, default)
