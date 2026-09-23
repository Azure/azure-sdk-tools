"""Backfill historical Teams channel posts into the shared conversation store.

One Cosmos document is written per Teams message, in exactly the shape the
realtime bot Logic App writes, so a backfilled thread and a live thread are
indistinguishable to every downstream reader. Reads go through a dedicated
Logic App so the bot pipeline keeps no side effects here.

The module holds the whole backfill story: the message shaping and paging
rules, the Logic App reader and Cosmos writer they run against, and
``backfill_configured_channels`` which wires the two together for the hosted
agent. It also owns the shape a stored Teams body has, so ``split_subject``
lives here beside the ``title:`` line ``normalized_content`` writes even though
only summarization reads it back.
"""

from __future__ import annotations

import asyncio
import json
from datetime import datetime, timezone
from urllib.parse import parse_qs, unquote, urlsplit
from uuid import UUID

import httpx
from azure.core import MatchConditions
from azure.cosmos import exceptions

from models.conversation import (
    ConversationDocumentType,
    ConversationMessageExtraInfo,
    ConversationMessageItem,
    ConversationType,
    Role,
)

CONVERSATION_TYPE = ConversationType.teams_channel.value

_FORWARDED_CONTENT_TYPE = "forwardedMessageReference"
_TITLE_PREFIX = "title: "

_FORWARDED_CONTENT_PATHS = (
    ("body", "content"),
    ("message", "body", "content"),
    ("originalMessage", "body", "content"),
    ("content",),
)


def parse_timestamp(value: object, field: str) -> datetime:
    try:
        if not isinstance(value, str):
            raise ValueError
        timestamp = datetime.fromisoformat(value)
        if timestamp.tzinfo is None:
            raise ValueError
        return timestamp.astimezone(timezone.utc)
    except (TypeError, ValueError, OverflowError):
        raise ValueError(f"{field} must be an ISO 8601 timestamp with a timezone.") from None


def validate_channels(channels: list[dict]) -> None:
    if not isinstance(channels, list) or not channels:
        raise ValueError("At least one Teams channel must be configured.")
    seen = set()
    for channel in channels:
        UUID(channel["teamId"])
        channel_id = channel["channelId"]
        if not isinstance(channel_id, str) or not channel_id.strip():
            raise ValueError("Each channel requires a nonempty channelId.")
        tenant_key = channel.get("tenantKey")
        if not isinstance(tenant_key, str) or not tenant_key.strip():
            raise ValueError(
                "Each channel requires the tenantKey the bot uses for that channel."
            )
        if channel.get("startTime") is not None:
            parse_timestamp(channel["startTime"], "startTime")
        identity = (channel["teamId"], channel_id)
        if identity in seen:
            raise ValueError("Duplicate Teams channel configuration.")
        seen.add(identity)


def select_channels(channels: list[dict], channel_id: str | None) -> list[dict]:
    validate_channels(channels)
    if channel_id is None:
        return channels
    selected = [channel for channel in channels if channel["channelId"] == channel_id]
    if not selected:
        raise ValueError("Channel is not in the configured collection allowlist.")
    return selected


def _with_start_time(channels: list[dict], start_time: str | None) -> list[dict]:
    if start_time is None:
        return channels
    return [{**channel, "startTime": start_time} for channel in channels]


def conversation_id(channel_id: str, root_id: str) -> str:
    return f"{channel_id};messageid={root_id}"


def thread_partition(channel_id: str, root_id: str) -> str:
    return f"{CONVERSATION_TYPE}:{conversation_id(channel_id, root_id)}"


def channel_partition_prefix(channel_id: str) -> str:
    """Prefix shared by every thread partition of one channel.

    Keeping ``;messageid=`` in the prefix stops a channel ID from matching
    another channel whose ID merely starts with the same characters.
    """
    return f"{CONVERSATION_TYPE}:{channel_id};messageid="


def root_id_from_partition(partition: str) -> str | None:
    marker = ";messageid="
    index = partition.rfind(marker)
    if index == -1:
        return None
    return partition[index + len(marker):] or None


def continuation_token(link: str, channel: dict, message_id: str | None) -> str:
    address = urlsplit(link)
    expected = f"/teams/{channel['teamId']}/channels/{channel['channelId']}/messages"
    if message_id is not None:
        expected += f"/{message_id}/replies"
    prefix = "/v1.0" if message_id is not None else "/beta"
    path = unquote(address.path)
    route = path.split("/", 4)
    graph_route = address.hostname == "graph.microsoft.com" and path == prefix + expected
    connector_route = (
        bool(address.hostname) and address.hostname.endswith(".azure-apim.net")
        and len(route) == 5 and route[1:3] == ["apim", "teams"]
        and bool(route[3]) and "/" + route[4] == prefix + expected
    )
    if (address.scheme != "https" or not address.hostname
            or address.username or address.password or address.port not in (None, 443)
            or address.fragment or not (graph_route or connector_route)):
        raise ValueError("Unexpected Teams connector continuation URL.")
    queries = parse_qs(address.query, keep_blank_values=True)
    tokens = queries.get("$skiptoken", [])
    allowed_queries = {"$skiptoken", "$top"}
    if message_id is None and queries.get("$expand") == ["replies"]:
        allowed_queries.add("$expand")
    if len(tokens) != 1 or not tokens[0] or set(queries) - allowed_queries:
        raise ValueError("Unexpected Teams connector continuation parameters.")
    return tokens[0]


def _validate_messages(messages, message_id=None, allow_missing_reply_to=False):
    """Validate the raw message list and return only entries whose messageType is "message".

    Every entry must have an ID, but reply-to-thread validation applies only to
    actual messages because Teams system event records do not consistently carry
    a replyToId.
    """
    if not isinstance(messages, list):
        raise ValueError("Teams connector did not return a message list.")
    filtered = []
    for message in messages:
        if not isinstance(message, dict) or not isinstance(message.get("id"), str) or not message["id"]:
            raise ValueError("Teams connector returned a message without an ID.")
        if message.get("messageType") != "message":
            continue
        reply_to_id = message.get("replyToId")
        if (message_id is not None and reply_to_id != message_id
                and not (allow_missing_reply_to and reply_to_id is None)):
            raise ValueError("Reply does not belong to the requested thread.")
        filtered.append(message)
    return filtered


def _message_activity(message: dict, field: str) -> datetime:
    timestamps = []
    for name in ("lastModifiedDateTime", "createdDateTime"):
        if message.get(name) is not None:
            timestamps.append(parse_timestamp(message[name], f"{field} {name}"))
    if not timestamps:
        raise ValueError(f"{field} requires createdDateTime or lastModifiedDateTime.")
    return max(timestamps)


def _thread_activity(root: dict, replies: list[dict]) -> datetime:
    return max([_message_activity(root, "Post"), *(
        _message_activity(reply, "Reply") for reply in replies
    )])


def _forwarded_text(content) -> str | None:
    """Return the inline text for a forwarded attachment, or None to skip it."""
    parsed = content
    if isinstance(parsed, str):
        try:
            parsed = json.loads(parsed)
        except ValueError:
            return content
    if isinstance(parsed, dict):
        for path in _FORWARDED_CONTENT_PATHS:
            value = parsed
            for name in path:
                value = value.get(name) if isinstance(value, dict) else None
            if isinstance(value, str) and value:
                return value
    return content if isinstance(content, str) else None


def _resolve_forwarded_content(message: dict) -> str:
    body = message.get("body")
    content = body.get("content") if isinstance(body, dict) else None
    content = content if isinstance(content, str) else ""
    attachments = message.get("attachments")
    for attachment in attachments if isinstance(attachments, list) else []:
        if (not isinstance(attachment, dict)
                or attachment.get("contentType") != _FORWARDED_CONTENT_TYPE
                or not attachment.get("content")):
            continue
        forwarded = _forwarded_text(attachment["content"])
        if forwarded is None:
            continue
        content = content.replace(
            f'<attachment id="{attachment.get("id")}"></attachment>', forwarded
        )
    return content


def normalized_content(message: dict) -> str:
    """Return the message body exactly as the realtime workflow would store it.

    The realtime workflow rewrites every message before it reaches
    ``/conversation/save``: forwarded-message attachments are inlined and a post
    subject is prepended as a ``title:`` line. Backfill has to apply the same two
    steps or every run would read the stored (rewritten) content as changed and
    overwrite it with the raw Graph body. Mirrors
    ``Resolve_forwarded_message_content`` and
    ``Include_post_title_in_message_content`` in ``pipelines/logicapp/template.json``.
    """
    content = _resolve_forwarded_content(message)
    subject = message.get("subject")
    if isinstance(subject, str) and subject.strip():
        content = f"{_TITLE_PREFIX}{subject.strip()}\n\n{content}"
    return content


def split_subject(content: str) -> tuple[str | None, str]:
    """Split a stored body back into its subject and the remaining body.

    The inverse of the ``title:`` line ``normalized_content`` writes, kept beside
    it so the two cannot drift. Lets summarization keep seeing a post subject
    without the ``title:`` line being duplicated inside the question text.
    """
    if not isinstance(content, str) or not content.startswith(_TITLE_PREFIX):
        return None, content if isinstance(content, str) else ""
    subject, _, body = content[len(_TITLE_PREFIX):].partition("\n\n")
    subject = subject.strip()
    return (subject, body) if subject else (None, content)


def _sender(message: dict) -> tuple[Role, str, str]:
    origin = message.get("from")
    origin = origin if isinstance(origin, dict) else {}
    application = origin.get("application")
    if isinstance(application, dict):
        return Role.System, application.get("id") or "", application.get("displayName") or ""
    user = origin.get("user")
    user = user if isinstance(user, dict) else {}
    return Role.User, user.get("id") or "", user.get("displayName") or ""


def message_document(channel: dict, message: dict, root_id: str) -> dict:
    """Build the Cosmos document for one Teams message.

    Mirrors ``Build_Conversation_Save_Request_Body`` in the realtime workflow.
    """
    sender_role, sender_id, sender_name = _sender(message)
    item = ConversationMessageItem(
        id=message["id"],
        tenant_id=channel["tenantKey"],
        sender_role=sender_role,
        sender_id=sender_id,
        sender_name=sender_name,
        content=normalized_content(message),
        created_at=parse_timestamp(
            message.get("createdDateTime"), "Message createdDateTime"
        ),
        conversation_id=conversation_id(channel["channelId"], root_id),
        conversation_type=ConversationType.teams_channel,
        conversation_partition=thread_partition(channel["channelId"], root_id),
        extra_info=ConversationMessageExtraInfo(
            channel_id=channel["channelId"],
            message_link=message.get("webUrl"),
        ),
    )
    return item.model_dump(mode="json", exclude_none=True)


class TeamsCollectionService:
    """Backfill a channel's history without disturbing what the bot already owns."""

    def __init__(self, fetch_page, store, max_pages: int = 1000):
        if not 1 <= max_pages <= 1000:
            raise ValueError("max_pages must be between 1 and 1000.")
        self._fetch_page = fetch_page
        self._store = store
        self._max_pages = max_pages

    async def _pages(self, channel: dict, message_id: str | None = None):
        token = None
        seen = set()
        for _page_index in range(self._max_pages):
            page = await self._fetch_page(channel, message_id, token)
            if not isinstance(page, dict):
                raise ValueError("Teams connector did not return a message list.")
            messages = _validate_messages(page.get("value"), message_id)
            yield messages
            link = page.get("@odata.nextLink")
            if not link:
                return
            token = continuation_token(link, channel, message_id)
            if token in seen:
                raise RuntimeError("Teams connector repeated a continuation token.")
            seen.add(token)
        raise RuntimeError("Page limit reached; collection is incomplete.")

    async def _replies(self, channel: dict, root: dict) -> list[dict]:
        root_id = root["id"]
        replies: dict[str, dict] = {}
        if "replies" in root and not root.get("replies@odata.nextLink"):
            for reply in _validate_messages(root["replies"], root_id, allow_missing_reply_to=True):
                replies[reply["id"]] = reply
        else:
            async for page in self._pages(channel, root_id):
                for reply in page:
                    replies[reply["id"]] = reply
        return sorted(replies.values(), key=lambda reply: (
            reply.get("createdDateTime") or "", reply["id"]
        ))

    async def _store_thread(self, channel: dict, root: dict, replies: list[dict],
                            summary: dict) -> None:
        root_id = root["id"]
        partition = thread_partition(channel["channelId"], root_id)
        stored = await self._store.read_thread(partition)
        for message in (root, *replies):
            summary["messagesRead"] += 1
            existing = stored.get(message["id"])
            if existing is None:
                created = await self._store.create(message_document(channel, message, root_id))
                summary["messagesCreated" if created else "messagesSkipped"] += 1
                continue
            content = normalized_content(message)
            if existing.get("content") == content:
                summary["messagesUnchanged"] += 1
                continue
            updated = await self._store.update_content(message["id"], partition, content)
            summary["messagesUpdated" if updated else "messagesSkipped"] += 1

    async def backfill(self, channels: list[dict]) -> dict:
        """Add every missing message and refresh every edited one, nothing else."""
        validate_channels(channels)
        summary = {"channelsCompleted": 0, "threadsRead": 0, "messagesRead": 0,
                   "messagesCreated": 0, "messagesUpdated": 0, "messagesUnchanged": 0,
                   "messagesSkipped": 0}
        for channel in channels:
            start_time = (parse_timestamp(channel["startTime"], "startTime")
                          if channel.get("startTime") is not None else None)
            seen_roots = set()
            reached_start_time = False
            async for roots in self._pages(channel):
                for root in roots:
                    root_id = root["id"]
                    if root_id in seen_roots:
                        continue
                    seen_roots.add(root_id)
                    replies = await self._replies(channel, root)
                    if start_time is not None:
                        # Graph orders roots by the latest activity in the whole
                        # reply chain, and a post is never newer than that
                        # activity, so nothing after this point can qualify.
                        if _thread_activity(root, replies) < start_time:
                            reached_start_time = True
                            break
                        if parse_timestamp(
                            root.get("createdDateTime"), "Post createdDateTime"
                        ) < start_time:
                            continue
                    summary["threadsRead"] += 1
                    await self._store_thread(channel, root, replies, summary)
                if reached_start_time:
                    break
            summary["channelsCompleted"] += 1
        return summary


class LogicAppPageClient:
    """Read Teams pages through the dedicated Logic App, never the bot's own."""

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
                    self._url, json=payload, headers={"Authorization": f"******"},
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


class CosmosMessageStore:
    """Add missing messages and refresh edited content, never remove anything.

    Backfill shares ``conversation-messages`` with the live bot, so a write
    that loses to a concurrent bot write is reported as skipped and retried on
    the next run instead of overwriting fresher data.
    """

    def __init__(self, container):
        self._container = container

    async def validate(self):
        properties = await self._container.read()
        if properties.get("partitionKey", {}).get("paths") != ["/conversation_partition"]:
            raise ValueError("Backfill container must use partition key /conversation_partition.")

    async def read_thread(self, partition):
        items = self._container.query_items(
            query="SELECT c.id, c.content FROM c WHERE c.document_type = @dtype",
            parameters=[
                {"name": "@dtype", "value": ConversationDocumentType.message.value},
            ],
            partition_key=partition,
        )
        return {item["id"]: item async for item in items}

    async def create(self, document):
        try:
            await self._container.create_item(body=document)
        except exceptions.CosmosResourceExistsError:
            return False
        return True

    async def update_content(self, document_id, partition, content):
        try:
            existing = await self._container.read_item(item=document_id, partition_key=partition)
        except exceptions.CosmosResourceNotFoundError:
            return False
        if existing.get("content") == content:
            return False
        existing["content"] = content
        try:
            await self._container.replace_item(
                item=document_id, body=existing, etag=existing["_etag"],
                match_condition=MatchConditions.IfNotModified,
            )
        except exceptions.CosmosAccessConditionFailedError:
            return False
        return True


async def _message_store():
    from utils.azure_cosmosdb import get_conversation_message_container

    store = CosmosMessageStore(await get_conversation_message_container())
    await store.validate()
    return store


async def backfill_configured_channels(config, settings, channel_id=None, start_time=None):
    from utils.azure_credential import get_credential

    channels = _with_start_time(select_channels(config["channels"], channel_id), start_time)
    validate_channels(channels)
    store = await _message_store()
    async with httpx.AsyncClient() as client:
        pages = LogicAppPageClient(
            client, get_credential(), settings("TEAMS_COLLECTION_LOGIC_APP_URL", ""),
            "https://management.core.windows.net/", channels,
        )
        service = TeamsCollectionService(pages.fetch_page, store, config["maxPages"])
        return await service.backfill(channels)
