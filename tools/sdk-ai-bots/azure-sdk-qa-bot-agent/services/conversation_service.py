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

    async def save_conversation(self, message: ConversationMessage) -> str:
        """Save a conversation message to the backing store.

        Args:
            message: The conversation message to persist.

        Returns:
            The ID of the saved message.
        """
        if not message.conversation_id or not message.conversation_type:
            raise ValueError("conversation_id and conversation_type are required")
        container = await get_conversation_message_container()
        message_item = ConversationMessageItem(
            **message.model_dump(mode="json"),
            conversation_partition=self._build_message_partition_key(message),
        )
        await container.upsert_item(message_item.model_dump(mode="json"))
        logger.info("Saved conversation message: %s", message.id)
        return message.id
