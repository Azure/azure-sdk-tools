"""Azure App Configuration loader.

Loads settings from Azure App Configuration and sets them as environment
variables (only if not already set, giving priority to .env/local values).
"""

from __future__ import annotations

import logging
import os
from pathlib import Path

from dotenv import load_dotenv

logger = logging.getLogger(__name__)


def _load_env_file() -> None:
    """Load .env file from project root."""
    project_root = Path(__file__).resolve().parent.parent.parent
    env_path = project_root / ".env"
    if env_path.exists():
        load_dotenv(env_path)
        logger.info("Loaded environment variables from %s", env_path)
    else:
        logger.debug("No .env file at %s", env_path)


async def init_configuration() -> None:
    """Initialize configuration from .env then Azure App Configuration.

    Settings from App Configuration only override env vars that are not
    already set (local .env takes priority).
    """
    _load_env_file()

    endpoint = os.environ.get("AZURE_APPCONFIG_ENDPOINT")
    if not endpoint:
        logger.warning("AZURE_APPCONFIG_ENDPOINT not set; skipping App Configuration")
        return

    from azure.appconfiguration.aio import AzureAppConfigurationClient
    from azure.identity.aio import (
        AzureCliCredential,
        ChainedTokenCredential,
        ManagedIdentityCredential,
        WorkloadIdentityCredential,
    )

    logger.info("Loading configuration from Azure App Configuration...")

    credential = ChainedTokenCredential(
        ManagedIdentityCredential(),
        AzureCliCredential(),
        WorkloadIdentityCredential(),
    )

    client = AzureAppConfigurationClient(endpoint, credential=credential)

    try:
        async for setting in client.list_configuration_settings():
            if setting.key and setting.value is not None:
                if not os.environ.get(setting.key):
                    os.environ[setting.key] = setting.value
                    logger.info("Set %s from App Configuration", setting.key)
                else:
                    logger.debug("Skipping %s — already set", setting.key)
    finally:
        await client.close()
        await credential.close()

    logger.info("Successfully loaded configuration from Azure App Configuration")
