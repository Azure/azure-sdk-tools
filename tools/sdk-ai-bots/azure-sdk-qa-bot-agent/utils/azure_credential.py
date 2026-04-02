"""Azure credential singleton.

Builds a chained credential that supports:
1. Managed Identity
2. Azure Pipelines
3. Azure CLI
"""

import logging
import os

from azure.core.credentials_async import AsyncTokenCredential
from azure.identity.aio import (
    AzureCliCredential,
    AzurePipelinesCredential,
    ChainedTokenCredential,
    ManagedIdentityCredential,
)

_logger = logging.getLogger(__name__)
_credential: AsyncTokenCredential | None = None
_frontend_credential: AsyncTokenCredential | None = None


def _build_credential_chain(client_id: str | None) -> AsyncTokenCredential:
    """Build a chained credential with optional ManagedIdentity, Pipelines, and CLI."""
    credentials: list[AsyncTokenCredential] = []

    if client_id:
        credentials.append(ManagedIdentityCredential(client_id=client_id))

    # Azure Pipelines federated identity (requires SYSTEM_ACCESSTOKEN and service connection id)
    system_access_token = os.environ.get("SYSTEM_ACCESSTOKEN")
    service_connection_id = os.environ.get("AZURESUBSCRIPTION_SERVICE_CONNECTION_ID")
    tenant_id = os.environ.get("AZURE_TENANT_ID")
    az_client_id = client_id or os.environ.get("AZURE_CLIENT_ID")
    if system_access_token and service_connection_id and tenant_id and az_client_id:
        credentials.append(
            AzurePipelinesCredential(
                tenant_id=tenant_id,
                client_id=az_client_id,
                service_connection_id=service_connection_id,
                system_access_token=system_access_token,
            )
        )
        _logger.info(
            "AzurePipelinesCredential added (service_connection=%s)",
            service_connection_id,
        )

    credentials.append(AzureCliCredential())
    return ChainedTokenCredential(*credentials)


def get_credential() -> AsyncTokenCredential:
    """Return the shared async chained credential (created once on first call)."""
    global _credential
    if _credential is None:
        client_id = os.environ.get("UMI_BACKEND_CLIENT_ID") or os.environ.get(
            "AZURE_CLIENT_ID"
        )
        _credential = _build_credential_chain(client_id)
        _logger.info("Backend credential created (client_id=%s)", client_id)
    return _credential


def get_frontend_credential() -> AsyncTokenCredential:
    """Return an async credential that uses the UMI_FRONTEND_CLIENT_ID identity.

    Falls back to the default credential if UMI_FRONTEND_CLIENT_ID is not set.
    """
    global _frontend_credential
    if _frontend_credential is None:
        frontend_client_id = os.environ.get("UMI_FRONTEND_CLIENT_ID")
        _frontend_credential = _build_credential_chain(frontend_client_id)
        _logger.info("Frontend credential created (client_id=%s)", frontend_client_id)
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
