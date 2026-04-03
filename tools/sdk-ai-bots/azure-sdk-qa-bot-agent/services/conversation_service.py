"""Conversation service.

Manages the mapping between customer conversation identifiers
(the source conversation_id from Teams/Slack) and AI Foundry conversation IDs.

The backing store is Azure Cosmos DB — each document represents one
conversation mapping.
"""

from __future__ import annotations

import logging

logger = logging.getLogger(__name__)

from models.conversation import (
    ConversationDocumentType,
    ConversationMappingItem,
    ConversationMessage,
    ConversationMessageItem,
    ConversationPartitionPrefix,
    ConversationType,
)
from utils.azure_cosmosdb import (
    get_conversation_mapping_container,
    get_conversation_message_container,
)


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
            conversation_type=conversation_type_value,
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
            message.id, message.conversation_id, message.conversation_type, message.sender_role,
        )
        container = await get_conversation_message_container()
        message_item = ConversationMessageItem(
            **message.model_dump(mode="json"),
            conversation_partition=self._build_message_partition_key(message),
        )
        result = await container.upsert_item(message_item.model_dump(mode="json"))
        logger.info("Saved conversation message: %s", result["id"])
        return

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
        parameters = [
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
        parameters = [
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
