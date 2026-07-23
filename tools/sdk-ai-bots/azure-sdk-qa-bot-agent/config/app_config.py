"""Centralized configuration loaded from Azure App Configuration.

On startup, connects to the App Configuration store specified by the
``AZURE_APPCONFIG_ENDPOINT`` environment variable and fetches all key-values.
Reuses the shared async credential from ``utils.azure_credential``.
Every other module reads config through the ``settings`` dict exposed here
instead of calling ``os.getenv`` directly.

Loading order:

1. ``.env`` (if present) is read into ``os.environ`` so developers can
   override anything locally.
2. App Configuration is fetched and each key is *also* mirrored into
   ``os.environ`` via ``setdefault`` semantics — local env vars and
   ``.env`` values win.

The env-var mirror exists so third-party libraries that resolve their
own placeholders against ``os.environ`` see the same values as ``cfg()``.

Call ``await init()`` once during application startup (inside the async
event loop) before any calls to ``get()``.
"""

from __future__ import annotations

import logging
import os
from pathlib import Path
from typing import overload

from azure.appconfiguration.aio import AzureAppConfigurationClient

from utils.azure_credential import get_credential

_logger = logging.getLogger(__name__)

_settings: dict[str, str] | None = None


def _load_env_file() -> None:
    """Load ``.env`` from the project root, if present.
    """
    from dotenv import load_dotenv

    project_root = Path(__file__).resolve().parent.parent
    env_path = project_root / ".env"
    if env_path.exists():
        load_dotenv(env_path)
        _logger.info("Loaded environment variables from %s", env_path)
    else:
        _logger.debug("No .env file at %s", env_path)


async def init() -> None:
    """Load all settings from Azure App Configuration.

    Must be awaited once at startup before calling ``get()``.
    """
    global _settings
    if _settings is not None:
        return

    _load_env_file()

    endpoint = os.environ.get("AZURE_APPCONFIG_ENDPOINT")
    if not endpoint:
        raise RuntimeError("AZURE_APPCONFIG_ENDPOINT environment variable is required.")

    _logger.info("Loading settings from App Configuration: %s", endpoint)
    credential = get_credential()
    client = AzureAppConfigurationClient(base_url=endpoint, credential=credential)

    config: dict[str, str] = {}
    mirrored = 0
    async for item in client.list_configuration_settings():
        if item.key and item.value is not None:
            config[item.key] = item.value
            # ``setdefault`` so real env vars and ``.env`` values win
            # over App Configuration — matches sync project semantics.
            if item.key not in os.environ:
                os.environ[item.key] = item.value
                mirrored += 1
    await client.close()

    _settings = config
    _logger.info(
        "Loaded %d settings from App Configuration (mirrored %d into os.environ)",
        len(_settings),
        mirrored,
    )


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
