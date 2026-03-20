"""Azure credential singleton.

Builds a chained credential that mirrors the Go backend approach:
1. Managed Identity (if AZURE_CLIENT_ID is set)
2. Azure CLI (local dev fallback)
"""

import logging
import os

from azure.core.credentials_async import AsyncTokenCredential
from azure.identity.aio import AzureCliCredential, ChainedTokenCredential, ManagedIdentityCredential

_logger = logging.getLogger(__name__)
_credential: AsyncTokenCredential | None = None


def get_credential() -> AsyncTokenCredential:
    """Return the shared async chained credential (created once on first call)."""
    global _credential
    if _credential is None:
        credentials: list[AsyncTokenCredential] = []

        client_id = os.environ.get("AZURE_CLIENT_ID")
        if client_id:
            credentials.append(ManagedIdentityCredential(client_id=client_id))
            _logger.info("Managed Identity credential added (client_id=%s)", client_id)
        else:
            _logger.info("AZURE_CLIENT_ID not set; skipping Managed Identity credential")

        credentials.append(AzureCliCredential())
        _logger.info("Azure CLI credential added")

        _credential = ChainedTokenCredential(*credentials)
    return _credential


async def close_credential() -> None:
    """Close the credential.  Safe to call even if never created."""
    global _credential
    if _credential is not None:
        await _credential.close()
        _credential = None

