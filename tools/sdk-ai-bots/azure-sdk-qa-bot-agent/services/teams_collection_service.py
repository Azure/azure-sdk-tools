"""Collect complete Teams threads through a dedicated Logic App, without bot side effects."""

from __future__ import annotations

import hashlib
import json
from datetime import datetime, timezone
from urllib.parse import parse_qs, unquote, urlsplit
from uuid import UUID


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


def _validate_messages(messages, message_id=None):
    if not isinstance(messages, list):
        raise ValueError("Teams connector did not return a message list.")
    for message in messages:
        if not isinstance(message, dict) or not isinstance(message.get("id"), str) or not message["id"]:
            raise ValueError("Teams connector returned a message without an ID.")
        if message_id is not None and message.get("replyToId") != message_id:
            raise ValueError("Reply does not belong to the requested thread.")


class TeamsCollectionService:
    def __init__(self, fetch_page, store, tenant_id: str, max_pages: int = 1000):
        UUID(tenant_id)
        if not 1 <= max_pages <= 1000:
            raise ValueError("max_pages must be between 1 and 1000.")
        self._fetch_page = fetch_page
        self._store = store
        self._tenant_id = tenant_id
        self._max_pages = max_pages

    async def _pages(self, channel: dict, message_id: str | None = None):
        token = None
        seen = set()
        for _page_index in range(self._max_pages):
            page = await self._fetch_page(channel, message_id, token)
            if not isinstance(page, dict):
                raise ValueError("Teams connector did not return a message list.")
            _validate_messages(page.get("value"), message_id)
            yield page["value"]
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
            scan_started_at = datetime.now(timezone.utc).isoformat()
            start_time = (_parse_timestamp(channel["startTime"], "startTime")
                          if channel.get("startTime") is not None else None)
            key = hashlib.sha256(json.dumps(
                [self._tenant_id, channel["teamId"], channel["channelId"]]
            ).encode()).hexdigest()
            previous_checkpoint = await self._store.read("channel-checkpoint", key)
            index = await self._store.read_channel_index(key)
            seen_roots = set()
            async for roots in self._pages(channel):
                for root in roots:
                    root_id = root["id"]
                    if root_id in seen_roots:
                        continue
                    seen_roots.add(root_id)
                    if (start_time is not None
                            and _parse_timestamp(root.get("createdDateTime"), "Post createdDateTime") < start_time):
                        continue
                    document_id = hashlib.sha256(f"{key}:{root_id}".encode()).hexdigest()
                    previous = index.get(document_id)
                    replies = {}
                    if "replies" in root and not root.get("replies@odata.nextLink"):
                        _validate_messages(root["replies"], root_id)
                        replies = {reply["id"]: reply for reply in root["replies"]}
                    else:
                        async for page in self._pages(channel, root_id):
                            for reply in page:
                                replies[reply["id"]] = reply
                    post = {name: value for name, value in root.items()
                            if name not in ("replies", "replies@odata.nextLink", "replies@odata.count")}
                    ordered_replies = sorted(replies.values(), key=lambda reply: (
                        reply.get("createdDateTime") or "", reply["id"]
                    ))
                    content = {"post": post, "replies": ordered_replies}
                    digest = hashlib.sha256(json.dumps(
                        content, sort_keys=True, ensure_ascii=False, separators=(",", ":")
                    ).encode("utf-8")).hexdigest()
                    summary["postsRead"] += 1
                    summary["repliesRead"] += len(ordered_replies)
                    if previous and previous.get("content_hash") == digest:
                        summary["postsUnchanged"] += 1
                        continue
                    document = {
                        "id": document_id, "channel_key": key, "tenant_id": self._tenant_id,
                        "team_id": channel["teamId"], "channel_id": channel["channelId"],
                        "post_id": root_id, **content, "content_hash": digest,
                        "collected_at": datetime.now(timezone.utc).isoformat(),
                        "all_reply_pages_read": True,
                    }
                    if len(json.dumps(document, ensure_ascii=False).encode("utf-8")) > 1_900_000:
                        raise ValueError("Thread exceeds the Cosmos document size budget; no partial write was made.")
                    await self._store.write(document, previous)
                    summary["postsWritten"] += 1
            await self._store.write({
                "id": "channel-checkpoint", "channel_key": key, "type": "channel-checkpoint",
                "tenant_id": self._tenant_id, "team_id": channel["teamId"], "channel_id": channel["channelId"],
                "start_time": start_time.isoformat() if start_time is not None else None,
                "last_successful_scan_started_at": scan_started_at,
                "completed_at": datetime.now(timezone.utc).isoformat(),
            }, previous_checkpoint)
            summary["channelsCompleted"] += 1
        return summary