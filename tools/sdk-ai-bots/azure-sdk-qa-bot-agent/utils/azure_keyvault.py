"""Azure Key Vault helper for fetching secrets."""

from __future__ import annotations

import logging

from azure.keyvault.secrets.aio import SecretClient

from config.app_config import get as cfg
from utils.azure_credential import get_credential

logger = logging.getLogger(__name__)

_client: SecretClient | None = None


def _get_client() -> SecretClient:
    global _client
    if _client is None:
        vault_url = cfg("KEYVAULT_ENDPOINT", "")
        if not vault_url:
            raise RuntimeError(
                "Key Vault endpoint is not configured. "
                "Set KEYVAULT_ENDPOINT in Azure App Configuration."
            )
        _client = SecretClient(
            vault_url=vault_url,
            credential=get_credential(),
        )
    return _client


async def get_secret(name: str) -> str | None:
    """Fetch a secret value by name. Returns None on failure."""
    try:
        secret = await _get_client().get_secret(name)
        return secret.value
    except Exception as exc:
        logger.warning("Failed to fetch secret '%s' from Key Vault: %s", name, exc)
        return None
