"""Azure Cosmos DB client singletons and container helpers.

Creates the async Cosmos client once, reuses it for the process lifetime,
and validates that the configured database and containers already exist.
"""

from __future__ import annotations

import asyncio
import logging
from typing import Any

from azure.cosmos import PartitionKey, exceptions as cosmos_exceptions
from azure.cosmos.aio import ContainerProxy, CosmosClient

from config.app_config import get as cfg
from utils.azure_credential import get_credential
from utils.azure_keyvault import get_secret

logger = logging.getLogger(__name__)

_DEFAULT_EPISODE_SEARCH_TOP_K = 5

_DEFAULT_DATABASE_NAME = "azure-sdk-qa-bot"
_DEFAULT_MAPPING_CONTAINER_NAME = "conversation-mappings"
_DEFAULT_MESSAGE_CONTAINER_NAME = "conversation-messages"
_DEFAULT_EPISODE_CONTAINER_NAME = "experience-episodes"

# Retry defaults for the Cosmos DB client
_DEFAULT_RETRY_TOTAL = 3  # Maximum number of total retry attempts
_DEFAULT_RETRY_CONNECT = 3  # Maximum retries on connection errors
_DEFAULT_RETRY_READ = 3  # Maximum retries on read errors
_DEFAULT_RETRY_STATUS = 3  # Maximum retries on bad status codes
_DEFAULT_RETRY_BACKOFF_FACTOR = 0.8  # Exponential backoff multiplier (seconds)
_DEFAULT_RETRY_BACKOFF_MAX = 30  # Maximum backoff interval (seconds)

# Embedding dimensions for text-embedding-3-small
_EMBEDDING_DIMENSIONS = 1536

_client: CosmosClient | None = None
_mapping_container: ContainerProxy | None = None
_message_container: ContainerProxy | None = None
_episode_container: ContainerProxy | None = None
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


async def _get_cosmos_credential():
    """Return the Cosmos DB account key from Key Vault.

    Falls back to the shared token credential when the key cannot be
    fetched (e.g. local dev).
    """
    key = await get_secret("AZURE-COSMOSDB-KEY")
    if key:
        logger.info("Using Cosmos DB key-based auth (from Key Vault)")
        return key
    return get_credential()


async def _get_client() -> CosmosClient:
    global _client
    if _client is not None:
        return _client

    async with _client_lock:
        if _client is None:
            client = CosmosClient(
                url=_get_endpoint(),
                credential=await _get_cosmos_credential(),
                retry_total=int(
                    cfg("AZURE_COSMOSDB_RETRY_TOTAL", str(_DEFAULT_RETRY_TOTAL))
                ),
                retry_connect=int(
                    cfg("AZURE_COSMOSDB_RETRY_CONNECT", str(_DEFAULT_RETRY_CONNECT))
                ),
                retry_read=int(
                    cfg("AZURE_COSMOSDB_RETRY_READ", str(_DEFAULT_RETRY_READ))
                ),
                retry_status=int(
                    cfg("AZURE_COSMOSDB_RETRY_STATUS", str(_DEFAULT_RETRY_STATUS))
                ),
                retry_backoff_factor=float(
                    cfg(
                        "AZURE_COSMOSDB_RETRY_BACKOFF_FACTOR",
                        str(_DEFAULT_RETRY_BACKOFF_FACTOR),
                    )
                ),
                retry_backoff_max=int(
                    cfg(
                        "AZURE_COSMOSDB_RETRY_BACKOFF_MAX",
                        str(_DEFAULT_RETRY_BACKOFF_MAX),
                    )
                ),
            )
            await client.__aenter__()
            _client = client
            logger.info("Initialized Azure Cosmos DB client for %s", _get_endpoint())

    assert _client is not None
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
    global _client, _mapping_container, _message_container, _episode_container
    _mapping_container = None
    _message_container = None
    _episode_container = None
    if _client is not None:
        await _client.__aexit__(None, None, None)
        _client = None


# ---------------------------------------------------------------------------
# Episode container (experience-episodes)
# ---------------------------------------------------------------------------


async def ensure_episode_container() -> ContainerProxy:
    """Return the ``experience-episodes`` container, creating it if needed.

    The container is configured with:
    - Partition key ``/tenant_id``
    - A vector embedding policy on the ``embedding`` field for
      cosine-similarity search.
    - A vector index of type ``quantizedFlat`` for efficient ANN queries.
    """
    global _episode_container
    if _episode_container is not None:
        return _episode_container

    async with _container_lock:
        if _episode_container is not None:
            return _episode_container

        container_name = _DEFAULT_EPISODE_CONTAINER_NAME

        try:
            _episode_container = await _get_container(
                container_name=container_name,
            )
            logger.info("Using Cosmos DB episode container: %s", container_name)
        except RuntimeError:
            # Container doesn't exist — create it with vector indexing
            client = await _get_client()
            database = client.get_database_client(_DEFAULT_DATABASE_NAME)

            vector_embedding_policy: dict[str, Any] = {
                "vectorEmbeddings": [
                    {
                        "path": "/embedding",
                        "dataType": "float32",
                        "distanceFunction": "cosine",
                        "dimensions": _EMBEDDING_DIMENSIONS,
                    },
                ],
            }

            indexing_policy: dict[str, Any] = {
                "indexingMode": "consistent",
                "automatic": True,
                "includedPaths": [{"path": "/*"}],
                "excludedPaths": [{"path": "/embedding/*"}],
                "vectorIndexes": [
                    {"path": "/embedding", "type": "quantizedFlat"},
                ],
            }

            _episode_container = await database.create_container(
                id=container_name,
                partition_key=PartitionKey(path="/tenant_id"),
                indexing_policy=indexing_policy,
                vector_embedding_policy=vector_embedding_policy,
            )
            logger.info("Created episode container: %s", container_name)

    return _episode_container


async def get_episode_container() -> ContainerProxy:
    """Return the episode container, creating it if needed."""
    if _episode_container is not None:
        return _episode_container
    return await ensure_episode_container()


async def save_episode(document: dict[str, Any]) -> dict[str, Any]:
    """Upsert an episode document into the episode container."""
    container = await get_episode_container()
    result = await container.upsert_item(document)
    return result


async def search_episodes_by_vector(
    tenant_id: str,
    query_embedding: list[float],
    *,
    top_k: int = _DEFAULT_EPISODE_SEARCH_TOP_K,
) -> list[dict[str, Any]]:
    """Search episodes by vector similarity within a tenant partition.

    Uses Cosmos DB's ``VectorDistance`` function for cosine similarity.
    Returns the top-k most similar episodes ordered by relevance.
    """
    container = await get_episode_container()
    query = (
        "SELECT TOP @top_k c.id, c.tenant_id, c.trigger, "
        "c.symptoms, c.reasoning_chain, c.resolution, c.key_insight, "
        "c.confidence, c.source_thread_id, c.created_at, "
        "VectorDistance(c.embedding, @embedding) AS similarity_score "
        "FROM c "
        "WHERE c.tenant_id = @tenant_id "
        "ORDER BY VectorDistance(c.embedding, @embedding)"
    )
    parameters = [
        {"name": "@top_k", "value": top_k},
        {"name": "@tenant_id", "value": tenant_id},
        {"name": "@embedding", "value": query_embedding},
    ]
    items: list[dict[str, Any]] = []
    async for item in container.query_items(
        query=query,
        parameters=parameters,
        partition_key=tenant_id,
    ):
        items.append(item)
    logger.info(
        "Vector search returned %d episodes for tenant=%s",
        len(items),
        tenant_id,
    )
    return items


async def query_episodes(
    *,
    tenant_id: str | None = None,
    source_thread_id: str | None = None,
) -> list[dict[str, Any]]:
    """Query episodes by tenant and/or source thread ID.

    Returns episodes without the embedding field for efficiency.
    """
    container = await get_episode_container()

    fields = (
        "c.id, c.tenant_id, c.trigger, c.symptoms, c.reasoning_chain, "
        "c.resolution, c.key_insight, c.confidence, c.source_thread_id, "
        "c.message_count, c.created_at, c.updated_at"
    )
    conditions: list[str] = []
    parameters: list[dict] = []
    partition_key: str | None = None

    if tenant_id:
        conditions.append("c.tenant_id = @tenant_id")
        parameters.append({"name": "@tenant_id", "value": tenant_id})
        partition_key = tenant_id

    if source_thread_id:
        conditions.append("CONTAINS(c.source_thread_id, @thread_id)")
        parameters.append({"name": "@thread_id", "value": source_thread_id})

    where = f" WHERE {' AND '.join(conditions)}" if conditions else ""
    query = f"SELECT {fields} FROM c{where} ORDER BY c.created_at DESC"

    kwargs: dict[str, Any] = {"query": query, "parameters": parameters}
    if partition_key:
        kwargs["partition_key"] = partition_key
    else:
        kwargs["enable_cross_partition_query"] = True

    items: list[dict[str, Any]] = []
    async for item in container.query_items(**kwargs):
        items.append(item)
    return items
