"""Conversation service.

Manages the mapping between customer conversation identifiers
(the source conversation_id from Teams/Slack) and AI Foundry conversation IDs.

The backing store is Azure Cosmos DB — each document represents one
conversation mapping.
"""

from __future__ import annotations

import logging

logger = logging.getLogger(__name__)

from models.conversation import ConversationMessage, ConversationType

class ConversationService:
    """Persists and retrieves customer-to-agent conversation ID mappings."""

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

        # TODO: query Cosmos DB for the mapping
        #   container.read_item(
        #       item=customer_conversation_id,
        #       partition_key=f"{conversation_type}:{customer_conversation_id}",
        #   )
        return None

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

        # TODO: upsert into Cosmos DB
        #   container.upsert_item({
        #       "id": customer_conversation_id,
        #       "conversation_type": conversation_type,
        #       "mapping_key": f"{conversation_type}:{customer_conversation_id}",
        #       "agent_conversation_id": agent_conversation_id,
        #   })
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
        # TODO: upsert into Cosmos DB
        #   container.upsert_item(message.model_dump(mode="json"))
        logger.info("Saved conversation message: %s", message.id)
        return message.id
