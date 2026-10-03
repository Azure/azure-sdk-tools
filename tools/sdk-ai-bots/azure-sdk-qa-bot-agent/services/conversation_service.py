"""Conversation service.

Manages the mapping between customer conversation identifiers
(the source conversation_id from Teams/Slack) and AI Foundry conversation IDs.

The backing store is Azure Cosmos DB — each document represents one
conversation mapping.
"""

from __future__ import annotations

import logging
from collections.abc import Callable
from datetime import datetime, timezone

from azure.core import MatchConditions
from azure.core.exceptions import AzureError
from azure.cosmos.exceptions import CosmosHttpResponseError, CosmosResourceExistsError

logger = logging.getLogger(__name__)

from models.conversation import (
    ConversationDocumentType,
    ConversationMappingItem,
    ConversationMessage,
    ConversationMessageItem,
    ConversationType,
    ExpertNotificationAttempt,
    Role,
)
from utils.azure_cosmosdb import (
    get_conversation_mapping_container,
    get_conversation_message_container,
)


class ConversationService:
    """Persists and retrieves customer-to-agent conversation ID mappings."""

    _MAX_WRITE_ATTEMPTS = 3

    async def _write_message_document(
        self,
        message_id: str,
        partition_key: str,
        update: Callable[[dict], bool],
        *,
        create_body: dict | None = None,
    ) -> bool:
        """Conditionally write a Cosmos message document, preserving raw fields.

        Apply the callback to an existing document, or create a missing one
        only when create_body is supplied. Retry known concurrency conflicts
        only; failed or ambiguous writes return False.
        """
        try:
            container = await get_conversation_message_container()
        except (AzureError, TimeoutError, RuntimeError):
            logger.exception(
                "Cannot access message storage; skipping update: message_id=%s, partition_key=%s",
                message_id, partition_key,
            )
            return False

        try:
            for _ in range(self._MAX_WRITE_ATTEMPTS):
                try:
                    raw = await container.read_item(
                        item=message_id, partition_key=partition_key
                    )
                except CosmosHttpResponseError as exc:
                    if exc.status_code != 404:
                        raise
                    if create_body is None:
                        logger.warning(
                            "Cannot update message %s: not found in %s",
                            message_id,
                            partition_key,
                        )
                        return False
                    try:
                        await container.create_item(body=create_body)
                        return True
                    except CosmosHttpResponseError as create_exc:
                        if create_exc.status_code == 409:
                            continue
                        raise

                if raw.get("document_type") != ConversationDocumentType.message.value:
                    logger.warning("Cannot update non-message document %s", message_id)
                    return False
                etag = raw.get("_etag")
                if not etag:
                    logger.warning(
                        "Missing ETag; skipping message update: message_id=%s, partition_key=%s",
                        message_id, partition_key,
                    )
                    return False
                if not update(raw):
                    return False
                try:
                    await container.replace_item(
                        item=message_id,
                        body=raw,
                        etag=etag,
                        match_condition=MatchConditions.IfNotModified,
                    )
                    return True
                except CosmosHttpResponseError as exc:
                    if exc.status_code in (404, 412):
                        continue
                    raise
        except (AzureError, TimeoutError):
            logger.exception(
                "Message storage operation failed; skipping update: message_id=%s, partition_key=%s",
                message_id, partition_key,
            )
            return False

        logger.warning(
            "Concurrent updates exhausted after %s attempts; skipping update: message_id=%s, partition_key=%s",
            self._MAX_WRITE_ATTEMPTS, message_id, partition_key,
        )
        return False

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
        """Get the mapped AI Foundry conversation ID, if present."""
        mapping = await self.get_agent_conversation_mapping(
            customer_conversation_id, conversation_type
        )
        return mapping.agent_conversation_id if mapping else None

    async def get_agent_conversation_mapping(
        self,
        customer_conversation_id: str | None,
        conversation_type: ConversationType | None = None,
    ) -> ConversationMappingItem | None:
        """Get the AI Foundry conversation mapping and pinned response format.

        Args:
            customer_conversation_id: The source conversation identifier
                (e.g. Teams conversation ID).
            conversation_type: The source conversation type
                (e.g. teams_channel).

        Returns:
            The saved mapping if found, otherwise ``None``.
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

        return ConversationMappingItem.model_validate(raw)

    async def save_agent_conversation_mapping(
        self,
        customer_conversation_id: str | None,
        conversation_type: ConversationType | None,
        agent_conversation_id: str,
        *,
        confidence_enabled: bool,
        create_only: bool = False,
    ) -> ConversationMappingItem | None:
        """Save the mapping relationship in the local store.

        Args:
            customer_conversation_id: The source conversation identifier
                (e.g. Teams conversation ID).
            conversation_type: The source conversation type
                (e.g. teams_channel).
            agent_conversation_id: The AI Foundry conversation ID to persist.
            confidence_enabled: Pinned response format; preserve it during recovery.
            create_only: Keep the winning mapping if another request created it first.

        Returns:
            The saved mapping, or ``None`` if input is invalid or a conflicting
            mapping cannot be found.
        """
        if not customer_conversation_id:
            return None

        container = await get_conversation_mapping_container()
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
            confidence_enabled=confidence_enabled,
        )

        if create_only:
            try:
                await container.create_item(body=mapping_item.model_dump(mode="json"))
            except CosmosResourceExistsError:
                return await self.get_agent_conversation_mapping(
                    customer_conversation_id, conversation_type
                )
        else:
            await container.upsert_item(mapping_item.model_dump(mode="json"))

        logger.info(
            "Saved conversation mapping: %s -> %s",
            customer_conversation_id,
            agent_conversation_id,
        )
        return mapping_item

    async def save_conversation(self, message: ConversationMessage) -> bool:
        """Save a conversation message and return whether the write was confirmed."""
        if not message.conversation_id or not message.conversation_type:
            raise ValueError("conversation_id and conversation_type are required")
        logger.info(
            "Saving conversation message: id=%s, conversation_id=%s, type=%s, sender_role=%s",
            message.id,
            message.conversation_id,
            message.conversation_type,
            message.sender_role,
        )
        # Only ingress model fields are accepted, even if a persistence-model
        # instance was passed. Notification attempts are server-owned metadata.
        ingress = message.model_dump(
            mode="json", include=set(ConversationMessage.model_fields)
        )
        message_item = ConversationMessageItem(
            **ingress,
            conversation_partition=self._build_message_partition_key(message),
        )

        def update(raw: dict) -> bool:
            raw.update(
                {
                    key: value
                    for key, value in ingress.items()
                    if key not in ("should_reply", "created_at")
                }
            )
            return True

        saved = await self._write_message_document(
            message.id,
            message_item.conversation_partition,
            update,
            create_body=message_item.model_dump(mode="json"),
        )
        if not saved:
            return False
        logger.info("Saved conversation message: %s", message.id)
        return True

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
        partition_key = f"{conversation_type.value}:{conversation_id}"

        def update(raw: dict) -> bool:
            raw["should_reply"] = should_reply
            return True

        if not await self._write_message_document(message_id, partition_key, update):
            return
        logger.info(
            "Recorded should_reply=%s for message %s",
            should_reply,
            message_id,
        )

    @staticmethod
    def _root_message_id(conversation_id: str) -> str | None:
        channel, separator, root_id = conversation_id.partition(";messageid=")
        if not channel or not separator or not root_id or ";" in root_id:
            logger.warning("Cannot identify root message for %s", conversation_id)
            return None
        return root_id

    async def reserve_expert_notification(
        self,
        conversation_id: str,
        conversation_type: ConversationType,
        response_id: str,
    ) -> bool:
        """Reserve the thread's only notification attempt on its existing root.

        Once reserved, even the same response cannot reserve again. A lost
        completion response or failed Teams send does not release the attempt.
        """
        if not response_id.strip():
            return False
        root_id = self._root_message_id(conversation_id)
        if not root_id:
            return False

        def update(raw: dict) -> bool:
            if raw.get("expert_notification") is not None:
                return False
            raw["expert_notification"] = ExpertNotificationAttempt(
                response_id=response_id,
                reserved_at=datetime.now(timezone.utc),
            ).model_dump(mode="json")
            return True

        return await self._write_message_document(
            root_id, f"{conversation_type.value}:{conversation_id}", update
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
