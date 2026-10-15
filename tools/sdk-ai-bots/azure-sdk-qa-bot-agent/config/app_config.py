"""Centralized configuration loaded from Azure App Configuration.

On startup, connects to the App Configuration store specified by the
``AZURE_APPCONFIG_ENDPOINT`` environment variable and fetches all key-values.
Reuses the shared async credential from ``utils.azure_credential``.
Every other module reads config through the ``settings`` dict exposed here
instead of calling ``os.getenv`` directly.

Call ``await init()`` during application startup before any calls to ``get()``.
The server calls ``refresh()`` before requests to refresh the shared snapshot
on demand once its one-minute interval expires.
"""

from __future__ import annotations

import logging
import os
from asyncio import Lock
from dataclasses import dataclass
from time import monotonic
from typing import overload, Protocol
from urllib.parse import quote

from azure.appconfiguration.aio import AzureAppConfigurationClient

from utils.azure_credential import get_credential

_logger = logging.getLogger(__name__)

_settings: dict[str, str] | None = None
CONFIG_REFRESH_INTERVAL_SECONDS = 60
_settings_refresh_at = 0.0
_settings_refresh_lock = Lock()


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


def get_agent_deployment_key(branch: str, agent_name: str) -> str:
    """Build a deployment key from a branch name, preserving its path and case."""
    return f"agent-deployments:{quote(branch, safe='')}:{quote(agent_name, safe='')}"


async def init() -> None:
    """Load the shared settings snapshot at startup or when its interval expires."""
    global _settings, _settings_refresh_at
    if _settings is not None and monotonic() < _settings_refresh_at:
        return

    endpoint = os.environ.get("AZURE_APPCONFIG_ENDPOINT")
    if not endpoint:
        raise RuntimeError("AZURE_APPCONFIG_ENDPOINT environment variable is required.")

    async with _settings_refresh_lock:
        if _settings is not None and monotonic() < _settings_refresh_at:
            return
        snapshot = await load(endpoint)
        _settings = snapshot.settings
        _settings_refresh_at = monotonic() + CONFIG_REFRESH_INTERVAL_SECONDS
        _logger.info("Loaded %d settings from App Configuration", len(_settings))


async def refresh() -> None:
    """Refresh initialized settings when due; initialization belongs to startup."""
    if _settings is not None:
        await init()


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
