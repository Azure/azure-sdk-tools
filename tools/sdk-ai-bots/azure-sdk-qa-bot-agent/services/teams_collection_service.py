"""Export Teams history to JSON, with optional create-only Cosmos storage."""

from __future__ import annotations

import asyncio
import json
from collections.abc import AsyncIterator, Awaitable, Callable
from datetime import datetime, timezone
from functools import partial
from pathlib import Path
from typing import Any
from urllib.parse import parse_qs, unquote, urlsplit
from uuid import UUID

import httpx
from azure.core.credentials_async import AsyncTokenCredential
from azure.cosmos import exceptions
from azure.cosmos.aio import ContainerProxy
from pydantic import BaseModel, Field

from models.conversation import (
    ConversationMessageAttachment,
    ConversationMessageExtraInfo,
    ConversationMessageItem,
    ConversationType,
    Role,
)
from utils.azure_cosmosdb import get_conversation_message_container
from utils.azure_credential import get_credential

MAX_HISTORY_PAGES = 1000
_LOGIC_APP_AUDIENCE = "https://management.core.windows.net/"
_FORWARDED_CONTENT_PATHS = (
    ("body", "content"),
    ("message", "body", "content"),
    ("originalMessage", "body", "content"),
    ("content",),
)


class TeamsHistoryRequest(BaseModel):
    team_id: str = Field(min_length=1, max_length=100)
    channel_id: str = Field(min_length=1, max_length=500)
    tenant_id: str | None = Field(default=None, min_length=1, max_length=200)
    start_time: str | None = Field(default=None, max_length=100)
    max_pages: int | None = Field(default=None, ge=1, le=MAX_HISTORY_PAGES)
    save_to_cosmos: bool = False


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


def validate_history_request(request: TeamsHistoryRequest) -> None:
    UUID(request.team_id)
    if not request.channel_id.strip():
        raise ValueError("channel_id must be nonempty.")
    if request.tenant_id is not None and not request.tenant_id.strip():
        raise ValueError("tenant_id must be nonempty when supplied.")
    if request.save_to_cosmos and not request.tenant_id:
        raise ValueError("tenant_id is required when saving to Cosmos DB.")
    if request.start_time is not None:
        parse_timestamp(request.start_time, "start_time")


def extract_skip_token(
    link: str, request: TeamsHistoryRequest, message_id: str | None
) -> str:
    """Validate the source route and extract its token; never follow the URL."""
    address = urlsplit(link)
    expected = f"/teams/{request.team_id}/channels/{request.channel_id}/messages"
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


def _validate_messages(
    messages: Any, message_id: str | None = None, allow_missing_reply_to: bool = False
) -> list[dict[str, Any]]:
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


def _message_activity(message: dict[str, Any]) -> datetime:
    timestamps = [
        parse_timestamp(message[name], name)
        for name in ("createdDateTime", "lastModifiedDateTime")
        if message.get(name) is not None
    ]
    if not timestamps:
        raise ValueError("Message requires createdDateTime or lastModifiedDateTime.")
    return max(timestamps)


def normalize_message_content(message: dict[str, Any]) -> str:
    """Mirror title and forwarded-content normalization in template.chat.json."""
    body = message.get("body") or {}
    content = body.get("content") or ""
    for attachment in message.get("attachments") or []:
        if (attachment.get("contentType") != "forwardedMessageReference"
                or not attachment.get("content")):
            continue
        forwarded = attachment["content"]
        parsed = forwarded
        if isinstance(parsed, str):
            try:
                parsed = json.loads(parsed)
            except ValueError:
                pass
        if isinstance(parsed, dict):
            for path in _FORWARDED_CONTENT_PATHS:
                value = parsed
                for name in path:
                    value = value.get(name) if isinstance(value, dict) else None
                if isinstance(value, str) and value:
                    forwarded = value
                    break
        if isinstance(forwarded, str):
            content = content.replace(
                f'<attachment id="{attachment.get("id")}"></attachment>', forwarded
            )
    subject = message.get("subject")
    if isinstance(subject, str) and subject.strip():
        content = f"title: {subject.strip()}\n\n{content}"
    return content


def build_message_document(
    request: TeamsHistoryRequest, message: dict[str, Any], root_id: str
) -> dict[str, Any]:
    origin = message.get("from") or {}
    application = origin.get("application")
    sender = application if isinstance(application, dict) else origin.get("user") or {}
    conversation_id = f"{request.channel_id};messageid={root_id}"
    item = ConversationMessageItem(
        id=message["id"],
        tenant_id=request.tenant_id,
        sender_role=Role.System if isinstance(application, dict) else Role.User,
        sender_id=sender.get("id") or "",
        sender_name=sender.get("displayName") or "",
        content=normalize_message_content(message),
        created_at=parse_timestamp(message.get("createdDateTime"), "createdDateTime"),
        conversation_id=conversation_id,
        conversation_type=ConversationType.teams_channel,
        conversation_partition=f"{ConversationType.teams_channel.value}:{conversation_id}",
        extra_info=ConversationMessageExtraInfo(
            channel_id=request.channel_id,
            message_link=message.get("webUrl"),
            attachments=[
                ConversationMessageAttachment.model_validate(attachment)
                for attachment in message.get("attachments") or []
            ],
        ),
    )
    return item.model_dump(
        mode="json", exclude={name for name, value in item if value is None}
    )


async def fetch_teams_page(
    client: httpx.AsyncClient, credential, url: str, request: TeamsHistoryRequest,
    message_id: str | None = None, skip_token: str | None = None,
    *, token_scope: str = _LOGIC_APP_AUDIENCE + ".default",
) -> dict[str, Any]:
    """Fetch one bound channel page, validating the URL before sending credentials."""
    address = urlsplit(url)
    if (address.scheme != "https" or not address.hostname
            or not address.hostname.endswith(".logic.azure.com")
            or address.username or address.password or address.port not in (None, 443)
            or address.fragment or set(parse_qs(address.query)) - {"api-version"}
            or not address.path.endswith("/triggers/manual/paths/invoke")):
        raise ValueError("Configure an HTTPS Logic App manual trigger URL without SAS parameters.")
    payload = {
        "teamId": request.team_id,
        "channelId": request.channel_id,
        "operation": "replies" if message_id is not None else "messages",
    }
    if message_id is not None:
        payload["messageId"] = message_id
    if skip_token is not None:
        payload["skipToken"] = skip_token
    token = await credential.get_token(token_scope)
    response = None
    for attempt in range(4):
        try:
            response = await client.post(
                url, json=payload, follow_redirects=False, timeout=180,
                headers={"Authorization": f"Bearer {token.token}"},
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
        if result["operation"] != payload["operation"] or not isinstance(result["data"], dict):
            raise ValueError
        return result["data"]
    except (ValueError, KeyError, TypeError):
        raise ValueError("Logic App returned an invalid collection response.") from None


async def iter_message_pages(
    request: TeamsHistoryRequest,
    fetch_page: Callable[[str | None, str | None], Awaitable[dict[str, Any]]],
    root_message_id: str | None = None,
) -> AsyncIterator[list[dict[str, Any]]]:
    token = None
    seen = set()
    max_pages = MAX_HISTORY_PAGES
    if root_message_id is None and request.max_pages is not None:
        max_pages = request.max_pages
    for _ in range(max_pages):
        page = await fetch_page(root_message_id, token)
        if not isinstance(page, dict):
            raise ValueError("Teams connector did not return a message list.")
        yield _validate_messages(page.get("value"), root_message_id)
        link = page.get("@odata.nextLink")
        if not link:
            return
        token = extract_skip_token(link, request, root_message_id)
        if token in seen:
            raise RuntimeError("Teams connector repeated a continuation token.")
        seen.add(token)
    raise RuntimeError("Page limit reached; collection is incomplete.")


async def fetch_thread_replies(
    request: TeamsHistoryRequest, root: dict[str, Any],
    fetch_page: Callable[[str | None, str | None], Awaitable[dict[str, Any]]],
) -> list[dict[str, Any]]:
    root_id = root["id"]
    replies = {}
    if "replies" in root and not root.get("replies@odata.nextLink"):
        for reply in _validate_messages(root["replies"], root_id, allow_missing_reply_to=True):
            replies[reply["id"]] = reply
    else:
        # A partial expansion is not a full thread; page replies from the start.
        async for page in iter_message_pages(request, fetch_page, root_id):
            for reply in page:
                replies[reply["id"]] = reply
    return sorted(replies.values(), key=lambda reply: (
        reply.get("createdDateTime") or "", reply["id"]
    ))


async def iter_channel_threads(
    request: TeamsHistoryRequest,
    fetch_page: Callable[[str | None, str | None], Awaitable[dict[str, Any]]],
) -> AsyncIterator[tuple[dict[str, Any], list[dict[str, Any]]]]:
    """Yield each selected root post together with its complete replies."""
    validate_history_request(request)
    start_time = (parse_timestamp(request.start_time, "start_time")
                  if request.start_time is not None else None)
    seen_roots = set()
    async for roots in iter_message_pages(request, fetch_page):
        for root in roots:
            if root["id"] in seen_roots:
                continue
            seen_roots.add(root["id"])
            # Fetch all replies before writing so a paging failure leaves no partial thread.
            replies = await fetch_thread_replies(request, root, fetch_page)
            if start_time is not None:
                # Roots are ordered by the latest activity across the thread.
                if max(_message_activity(message) for message in (root, *replies)) < start_time:
                    return
                # The cutoff selects root creation time, not recent replies to old posts.
                if parse_timestamp(root.get("createdDateTime"), "createdDateTime") < start_time:
                    continue
            yield root, replies


async def save_thread_to_cosmos(
    request: TeamsHistoryRequest, root: dict[str, Any], replies: list[dict[str, Any]],
    container: ContainerProxy,
) -> tuple[int, int]:
    created = skipped = 0
    for message in (root, *replies):
        try:
            # Atomic create preserves existing records, including concurrent live writes.
            await container.create_item(body=build_message_document(request, message, root["id"]))
        except exceptions.CosmosResourceExistsError:
            skipped += 1
        else:
            created += 1
    return created, skipped


async def export_history_to_json(
    request: TeamsHistoryRequest,
    fetch_page: Callable[[str | None, str | None], Awaitable[dict[str, Any]]],
    path: Path,
    *, container: ContainerProxy | None = None,
) -> dict[str, int]:
    """Export complete threads, optionally creating missing Cosmos messages."""
    validate_history_request(request)
    if request.save_to_cosmos and container is None:
        raise ValueError("Cosmos storage is required when save_to_cosmos is enabled.")
    summary = {"channelsCompleted": 0, "threadsRead": 0, "messagesRead": 0,
               "messagesExported": 0}
    if request.save_to_cosmos:
        summary.update(messagesCreated=0, messagesSkipped=0)
    # Write one thread at a time; do not retain the entire channel in memory.
    output = path.open("x", encoding="utf-8")
    try:
        with output:
            output.write('{"request": ')
            json.dump(request.model_dump(mode="json"), output, ensure_ascii=False)
            output.write(', "threads": [')
            async for root, replies in iter_channel_threads(request, fetch_page):
                if summary["threadsRead"]:
                    output.write(",")
                post = {key: value for key, value in root.items()
                        if key not in ("replies", "replies@odata.nextLink")}
                json.dump({"post": post, "replies": replies}, output, ensure_ascii=False)
                summary["threadsRead"] += 1
                summary["messagesRead"] += 1 + len(replies)
                if request.save_to_cosmos and container is not None:
                    created, skipped = await save_thread_to_cosmos(request, root, replies, container)
                    summary["messagesCreated"] += created
                    summary["messagesSkipped"] += skipped
            output.write("]}")
    except BaseException:
        # Never leave a partial file that could be mistaken for completed history.
        path.unlink()
        raise
    summary["messagesExported"] = summary["messagesRead"]
    summary["channelsCompleted"] = 1
    return summary


async def collect_channel_history(
    request: TeamsHistoryRequest, output_path: Path, logic_app_url: str,
    *, token_scope: str = _LOGIC_APP_AUDIENCE + ".default",
    credential: AsyncTokenCredential | None = None,
) -> dict[str, int]:
    validate_history_request(request)
    container = None
    if request.save_to_cosmos:
        container = await get_conversation_message_container()
        properties = await container.read()
        if properties.get("partitionKey", {}).get("paths") != ["/conversation_partition"]:
            raise ValueError("Backfill container must use partition key /conversation_partition.")
    async with httpx.AsyncClient() as client:
        fetch = partial(
            fetch_teams_page, client, credential if credential is not None else get_credential(),
            logic_app_url, request,
            token_scope=token_scope,
        )
        return await export_history_to_json(request, fetch, output_path, container=container)
