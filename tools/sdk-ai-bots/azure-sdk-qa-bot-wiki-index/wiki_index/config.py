"""Configuration from Azure App Configuration, overridable by environment.

Mirrors the agent's config approach so the build pipeline only needs
``AZURE_APPCONFIG_ENDPOINT``. An explicit environment variable always wins,
which keeps local runs and one-off overrides simple.
"""

from __future__ import annotations

import logging
import os

logger = logging.getLogger(__name__)

_settings: dict[str, str] = {}


async def load(credential) -> None:
    """Fetch all key-values from App Configuration, if an endpoint is set."""
    endpoint = os.environ.get("AZURE_APPCONFIG_ENDPOINT")
    if not endpoint:
        logger.info("AZURE_APPCONFIG_ENDPOINT not set; using environment only")
        return

    from azure.appconfiguration.aio import AzureAppConfigurationClient

    client = AzureAppConfigurationClient(base_url=endpoint, credential=credential)
    async with client:
        async for item in client.list_configuration_settings():
            if item.value is not None:
                _settings[item.key] = item.value
    logger.info("loaded %d settings from App Configuration", len(_settings))


def load_sync(credential) -> None:
    """Blocking variant of :func:`load` for the synchronous setup scripts."""
    endpoint = os.environ.get("AZURE_APPCONFIG_ENDPOINT")
    if not endpoint:
        logger.info("AZURE_APPCONFIG_ENDPOINT not set; using environment only")
        return

    from azure.appconfiguration import AzureAppConfigurationClient

    client = AzureAppConfigurationClient(base_url=endpoint, credential=credential)
    with client:
        for item in client.list_configuration_settings():
            if item.value is not None:
                _settings[item.key] = item.value
    logger.info("loaded %d settings from App Configuration", len(_settings))


def get(name: str, default: str = "") -> str:
    """Read a setting: environment first, then App Configuration, then *default*."""
    return os.environ.get(name) or _settings.get(name, default)


def require(name: str) -> str:
    """Read a setting that has no safe default, raising when it is missing."""
    value = get(name)
    if not value:
        raise RuntimeError(
            f"{name} is required; set it in the environment or App Configuration "
            "(AZURE_APPCONFIG_ENDPOINT)."
        )
    return value
