"""Azure Key Vault secret loader.

Fetches required secrets from Key Vault and sets them as environment variables.
"""

from __future__ import annotations

import logging
import os

logger = logging.getLogger(__name__)


async def init_secrets() -> None:
    """Load secrets from Azure Key Vault into environment variables."""
    endpoint = os.environ.get("KEYVAULT_ENDPOINT")
    if not endpoint:
        logger.warning("KEYVAULT_ENDPOINT not set; skipping Key Vault secrets")
        return

    from azure.identity.aio import (
        AzureCliCredential,
        ChainedTokenCredential,
        ManagedIdentityCredential,
        WorkloadIdentityCredential,
    )
    from azure.keyvault.secrets.aio import SecretClient

    logger.info("Loading secrets from Azure Key Vault...")

    credentials = []
    for cls in (WorkloadIdentityCredential, AzureCliCredential, ManagedIdentityCredential):
        try:
            credentials.append(cls())
        except Exception as e:  # noqa: BLE001
            logger.debug("Skipping %s: %s", cls.__name__, e)
    if not credentials:
        raise RuntimeError("No Azure credentials available for Key Vault")
    credential = ChainedTokenCredential(*credentials)

    client = SecretClient(vault_url=endpoint, credential=credential)

    try:
        # AI Search API Key
        secret = await client.get_secret("AI-SEARCH-APIKEY")
        if secret.value:
            os.environ["AI_SEARCH_API_KEY"] = secret.value
            logger.info("Set AI_SEARCH_API_KEY from Key Vault")

        # Azure OpenAI API Key
        secret = await client.get_secret("AOAI-CHAT-COMPLETIONS-API-KEY")
        if secret.value:
            os.environ["AOAI_CHAT_COMPLETIONS_API_KEY"] = secret.value
            logger.info("Set AOAI_CHAT_COMPLETIONS_API_KEY from Key Vault")

        # SSH private key
        secret = await client.get_secret("SSH-PRIVATE-KEY")
        if secret.value:
            os.environ["SSH_PRIVATE_KEY"] = secret.value
            logger.info("Set SSH_PRIVATE_KEY from Key Vault")

    finally:
        await client.close()
        await credential.close()

    logger.info("Successfully loaded secrets from Azure Key Vault")
