"""Azure Cosmos DB client singletons and container helpers.

Creates the async Cosmos client once, reuses it for the process lifetime,
and validates that the configured database and containers already exist.
"""

from __future__ import annotations

import asyncio
import logging

from azure.cosmos.aio import ContainerProxy, CosmosClient

from config.app_config import get as cfg
from utils.azure_credential import get_credential

logger = logging.getLogger(__name__)

_DEFAULT_DATABASE_NAME = "azure-sdk-qa-bot"
_DEFAULT_MAPPING_CONTAINER_NAME = "conversation-mappings"
_DEFAULT_MESSAGE_CONTAINER_NAME = "conversation-messages"

_client: CosmosClient | None = None
_mapping_container: ContainerProxy | None = None
_message_container: ContainerProxy | None = None
_client_lock = asyncio.Lock()
_container_lock = asyncio.Lock()


def _get_endpoint() -> str:
    endpoint = cfg("AZURE_COSMOSDB_ENDPOINT", "")
    if not endpoint:
        raise RuntimeError(
            "Azure Cosmos DB endpoint is not configured. Set AZURE_COSMOSDB_ENDPOINT "
            "in Azure App Configuration or the local environment."
        )
    return endpoint


async def _get_client() -> CosmosClient:
    global _client
    if _client is not None:
        return _client

    async with _client_lock:
        if _client is None:
            _client = CosmosClient(
                url=_get_endpoint(),
                credential=get_credential(),
            )
            await _client.__aenter__()
            logger.info("Initialized Azure Cosmos DB client for %s", _get_endpoint())

    return _client


async def _get_container(
    *,
    container_name: str,
) -> ContainerProxy:
    client = await _get_client()
    database_name = _DEFAULT_DATABASE_NAME
    database = client.get_database_client(database_name)

    try:
        await database.read()
    except Exception as exc:
        if getattr(exc, "status_code", None) == 404:
            raise RuntimeError(
                f"Azure Cosmos DB database '{database_name}' does not exist. "
                "Create it first or update AZURE_COSMOSDB_DATABASE."
            ) from exc
        raise

    container = database.get_container_client(container_name)
    try:
        await container.read()
    except Exception as exc:
        if getattr(exc, "status_code", None) == 404:
            raise RuntimeError(
                f"Azure Cosmos DB container '{container_name}' does not exist in "
                f"database '{database_name}'. Create it first or update the relevant "
                "AZURE_COSMOSDB_*_CONTAINER setting."
            ) from exc
        raise

    return container


async def get_conversation_mapping_container() -> ContainerProxy:
    """Return the container storing customer-to-agent conversation mappings."""
    global _mapping_container
    if _mapping_container is not None:
        return _mapping_container

    async with _container_lock:
        if _mapping_container is None:
            container_name = _DEFAULT_MAPPING_CONTAINER_NAME
            _mapping_container = await _get_container(
                container_name=container_name,
            )
            logger.info(
                "Using Cosmos DB conversation mapping container: %s",
                container_name,
            )

    return _mapping_container


async def get_conversation_message_container() -> ContainerProxy:
    """Return the container storing raw conversation messages."""
    global _message_container
    if _message_container is not None:
        return _message_container

    async with _container_lock:
        if _message_container is None:
            container_name = _DEFAULT_MESSAGE_CONTAINER_NAME
            _message_container = await _get_container(
                container_name=container_name,
            )
            logger.info(
                "Using Cosmos DB conversation message container: %s",
                container_name,
            )

    return _message_container


async def close_cosmos_client() -> None:
    """Close the shared Cosmos client and reset cached proxies."""
    global _client, _mapping_container, _message_container
    _mapping_container = None
    _message_container = None
    if _client is not None:
        await _client.close()
        _client = None
