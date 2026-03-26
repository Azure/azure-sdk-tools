"""Azure credential singleton.

Builds a chained credential that mirrors the Go backend approach:
1. Managed Identity (if AZURE_CLIENT_ID is set)
2. Azure CLI (local dev fallback)
"""

import logging
import os

from azure.core.credentials_async import AsyncTokenCredential
from azure.identity.aio import (
    AzureCliCredential,
    ChainedTokenCredential,
    ManagedIdentityCredential,
)

_logger = logging.getLogger(__name__)
_credential: AsyncTokenCredential | None = None
_frontend_credential: AsyncTokenCredential | None = None


def get_credential() -> AsyncTokenCredential:
    """Return the shared async chained credential (created once on first call)."""
    global _credential
    if _credential is None:
        client_id = os.environ.get("UMI_BACKEND_CLIENT_ID") or os.environ.get(
            "AZURE_CLIENT_ID"
        )
        if client_id:
            # Production: try Managed Identity first, then CLI as fallback.
            _credential = ChainedTokenCredential(
                ManagedIdentityCredential(client_id=client_id),
                AzureCliCredential(),
            )
            _logger.info("Managed Identity credential added (client_id=%s)", client_id)
        else:
            # Local dev: skip ManagedIdentityCredential to avoid slow IMDS timeouts.
            _credential = AzureCliCredential()
            _logger.info("UMI_BACKEND_CLIENT_ID not set, using AzureCliCredential only")
    return _credential


def get_frontend_credential() -> AsyncTokenCredential:
    """Return an async credential that uses the UMI_FRONTEND_CLIENT_ID identity.

    Falls back to the default credential if UMI_FRONTEND_CLIENT_ID is not set.
    """
    global _frontend_credential
    if _frontend_credential is None:
        frontend_client_id = os.environ.get("UMI_FRONTEND_CLIENT_ID")
        if frontend_client_id:
            _frontend_credential = ChainedTokenCredential(
                ManagedIdentityCredential(client_id=frontend_client_id),
                AzureCliCredential(),
            )
            _logger.info(
                "Frontend credential created (client_id=%s)", frontend_client_id
            )
        else:
            # Local dev: skip ManagedIdentityCredential to avoid slow IMDS timeouts.
            _frontend_credential = AzureCliCredential()
            _logger.warning(
                "UMI_FRONTEND_CLIENT_ID not set, using AzureCliCredential only"
            )
    return _frontend_credential


async def close_credential() -> None:
    """Close the credential.  Safe to call even if never created."""
    global _credential, _frontend_credential
    if _frontend_credential is not None and _frontend_credential is not _credential:
        await _frontend_credential.close()
        _frontend_credential = None
    if _credential is not None:
        await _credential.close()
        _credential = None
