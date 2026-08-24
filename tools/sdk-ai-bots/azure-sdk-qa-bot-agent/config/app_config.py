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
from dataclasses import dataclass
from typing import overload, Protocol

from azure.appconfiguration.aio import AzureAppConfigurationClient

from utils.azure_credential import get_credential

_logger = logging.getLogger(__name__)

_settings: dict[str, str] | None = None


class Settings(Protocol):
    """Callable configuration lookup preserving default-value narrowing."""

    @overload
    def __call__(self, key: str, default: str) -> str: ...

    @overload
    def __call__(self, key: str, default: None = None) -> str | None: ...


@dataclass(frozen=True)
class AppConfigSnapshot:
    """Immutable settings loaded from one App Configuration endpoint."""

    endpoint: str
    settings: dict[str, str]

    @overload
    def get(self, key: str, default: str) -> str: ...

    @overload
    def get(self, key: str, default: None = None) -> str | None: ...

    def get(self, key: str, default: str | None = None) -> str | None:
        return self.settings.get(key, default)


async def load(endpoint: str) -> AppConfigSnapshot:
    """Load a named configuration snapshot without changing global settings."""
    if not endpoint:
        raise ValueError("App Configuration endpoint is required.")

    _logger.info("Loading settings from App Configuration: %s", endpoint)
    credential = get_credential()
    client = AzureAppConfigurationClient(base_url=endpoint, credential=credential)
    try:
        settings: dict[str, str] = {}
        async for item in client.list_configuration_settings():
            if item.value is not None:
                settings[item.key] = item.value
    finally:
        await client.close()

    _logger.info("Loaded %d settings from App Configuration", len(settings))
    return AppConfigSnapshot(endpoint=endpoint, settings=settings)


async def init() -> None:
    """Load all settings from Azure App Configuration.

    Must be awaited once at startup before calling ``get()``.
    """
    global _settings
    if _settings is not None:
        return

    endpoint = os.environ.get("AZURE_APPCONFIG_ENDPOINT")
    if not endpoint:
        raise RuntimeError("AZURE_APPCONFIG_ENDPOINT environment variable is required.")

    snapshot = await load(endpoint)
    _settings = snapshot.settings
    _logger.info("Loaded %d settings from App Configuration", len(_settings))


@overload
def get(key: str, default: str) -> str: ...


@overload
def get(key: str, default: None = None) -> str | None: ...


def get(key: str, default: str | None = None) -> str | None:
    """Return a config value, falling back to *default*.

    Raises if ``init()`` has not been called yet and no *default* is given.
    """
    if _settings is None:
        if default is not None:
            return default
        raise RuntimeError(
            "App Configuration not loaded. Call 'await app_config.init()' first."
        )
    return _settings.get(key, default)
