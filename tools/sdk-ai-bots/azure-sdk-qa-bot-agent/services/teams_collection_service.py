"""Collect complete Teams threads through a dedicated Logic App, without bot side effects."""

from __future__ import annotations

import hashlib
import json
from datetime import datetime, timedelta, timezone
from urllib.parse import parse_qs, unquote, urlsplit
from uuid import UUID


_COSMOS_DOCUMENT_SIZE_BUDGET = 1_900_000


def _parse_timestamp(value: str, field: str) -> datetime:
    try:
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
        if channel.get("startTime") is not None:
            _parse_timestamp(channel["startTime"], "startTime")
        identity = (channel["teamId"], channel_id)
        if identity in seen:
            raise ValueError("Duplicate Teams channel configuration.")
        seen.add(identity)


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
            timestamps.append(_parse_timestamp(message[name], f"{field} {name}"))
    if not timestamps:
        raise ValueError(f"{field} requires createdDateTime or lastModifiedDateTime.")
    return max(timestamps)


def _thread_activity(root: dict, replies: list[dict]) -> datetime:
    return max([_message_activity(root, "Post"), *(
        _message_activity(reply, "Reply") for reply in replies
    )])


def _channel_key(tenant_id: str, channel: dict) -> str:
    return hashlib.sha256(json.dumps(
        [tenant_id, channel["teamId"], channel["channelId"]]
    ).encode()).hexdigest()


def _thread_content(root: dict, replies: list[dict]) -> dict:
    post = {name: value for name, value in root.items()
            if name not in ("replies", "replies@odata.nextLink", "replies@odata.count")}
    return {"post": post, "replies": replies}


def _content_hash(content: dict) -> str:
    return hashlib.sha256(json.dumps(
        content, sort_keys=True, ensure_ascii=False, separators=(",", ":")
    ).encode("utf-8")).hexdigest()


def _validate_document_size(document: dict) -> None:
    if len(json.dumps(document, ensure_ascii=False).encode("utf-8")) > _COSMOS_DOCUMENT_SIZE_BUDGET:
        raise ValueError("Thread exceeds the Cosmos document size budget; no partial write was made.")


class TeamsCollectionService:
    def __init__(self, fetch_page, store, tenant_id: str, max_pages: int = 1000,
                 lookback_days: int | None = None, processor=None):
        UUID(tenant_id)
        if not 1 <= max_pages <= 1000:
            raise ValueError("max_pages must be between 1 and 1000.")
        if (lookback_days is not None
                and (isinstance(lookback_days, bool) or not isinstance(lookback_days, int)
                     or not 1 <= lookback_days <= 30)):
            raise ValueError("lookback_days must be between 1 and 30.")
        self._fetch_page = fetch_page
        self._store = store
        self._tenant_id = tenant_id
        self._max_pages = max_pages
        self._lookback_days = lookback_days
        self._processor = processor

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

    async def collect(self, channels: list[dict]) -> dict:
        validate_channels(channels)
        summary = {"channelsCompleted": 0, "postsRead": 0, "postsWritten": 0,
                   "postsUnchanged": 0, "repliesRead": 0}
        for channel in channels:
            scan_started = datetime.now(timezone.utc)
            scan_started_at = scan_started.isoformat()
            start_time = (_parse_timestamp(channel["startTime"], "startTime")
                          if channel.get("startTime") is not None else None)
            key = _channel_key(self._tenant_id, channel)
            previous_checkpoint = await self._store.read("channel-checkpoint", key)
            lookback_cutoff = None
            if previous_checkpoint is not None and self._lookback_days is not None:
                previous_scan = _parse_timestamp(
                    previous_checkpoint.get("last_successful_scan_started_at"),
                    "Checkpoint last_successful_scan_started_at",
                )
                lookback_cutoff = min(
                    previous_scan, scan_started - timedelta(days=self._lookback_days)
                )
            index = await self._store.read_channel_index(key)
            seen_roots = set()
            reached_lookback_cutoff = False
            async for roots in self._pages(channel):
                for root in roots:
                    root_id = root["id"]
                    if root_id in seen_roots:
                        continue
                    seen_roots.add(root_id)
                    if (start_time is not None
                            and _parse_timestamp(root.get("createdDateTime"), "Post createdDateTime") < start_time):
                        continue
                    replies = {}
                    if "replies" in root and not root.get("replies@odata.nextLink"):
                        valid_replies = _validate_messages(
                            root["replies"], root_id, allow_missing_reply_to=True
                        )
                        replies = {reply["id"]: reply for reply in valid_replies}
                    else:
                        async for page in self._pages(channel, root_id):
                            for reply in page:
                                replies[reply["id"]] = reply
                    ordered_replies = sorted(replies.values(), key=lambda reply: (
                        reply.get("createdDateTime") or "", reply["id"]
                    ))
                    if (lookback_cutoff is not None
                            and _thread_activity(root, ordered_replies) < lookback_cutoff):
                        # Graph orders roots by the latest activity in the entire reply chain.
                        reached_lookback_cutoff = True
                        break
                    document_id = hashlib.sha256(f"{key}:{root_id}".encode()).hexdigest()
                    previous = index.get(document_id)
                    content = _thread_content(root, ordered_replies)
                    digest = _content_hash(content)
                    summary["postsRead"] += 1
                    summary["repliesRead"] += len(ordered_replies)
                    processing_is_current = (
                        self._processor is None
                        or self._processor.is_current(previous.get("processing"), digest)
                    ) if previous else False
                    if (previous and previous.get("content_hash") == digest
                            and processing_is_current):
                        summary["postsUnchanged"] += 1
                        continue
                    document = {
                        "id": document_id, "channel_key": key, "tenant_id": self._tenant_id,
                        "team_id": channel["teamId"], "channel_id": channel["channelId"],
                        "post_id": root_id, **content, "content_hash": digest,
                        "collected_at": datetime.now(timezone.utc).isoformat(),
                        "all_reply_pages_read": True,
                    }
                    if self._processor is not None:
                        document["processing"] = await self._processor.process(
                            channel, content["post"], ordered_replies, digest
                        )
                    _validate_document_size(document)
                    await self._store.write(document, previous)
                    summary["postsWritten"] += 1
                if reached_lookback_cutoff:
                    break
            await self._store.write({
                "id": "channel-checkpoint", "channel_key": key, "type": "channel-checkpoint",
                "tenant_id": self._tenant_id, "team_id": channel["teamId"], "channel_id": channel["channelId"],
                "start_time": start_time.isoformat() if start_time is not None else None,
                "lookback_days": self._lookback_days,
                "lookback_cutoff": lookback_cutoff.isoformat() if lookback_cutoff is not None else None,
                "scan_mode": "incremental" if lookback_cutoff is not None else "full",
                "last_successful_scan_started_at": scan_started_at,
                "completed_at": datetime.now(timezone.utc).isoformat(),
            }, previous_checkpoint)
            summary["channelsCompleted"] += 1
        return summary

    async def reprocess(self, channels: list[dict]) -> dict:
        validate_channels(channels)
        if self._processor is None:
            raise RuntimeError("A Teams thread processor is required for reprocessing.")
        summary = {"channelsCompleted": 0, "postsRead": 0, "postsReprocessed": 0}
        for channel in channels:
            key = _channel_key(self._tenant_id, channel)
            documents = await self._store.read_channel_documents(key)
            for previous in documents:
                content = {"post": previous["post"], "replies": previous["replies"]}
                digest = _content_hash(content)
                if previous.get("content_hash") != digest:
                    raise ValueError("Stored Teams thread content does not match its content hash.")
                document = {
                    name: value for name, value in previous.items()
                    if not name.startswith("_")
                }
                document["processing"] = await self._processor.process(
                    channel, content["post"], content["replies"], digest
                )
                _validate_document_size(document)
                await self._store.write(document, previous)
                summary["postsRead"] += 1
                summary["postsReprocessed"] += 1
            summary["channelsCompleted"] += 1
        return summary
