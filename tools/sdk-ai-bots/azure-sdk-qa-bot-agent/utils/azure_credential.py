"""Azure credential singleton.

Builds a chained credential that supports:
1. Managed Identity (default / Foundry-assigned agent identity)
2. Azure Pipelines
3. Azure CLI
"""

import asyncio
import logging
import os

from azure.core.credentials import AccessToken
from azure.core.credentials_async import AsyncTokenCredential
from azure.identity.aio import (
    AzureCliCredential,
    AzurePipelinesCredential,
    ChainedTokenCredential,
    ManagedIdentityCredential,
)

_logger = logging.getLogger(__name__)
_credential: AsyncTokenCredential | None = None


class _RetryCredential:
    """Wraps a credential and retries ``get_token`` on transient failures."""

    def __init__(
        self, inner: AsyncTokenCredential, max_retries: int = 3, delay: float = 3.0
    ):
        self._inner = inner
        self._max_retries = max_retries
        self._delay = delay

    async def get_token(self, *scopes, **kwargs) -> AccessToken:
        for attempt in range(1, self._max_retries + 1):
            try:
                return await self._inner.get_token(*scopes, **kwargs)
            except Exception as exc:
                if attempt < self._max_retries:
                    _logger.warning(
                        "get_token attempt %d/%d failed (%s), retrying in %.0fs",
                        attempt,
                        self._max_retries,
                        exc,
                        self._delay,
                    )
                    await asyncio.sleep(self._delay)
                else:
                    raise

    async def close(self) -> None:
        await self._inner.close()

    async def __aenter__(self):
        return self

    async def __aexit__(self, *args):
        await self.close()


def _build_credential_chain() -> AsyncTokenCredential:
    """Build a chained credential with Pipelines, ManagedIdentity, and CLI.

    When running in an Azure DevOps pipeline (``SYSTEM_ACCESSTOKEN`` is set
    and ``AZURESUBSCRIPTION_*`` env vars are present), returns
    ``AzurePipelinesCredential`` directly — skipping managed identity which
    would resolve to the pool VM's unrelated identity.
    """
    # ── Azure Pipelines shortcut ──
    # The AzureCLI@2 task with addSpnToEnvironment exposes the service
    # principal as AZURESUBSCRIPTION_CLIENT_ID / AZURESUBSCRIPTION_TENANT_ID.
    system_access_token = os.environ.get("SYSTEM_ACCESSTOKEN")
    if system_access_token:
        pipelines_client_id = os.environ.get("AZURESUBSCRIPTION_CLIENT_ID", "")
        pipelines_tenant_id = os.environ.get("AZURESUBSCRIPTION_TENANT_ID", "")
        service_connection_id = os.environ.get(
            "AZURESUBSCRIPTION_SERVICE_CONNECTION_ID", ""
        )
        if pipelines_client_id and pipelines_tenant_id and service_connection_id:
            _logger.info(
                "Running in Azure DevOps pipeline; using AzurePipelinesCredential "
                "(service_connection=%s)",
                service_connection_id,
            )
            return AzurePipelinesCredential(
                tenant_id=pipelines_tenant_id,
                client_id=pipelines_client_id,
                service_connection_id=service_connection_id,
                system_access_token=system_access_token,
            )
        _logger.warning(
            "SYSTEM_ACCESSTOKEN is set but AZURESUBSCRIPTION_* env vars are "
            "incomplete; falling back to credential chain"
        )

    # ── Standard credential chain (non-pipeline environments) ──
    credentials: list[AsyncTokenCredential] = []

    # User-assigned managed identity — used by the server on App Service
    # when AZURE_CLIENT_ID is set. Skipped in Foundry hosted containers.
    client_id = os.environ.get("AZURE_CLIENT_ID")
    if client_id:
        credentials.append(ManagedIdentityCredential(client_id=client_id))

    # Default managed identity — uses the Foundry-assigned agent identity
    # in hosted containers, or the system-assigned identity elsewhere.
    credentials.append(ManagedIdentityCredential())

    credentials.append(AzureCliCredential())
    return ChainedTokenCredential(*credentials)


def get_credential() -> AsyncTokenCredential:
    """Return the shared async chained credential (created once on first call).

    Wraps the inner chain with retry logic so that transient failures
    (e.g. managed-identity IMDS not ready during cold start) are retried
    automatically on every ``get_token`` call.
    """
    global _credential
    if _credential is None:
        _credential = _RetryCredential(_build_credential_chain())
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
