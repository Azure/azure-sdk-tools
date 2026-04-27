"""Azure credential singleton.

Builds a chained credential that supports:
1. Managed Identity (default / Foundry-assigned agent identity)
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


def _build_credential_chain() -> AsyncTokenCredential:
    """Build a chained credential with ManagedIdentity, Pipelines, and CLI."""
    credentials: list[AsyncTokenCredential] = []

    # User-assigned managed identity — used by the server on App Service
    # when AZURE_CLIENT_ID is set. Skipped in Foundry hosted containers.
    client_id = os.environ.get("AZURE_CLIENT_ID")
    if client_id:
        credentials.append(ManagedIdentityCredential(client_id=client_id))

    # Default managed identity — uses the Foundry-assigned agent identity
    # in hosted containers, or the system-assigned identity elsewhere.
    credentials.append(ManagedIdentityCredential())

    # Azure Pipelines federated identity (requires SYSTEM_ACCESSTOKEN and service connection id)
    system_access_token = os.environ.get("SYSTEM_ACCESSTOKEN")
    service_connection_id = os.environ.get("AZURESUBSCRIPTION_SERVICE_CONNECTION_ID")
    tenant_id = os.environ.get("AZURE_TENANT_ID")
    az_client_id = os.environ.get("AZURE_CLIENT_ID")
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
        _credential = _build_credential_chain()
        _logger.info(
            "Credential created (client_id=%s)", os.environ.get("AZURE_CLIENT_ID")
        )
    return _credential


async def close_credential() -> None:
    """Close the credential.  Safe to call even if never created."""
    global _credential
    if _credential is not None:
        await _credential.close()
        _credential = None
