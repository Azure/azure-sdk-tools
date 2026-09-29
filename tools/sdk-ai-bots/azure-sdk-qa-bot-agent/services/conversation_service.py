"""Conversation service.

Manages the mapping between customer conversation identifiers
(the source conversation_id from Teams/Slack) and AI Foundry conversation IDs.

The backing store is Azure Cosmos DB — each document represents one
conversation mapping.

Also backfills historical Teams channel posts that are missing from
``conversation-messages`` (see the "Teams channel backfill" section).
"""

from __future__ import annotations

import asyncio
import json
import logging
from collections import OrderedDict
from datetime import datetime, timezone
from html.parser import HTMLParser
from pathlib import Path
from urllib.parse import parse_qs, unquote, urlsplit
from uuid import UUID, uuid4

import httpx
from azure.core import MatchConditions
from azure.cosmos import exceptions

logger = logging.getLogger(__name__)

from config import app_config
from models.conversation import (
    ConversationDocumentType,
    ConversationMappingItem,
    ConversationMessage,
    ConversationMessageAttachment,
    ConversationMessageExtraInfo,
    ConversationMessageImage,
    ConversationMessageItem,
    ConversationType,
    Role,
    TeamsBackfillJob,
    TeamsBackfillRequest,
    TeamsBackfillStatus,
)
from utils.azure_cosmosdb import (
    get_conversation_mapping_container,
    get_conversation_message_container,
)
from utils.azure_credential import get_credential
from utils.background_tasks import BackgroundTaskTracker


class ConversationService:
    """Persists and retrieves customer-to-agent conversation ID mappings."""

    @staticmethod
    def _to_conversation_type_value(
        conversation_type: ConversationType | None,
    ) -> str | None:
        return conversation_type.value if conversation_type else None

    def _build_mapping_key(
        self,
        customer_conversation_id: str,
        conversation_type: ConversationType | None,
    ) -> str:
        conversation_type_value = self._to_conversation_type_value(conversation_type)
        if conversation_type_value:
            return f"{conversation_type_value}:{customer_conversation_id}"
        return customer_conversation_id

    def _build_message_partition_key(self, message: ConversationMessage) -> str:
        conversation_type_value = self._to_conversation_type_value(
            message.conversation_type
        )
        return f"{conversation_type_value}:{message.conversation_id}"

    async def get_agent_conversation_id(
        self,
        customer_conversation_id: str | None,
        conversation_type: ConversationType | None = None,
    ) -> str | None:
        """Get an AI Foundry agent conversation ID from the local store.

        Args:
            customer_conversation_id: The source conversation identifier
                (e.g. Teams conversation ID).
            conversation_type: The source conversation type
                (e.g. teams_channel).

        Returns:
            The AI Foundry conversation ID if found, otherwise ``None``.
        """
        if not customer_conversation_id:
            return None

        container = await get_conversation_mapping_container()
        mapping_key = self._build_mapping_key(
            customer_conversation_id,
            conversation_type,
        )

        try:
            raw = await container.read_item(
                item=customer_conversation_id,
                partition_key=mapping_key,
            )
        except Exception as exc:
            if getattr(exc, "status_code", None) == 404:
                return None
            raise

        return ConversationMappingItem.model_validate(raw).agent_conversation_id

    async def save_agent_conversation_mapping(
        self,
        customer_conversation_id: str | None,
        conversation_type: ConversationType | None,
        agent_conversation_id: str,
    ) -> str | None:
        """Save the mapping relationship in the local store.

        Args:
            customer_conversation_id: The source conversation identifier
                (e.g. Teams conversation ID).
            conversation_type: The source conversation type
                (e.g. teams_channel).
            agent_conversation_id: The AI Foundry conversation ID to persist.

        Returns:
            The saved AI Foundry conversation ID, or ``None`` if input is invalid.
        """
        if not customer_conversation_id:
            return None

        container = await get_conversation_mapping_container()
        conversation_type_value = self._to_conversation_type_value(conversation_type)
        mapping_key = self._build_mapping_key(
            customer_conversation_id,
            conversation_type,
        )

        mapping_item = ConversationMappingItem(
            id=customer_conversation_id,
            customer_conversation_id=customer_conversation_id,
            conversation_type=conversation_type,
            mapping_key=mapping_key,
            agent_conversation_id=agent_conversation_id,
        )

        await container.upsert_item(mapping_item.model_dump(mode="json"))

        logger.info(
            "Saved conversation mapping: %s -> %s",
            customer_conversation_id,
            agent_conversation_id,
        )
        return agent_conversation_id

    async def save_conversation(self, message: ConversationMessage) -> None:
        """Save a conversation message to the backing store."""
        if not message.conversation_id or not message.conversation_type:
            raise ValueError("conversation_id and conversation_type are required")
        logger.info(
            "Saving conversation message: id=%s, conversation_id=%s, type=%s, sender_role=%s",
            message.id,
            message.conversation_id,
            message.conversation_type,
            message.sender_role,
        )
        if (message.conversation_type == ConversationType.teams_channel
                and message.extra_info is not None
                and message.extra_info.images is None):
            # The connector hands over no image list, so derive it from the body
            # exactly as backfill does and live and imported messages match.
            message.extra_info.images = message_images(message.content)
        container = await get_conversation_message_container()
        message_item = ConversationMessageItem(
            **message.model_dump(mode="json"),
            conversation_partition=self._build_message_partition_key(message),
        )
        result = await container.upsert_item(message_item.model_dump(mode="json"))
        logger.info("Saved conversation message: %s", result["id"])
        return

    async def record_should_reply(
        self,
        message_id: str,
        conversation_id: str,
        conversation_type: ConversationType,
        should_reply: bool,
    ) -> None:
        """Record the ``should_reply`` flag on a previously saved message.

        Marks whether a message passed intention recognition (i.e. was a
        question within the bot's scope). This enables computing the bot
        answering rate: (replied messages) / (questions in scope).
        """
        container = await get_conversation_message_container()
        partition_key = f"{conversation_type.value}:{conversation_id}"

        try:
            raw = await container.read_item(
                item=message_id,
                partition_key=partition_key,
            )
        except Exception as exc:
            if getattr(exc, "status_code", None) == 404:
                logger.warning(
                    "Cannot record should_reply: message %s not found in %s",
                    message_id,
                    partition_key,
                )
                return
            raise

        message_item = ConversationMessageItem.model_validate(raw)
        message_item.should_reply = should_reply
        await container.upsert_item(message_item.model_dump(mode="json"))
        logger.info(
            "Recorded should_reply=%s for message %s",
            should_reply,
            message_id,
        )

    async def has_expert_reply(
        self,
        conversation_id: str,
        conversation_type: ConversationType,
        user_id: str,
    ) -> bool:
        """Check whether a non-author user has replied in a conversation thread.

        Queries saved messages for the given conversation and returns ``True``
        if any message was sent by a user other than the original post author
        and the bot itself (assistant role).
        """
        container = await get_conversation_message_container()
        partition_key = f"{conversation_type.value}:{conversation_id}"

        query = (
            "SELECT c.sender_id, c.sender_role FROM c "
            "WHERE c.conversation_partition = @partition "
            "AND c.sender_role = 'user' "
            "AND c.sender_id != @user_id"
        )
        parameters: list[dict[str, object]] = [
            {"name": "@partition", "value": partition_key},
            {"name": "@user_id", "value": user_id},
        ]

        async for _ in container.query_items(
            query=query,
            parameters=parameters,
            partition_key=partition_key,
            max_item_count=1,
        ):
            return True

        return False

    async def get_thread_messages(
        self, message: ConversationMessage
    ) -> list[ConversationMessageItem]:
        """Retrieve all messages in a thread, ordered by created_at.

        Uses the same partition key logic as ``save_conversation`` to
        locate messages belonging to the same thread/channel.
        """
        container = await get_conversation_message_container()
        partition_key = self._build_message_partition_key(message)

        query = (
            "SELECT * FROM c "
            "WHERE c.conversation_partition = @pk "
            "AND c.document_type = @dtype "
            "ORDER BY c.created_at ASC"
        )
        parameters: list[dict[str, object]] = [
            {"name": "@pk", "value": partition_key},
            {"name": "@dtype", "value": ConversationDocumentType.message.value},
        ]

        items: list[ConversationMessageItem] = []
        async for raw in container.query_items(
            query=query,
            parameters=parameters,
            partition_key=partition_key,
        ):
            items.append(ConversationMessageItem.model_validate(raw))

        logger.info(
            "Retrieved %d thread messages for partition=%s",
            len(items),
            partition_key,
        )
        return items

    async def get_message_by_trace_id(
        self,
        trace_id: str,
    ) -> ConversationMessageItem | None:
        """Look up a bot message by its OTel trace id (cross-partition).

        Enables resolving a conversation from a user-supplied trace id when
        neither the conversation id nor the bot response id is known. The
        returned message carries ``conversation_id``/``conversation_type`` and
        an id shaped ``bot-{response_id}``.
        """
        if not trace_id:
            return None

        container = await get_conversation_message_container()

        query = (
            "SELECT * FROM c "
            "WHERE c.trace_id = @trace_id "
            "AND c.document_type = @dtype "
            "ORDER BY c.created_at DESC"
        )
        parameters: list[dict[str, object]] = [
            {"name": "@trace_id", "value": trace_id},
            {"name": "@dtype", "value": ConversationDocumentType.message.value},
        ]

        async for raw in container.query_items(
            query=query,
            parameters=parameters,
            max_item_count=1,
        ):
            return ConversationMessageItem.model_validate(raw)

        logger.info("No conversation message found for trace_id=%s", trace_id)
        return None

    async def get_messages_by_conversation_id(
        self,
        conversation_id: str,
        conversation_type: ConversationType,
    ) -> list[ConversationMessageItem]:
        """Retrieve all messages for a conversation, ordered by created_at."""
        container = await get_conversation_message_container()
        partition_key = f"{conversation_type.value}:{conversation_id}"

        query = (
            "SELECT * FROM c "
            "WHERE c.conversation_partition = @pk "
            "AND c.document_type = @dtype "
            "ORDER BY c.created_at ASC"
        )
        parameters: list[dict[str, object]] = [
            {"name": "@pk", "value": partition_key},
            {"name": "@dtype", "value": ConversationDocumentType.message.value},
        ]

        items: list[ConversationMessageItem] = []
        async for raw in container.query_items(
            query=query,
            parameters=parameters,
            partition_key=partition_key,
        ):
            items.append(ConversationMessageItem.model_validate(raw))

        return items


    async def get_messages_in_period(
        self,
        start: datetime,
        end: datetime,
    ) -> list[ConversationMessageItem]:
        """Retrieve all messages of conversations *active* in the window.

        The lower bound is normalized to the **start of the day** (00:00:00) of
        ``start``. A conversation qualifies when it has at least one *bot*
        message (system/assistant) whose ``created_at`` falls within
        ``[start_of_day, end)`` — regardless of when the conversation started.
        When it qualifies, **all** of its messages are returned — including
        earlier messages before ``start`` and later replies after ``end`` — so
        the full thread can be evaluated.

        Runs cross-partition queries over the message container. Intended for
        offline/batch jobs (e.g. answer-quality evaluation), not the hot path.

        Args:
            start: Lower bound; normalized to the start of its day (UTC).
            end: Exclusive upper bound on message activity.

        Returns:
            Messages of qualifying conversations, ordered by ``created_at``.
        """
        # Normalize aware datetimes to UTC before flooring/comparison. Stored
        # ``created_at`` values are UTC ISO strings, so a non-UTC bound would
        # otherwise select the wrong window. Naive datetimes are assumed UTC.
        if start.tzinfo is not None:
            start = start.astimezone(timezone.utc)
        if end.tzinfo is not None:
            end = end.astimezone(timezone.utc)
        start = start.replace(hour=0, minute=0, second=0, microsecond=0)
        container = await get_conversation_message_container()

        start_iso = start.isoformat()
        end_iso = end.isoformat()

        # Cosmos' gateway cannot serve GROUP BY / aggregate cross-partition
        # queries, so the qualifying conversations are derived client-side
        # using simple projection queries.
        #
        # A conversation is "active" in the window when it has at least one
        # *bot* message (system/assistant) in [start, end). Its full thread is
        # then fetched.

        # Step 1: partitions that have a bot message inside the window.
        bot_roles = [Role.System.value, Role.Assistant.value]
        window_query = (
            "SELECT c.conversation_partition AS partition FROM c "
            "WHERE c.document_type = @dtype "
            "AND ARRAY_CONTAINS(@bot_roles, c.sender_role) "
            "AND c.created_at >= @start AND c.created_at < @end"
        )
        window_params: list[dict[str, object]] = [
            {"name": "@dtype", "value": ConversationDocumentType.message.value},
            {"name": "@bot_roles", "value": bot_roles},
            {"name": "@start", "value": start_iso},
            {"name": "@end", "value": end_iso},
        ]

        candidate_partitions: set[str] = set()
        async for row in container.query_items(
            query=window_query,
            parameters=window_params,
        ):
            partition = row.get("partition")
            if partition:
                candidate_partitions.add(partition)

        if not candidate_partitions:
            logger.info(
                "No conversations had a bot message in period [%s, %s)",
                start_iso,
                end_iso,
            )
            return []

        partitions = sorted(candidate_partitions)

        # Step 2: fetch all messages for the qualifying conversations.
        messages_query = (
            "SELECT * FROM c "
            "WHERE c.document_type = @dtype "
            "AND ARRAY_CONTAINS(@partitions, c.conversation_partition)"
        )
        messages_params: list[dict[str, object]] = [
            {"name": "@dtype", "value": ConversationDocumentType.message.value},
            {"name": "@partitions", "value": partitions},
        ]

        items: list[ConversationMessageItem] = []
        async for raw in container.query_items(
            query=messages_query,
            parameters=messages_params,
        ):
            items.append(ConversationMessageItem.model_validate(raw))

        items.sort(key=lambda m: m.created_at)

        logger.info(
            "Retrieved %d messages from %d conversations active in [%s, %s)",
            len(items),
            len(partitions),
            start_iso,
            end_iso,
        )
        return items


# ---------------------------------------------------------------------------
# Teams channel backfill
#
# Imports historical Teams channel posts that are missing from
# ``conversation-messages``. One document is written per Teams message, in
# exactly the shape the realtime bot Logic App writes through
# ``save_conversation``, so a backfilled thread and a live thread are
# indistinguishable to every downstream reader. Pages are read through the
# dedicated collection Logic App; writes only add missing messages or refresh
# edited content, attachments and images, never remove anything.
#
# This section also owns the shape a stored Teams body has, so
# ``split_subject`` lives beside the ``title:`` line ``normalized_content``
# writes even though only summarization reads it back.
# ---------------------------------------------------------------------------

_TEAMS_CONVERSATION_TYPE = ConversationType.teams_channel.value
_TEAMS_COLLECTION_CONFIG_PATH = (
    Path(__file__).resolve().parents[1] / "config" / "teams_collection_config.json"
)
_MAX_TRACKED_BACKFILL_JOBS = 50

_FORWARDED_CONTENT_TYPE = "forwardedMessageReference"
_TITLE_PREFIX = "title: "
_HOSTED_CONTENTS_SEGMENT = "/hostedContents/"

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


def teams_conversation_id(channel_id: str, root_id: str) -> str:
    return f"{channel_id};messageid={root_id}"


def thread_partition(channel_id: str, root_id: str) -> str:
    return f"{_TEAMS_CONVERSATION_TYPE}:{teams_conversation_id(channel_id, root_id)}"


def channel_partition_prefix(channel_id: str) -> str:
    """Prefix shared by every thread partition of one channel.

    Keeping ``;messageid=`` in the prefix stops a channel ID from matching
    another channel whose ID merely starts with the same characters.
    """
    return f"{_TEAMS_CONVERSATION_TYPE}:{channel_id};messageid="


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


class _ImageTags(HTMLParser):
    """Collect the attributes of every non-emoji ``<img>`` in a Teams body."""

    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.images: list[dict[str, str | None]] = []

    def handle_starttag(self, tag, attrs):
        if tag != "img":
            return
        values = dict(attrs)
        if values.get("src") and "emoji" not in (values.get("itemtype") or "").lower():
            self.images.append(values)


def message_images(content: str | None) -> list[ConversationMessageImage]:
    """Images embedded in a Teams message body.

    The Teams connector, like Graph, returns no image list for a message: a
    pasted image only appears as an ``<img>`` in ``body.content`` whose ``src``
    points at the message's ``hostedContents``. Lift those out so a stored
    message exposes its images without HTML parsing. Emoji are skipped and a
    repeated source is kept once.
    """
    parser = _ImageTags()
    parser.feed(content or "")
    parser.close()
    images: list[ConversationMessageImage] = []
    seen: set[str] = set()
    for values in parser.images:
        source = values["src"]
        if source in seen:
            continue
        seen.add(source)
        path = unquote(urlsplit(source).path)
        hosted_id = None
        if _HOSTED_CONTENTS_SEGMENT in path:
            hosted_id = path.split(_HOSTED_CONTENTS_SEGMENT, 1)[1].split("/", 1)[0]
        images.append(ConversationMessageImage(
            id=hosted_id or values.get("itemid"), contentUrl=source,
        ))
    return images


def message_attachments(attachments: object) -> list[dict]:
    """Attachments exactly as the Teams connector returns them.

    Validating through ``ConversationMessageAttachment`` keeps only the
    connector's attachment fields, so a live message and a backfilled one store
    the same keys and compare equal.
    """
    if not isinstance(attachments, list):
        return []
    return [
        ConversationMessageAttachment.model_validate(attachment).model_dump(mode="json")
        for attachment in attachments
        if isinstance(attachment, dict)
    ]


def _teams_message_fields(message: dict) -> dict:
    """The parts of a Teams message that backfill writes and keeps current."""
    content = normalized_content(message)
    return {
        "content": content,
        "attachments": message_attachments(message.get("attachments")),
        "images": [image.model_dump(mode="json") for image in message_images(content)],
    }


def _stored_message_fields(document: dict) -> dict:
    """The same parts read back from a stored document, for comparison."""
    extra_info = document.get("extra_info")
    extra_info = extra_info if isinstance(extra_info, dict) else {}
    images = extra_info.get("images")
    return {
        "content": document.get("content"),
        "attachments": message_attachments(extra_info.get("attachments")),
        "images": [
            ConversationMessageImage.model_validate(image).model_dump(mode="json")
            for image in (images if isinstance(images, list) else [])
            if isinstance(image, dict)
        ],
    }


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

    Mirrors ``Build_Conversation_Save_Request_Body`` in the realtime workflow,
    followed by the image extraction ``save_conversation`` applies.
    """
    sender_role, sender_id, sender_name = _sender(message)
    fields = _teams_message_fields(message)
    item = ConversationMessageItem(
        id=message["id"],
        tenant_id=channel["tenantKey"],
        sender_role=sender_role,
        sender_id=sender_id,
        sender_name=sender_name,
        content=fields["content"],
        created_at=parse_timestamp(
            message.get("createdDateTime"), "Message createdDateTime"
        ),
        conversation_id=teams_conversation_id(channel["channelId"], root_id),
        conversation_type=ConversationType.teams_channel,
        conversation_partition=thread_partition(channel["channelId"], root_id),
        extra_info=ConversationMessageExtraInfo(
            channel_id=channel["channelId"],
            message_link=message.get("webUrl"),
            attachments=fields["attachments"],
            images=fields["images"],
        ),
    )
    # Drop only unset top-level fields; attachments keep the connector's nulls.
    return item.model_dump(
        mode="json", exclude={name for name, value in item if value is None}
    )


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
            fields = _teams_message_fields(message)
            if _stored_message_fields(existing) == fields:
                summary["messagesUnchanged"] += 1
                continue
            updated = await self._store.update_message(message["id"], partition, fields)
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
                    self._url, json=payload, follow_redirects=False, timeout=180,
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
            if result["operation"] != payload["operation"]:
                raise ValueError
            return result["data"]
        except (ValueError, KeyError, TypeError):
            raise ValueError("Logic App returned an invalid collection response.") from None


class CosmosMessageStore:
    """Add missing messages and refresh edited ones, never remove anything.

    A refresh touches only content, attachments and images. Backfill shares
    ``conversation-messages`` with the live bot, so a write
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
            query="SELECT c.id, c.content, c.extra_info FROM c WHERE c.document_type = @dtype",
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

    async def update_message(self, document_id, partition, fields):
        """Refresh content, attachments and images; leave every other field alone."""
        try:
            existing = await self._container.read_item(item=document_id, partition_key=partition)
        except exceptions.CosmosResourceNotFoundError:
            return False
        if _stored_message_fields(existing) == fields:
            return False
        extra_info = existing.get("extra_info")
        extra_info = dict(extra_info) if isinstance(extra_info, dict) else {}
        extra_info["attachments"] = fields["attachments"]
        extra_info["images"] = fields["images"]
        existing["content"] = fields["content"]
        existing["extra_info"] = extra_info
        try:
            await self._container.replace_item(
                item=document_id, body=existing, etag=existing["_etag"],
                match_condition=MatchConditions.IfNotModified,
            )
        except exceptions.CosmosAccessConditionFailedError:
            return False
        return True


async def _message_store():
    store = CosmosMessageStore(await get_conversation_message_container())
    await store.validate()
    return store


async def backfill_configured_channels(config, settings, channel_id=None, start_time=None):
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


class BackfillInProgressError(RuntimeError):
    """Raised when a backfill is requested while another one is still running."""


class TeamsBackfillService:
    """Run the Teams channel backfill as a background job on the backend server.

    A run walks entire channels and can take many minutes, far longer than a
    request may wait, so callers receive a job id immediately and poll for the
    outcome. Only one run is accepted at a time because concurrent runs would
    page the same channels into the same shared container for no gain.
    """

    def __init__(self, config_path: Path = _TEAMS_COLLECTION_CONFIG_PATH,
                 runner=backfill_configured_channels):
        self._config_path = config_path
        self._runner = runner
        self._jobs: OrderedDict[str, TeamsBackfillJob] = OrderedDict()
        self._running: str | None = None
        self._lock = asyncio.Lock()

    async def start(self, request: TeamsBackfillRequest) -> TeamsBackfillJob:
        """Accept or refuse a run before a single Teams page is fetched."""
        config = json.loads(self._config_path.read_text(encoding="utf-8"))
        select_channels(config["channels"], request.channel_id)
        if request.start_time is not None:
            parse_timestamp(request.start_time, "start_time")
        async with self._lock:
            if self._running is not None:
                raise BackfillInProgressError(
                    "A Teams backfill is already running; wait for it to finish."
                )
            job = TeamsBackfillJob(
                job_id=str(uuid4()),
                status=TeamsBackfillStatus.running,
                channel_id=request.channel_id,
                start_time=request.start_time,
                started_at=datetime.now(timezone.utc),
            )
            self._jobs[job.job_id] = job
            while len(self._jobs) > _MAX_TRACKED_BACKFILL_JOBS:
                self._jobs.popitem(last=False)
            self._running = job.job_id
        BackgroundTaskTracker.instance().track(
            asyncio.create_task(self._run(job, config, request))
        )
        return job

    def get(self, job_id: str) -> TeamsBackfillJob | None:
        """Return the live job state, or ``None`` once it has aged out."""
        return self._jobs.get(job_id)

    async def _run(
        self, job: TeamsBackfillJob, config: dict, request: TeamsBackfillRequest
    ) -> None:
        try:
            job.summary = await self._runner(
                config, app_config.get, request.channel_id, request.start_time
            )
            job.status = TeamsBackfillStatus.succeeded
        except asyncio.CancelledError:
            job.status = TeamsBackfillStatus.cancelled
            raise
        except Exception:
            logger.exception("Teams backfill job %s failed.", job.job_id)
            job.status = TeamsBackfillStatus.failed
            job.error = "Teams backfill failed; inspect the server logs."
        finally:
            job.completed_at = datetime.now(timezone.utc)
            self._running = None
