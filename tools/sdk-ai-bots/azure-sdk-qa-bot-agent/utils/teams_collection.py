"""Authenticated Logic App reads and optimistic Cosmos writes for Teams archives."""

import asyncio
from datetime import datetime, timezone
from urllib.parse import parse_qs, urlsplit
from uuid import uuid4

import httpx
from azure.core import MatchConditions
from azure.cosmos import exceptions

from services.teams_collection_service import TeamsCollectionService, validate_channels


class LogicAppPageClient:
    def __init__(self, client, credential, url: str, audience: str, channels: list[dict]):
        address = urlsplit(url)
        if (address.scheme != "https" or not address.hostname
                or not address.hostname.endswith(".logic.azure.com")
                or address.username or address.password or address.port not in (None, 443)
                or address.fragment or set(parse_qs(address.query)) - {"api-version"}
                or not address.path.endswith("/triggers/manual/paths/invoke")):
            raise ValueError("Configure an HTTPS Logic App manual trigger URL without SAS parameters.")
        if audience != "https://management.core.windows.net/":
            raise ValueError("Logic App audience must match the collection template.")
        validate_channels(channels)
        self._client = client
        self._credential = credential
        self._url = url
        self._audience = audience
        self._channels = channels

    async def fetch_page(self, channel, message_id, skip_token):
        if channel not in self._channels:
            raise ValueError("Channel is not in the configured collection allowlist.")
        payload = {"teamId": channel["teamId"], "channelId": channel["channelId"],
                   "operation": "replies" if message_id is not None else "messages"}
        if message_id is not None:
            payload["messageId"] = message_id
        if skip_token is not None:
            payload["skipToken"] = skip_token
        token = await self._credential.get_token(self._audience + ".default")
        response = None
        for attempt in range(4):
            try:
                response = await self._client.post(
                    self._url, json=payload, headers={"Authorization": f"Bearer {token.token}"},
                    follow_redirects=False, timeout=180,
                )
            except httpx.RequestError:
                if attempt == 3:
                    raise RuntimeError(
                        "Logic App request failed; retry the collection after checking workflow status."
                    ) from None
            else:
                if response.status_code == 200:
                    break
                if response.status_code not in (408, 429, 500, 502, 503, 504) or attempt == 3:
                    raise RuntimeError(
                        f"Logic App returned HTTP {response.status_code}; collection was not completed."
                    )
            await asyncio.sleep(2 ** attempt)
        if response is None:
            raise RuntimeError("Logic App request did not produce a response.")
        try:
            result = response.json()
            if result["operation"] != payload["operation"]:
                raise ValueError
            return result["data"]
        except (ValueError, KeyError, TypeError):
            raise ValueError("Logic App returned an invalid collection response.") from None


class CosmosThreadStore:
    def __init__(self, container):
        self._container = container

    async def validate(self):
        properties = await self._container.read()
        if properties.get("partitionKey", {}).get("paths") != ["/channel_key"]:
            raise ValueError("Collection container must use partition key /channel_key.")

    async def read(self, document_id, partition):
        try:
            return await self._container.read_item(item=document_id, partition_key=partition)
        except exceptions.CosmosResourceNotFoundError:
            return None

    async def read_channel_index(self, partition):
        items = self._container.query_items(
            query=("SELECT c.id, c.content_hash, c.processing, c._etag "
                   "FROM c WHERE IS_DEFINED(c.post_id)"),
            partition_key=partition,
        )
        return {item["id"]: item async for item in items}

    async def read_channel_documents(self, partition):
        items = self._container.query_items(
            query="SELECT * FROM c WHERE IS_DEFINED(c.post_id)",
            partition_key=partition,
        )
        return [item async for item in items]

    async def write(self, document, previous):
        try:
            if previous is None:
                await self._container.create_item(body=document)
            else:
                await self._container.replace_item(
                    item=document["id"], body=document, etag=previous["_etag"],
                    match_condition=MatchConditions.IfNotModified,
                )
        except exceptions.CosmosHttpResponseError as error:
            if error.status_code in (409, 412):
                raise RuntimeError("Concurrent collection changed this thread; retry rather than overwrite it.") from None
            raise

    async def record_run(self, document):
        await self._container.upsert_item(body=document)


async def _configured_store():
    from utils.azure_cosmosdb import get_teams_channel_posts_container

    container = await get_teams_channel_posts_container()
    store = CosmosThreadStore(container)
    await store.validate()
    return store


def _new_run(operation, channel_count):
    run_id = str(uuid4())
    run = {"id": f"run-{run_id}", "channel_key": "collection-runs", "type": "collection-run",
           "status": "running", "started_at": datetime.now(timezone.utc).isoformat(),
           "operation": operation, "channelCount": channel_count}
    return run_id, run


async def collect_configured_channels(config, settings, processor=None):
    from utils.azure_credential import get_credential

    validate_channels(config["channels"])
    if config.get("processing") is not None and processor is None:
        raise RuntimeError("Configured Teams processing requires a processor.")
    store = await _configured_store()
    run_id, run = _new_run("collect", len(config["channels"]))
    await store.record_run(run)
    try:
        async with httpx.AsyncClient() as client:
            pages = LogicAppPageClient(
                client, get_credential(), settings("TEAMS_COLLECTION_LOGIC_APP_URL", ""),
                "https://management.core.windows.net/", config["channels"],
            )
            service = TeamsCollectionService(
                pages.fetch_page, store, config["tenantId"], config["maxPages"],
                config.get("lookbackDays"), processor,
            )
            summary = await service.collect(config["channels"])
        run.update({"status": "succeeded", "summary": summary})
    except (Exception, asyncio.CancelledError) as error:
        run.update({"status": "failed", "error_type": type(error).__name__})
        raise
    finally:
        run["ended_at"] = datetime.now(timezone.utc).isoformat()
        await store.record_run(run)
    return {"runId": run_id, **summary}


async def reprocess_configured_channels(config, processor):
    validate_channels(config["channels"])
    if processor is None:
        raise RuntimeError("A Teams thread processor is required for reprocessing.")
    store = await _configured_store()
    run_id, run = _new_run("reprocess", len(config["channels"]))
    await store.record_run(run)
    try:
        service = TeamsCollectionService(
            None, store, config["tenantId"], config["maxPages"],
            config.get("lookbackDays"), processor,
        )
        summary = await service.reprocess(config["channels"])
        run.update({"status": "succeeded", "summary": summary})
    except (Exception, asyncio.CancelledError) as error:
        run.update({"status": "failed", "error_type": type(error).__name__})
        raise
    finally:
        run["ended_at"] = datetime.now(timezone.utc).isoformat()
        await store.record_run(run)
    return {"runId": run_id, **summary}