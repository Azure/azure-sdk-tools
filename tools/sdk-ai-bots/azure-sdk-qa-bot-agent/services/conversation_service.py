"""Conversation service.

Manages the mapping between customer conversation identifiers
(the source conversation_id from Teams/Slack) and AI Foundry conversation IDs.

The backing store is Azure Cosmos DB — each document represents one
conversation mapping.
"""

from __future__ import annotations

import json
import logging
from datetime import datetime, timezone
from pathlib import Path
from typing import Sequence

logger = logging.getLogger(__name__)

from config.app_config import get as cfg
from models.conversation import (
    BotAnswerVerdict,
    ConversationDocumentType,
    ConversationEvaluationItem,
    ConversationMappingItem,
    ConversationMessage,
    ConversationMessageItem,
    ConversationType,
    Role,
)
from utils.azure_ai_foundry import get_project_client
from utils.azure_cosmosdb import (
    get_conversation_mapping_container,
    get_conversation_message_container,
)

_EVAL_PROMPT_PATH = (
    Path(__file__).resolve().parent.parent / "prompts" / "conversation_evaluation.md"
)
_EVAL_TEMPERATURE = 0.0


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

    async def get_messages_by_conversation(
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

    # ------------------------------------------------------------------
    # Conversation quality evaluation
    #
    # These helpers judge a *single* conversation with an LLM. They are
    # conversation-faced only: they take conversation messages and return a
    # verdict. They know nothing about the channel (Teams/Slack), message
    # links, batching, or scheduling — those belong to the caller.
    # ------------------------------------------------------------------

    _eval_prompt: str | None = None

    @staticmethod
    def group_by_conversation(
        messages: Sequence[ConversationMessageItem],
    ) -> dict[str, list[ConversationMessageItem]]:
        """Group messages by their conversation partition, preserving order."""
        conversations: dict[str, list[ConversationMessageItem]] = {}
        for msg in messages:
            conversations.setdefault(msg.conversation_partition, []).append(msg)
        return conversations

    async def evaluate_conversation(
        self,
        messages: Sequence[ConversationMessageItem],
    ) -> ConversationEvaluationItem | None:
        """Judge whether the bot answered a single conversation correctly.

        Builds a labelled transcript from ``messages`` and asks an LLM for a
        ``correct`` / ``incorrect`` / ``unknown`` verdict.

        Args:
            messages: All messages of one conversation, in any order.

        Returns:
            A :class:`ConversationEvaluationItem`, or ``None`` when the
            conversation has no user question or no bot answer to judge.
        """
        context = self._build_transcript(messages)
        if context is None:
            return None

        transcript, has_expert_reply = context

        ordered = sorted(messages, key=lambda m: m.created_at)
        first = ordered[0]

        verdict, reasoning, confidence = await self._evaluate_transcript(transcript)

        return ConversationEvaluationItem(
            conversation_id=first.conversation_id or first.conversation_partition,
            conversation_partition=first.conversation_partition,
            transcript=transcript,
            message_count=len(ordered),
            has_expert_reply=has_expert_reply,
            verdict=verdict,
            reasoning=reasoning,
            confidence=confidence,
            evaluated_at=datetime.now(timezone.utc),
        )

    @staticmethod
    def _build_transcript(
        messages: Sequence[ConversationMessageItem],
    ) -> tuple[str, bool] | None:
        """Build a labelled transcript of the whole conversation.

        The conversation is multi-turn: the original poster asks, the bot
        auto-replies, the poster may ask follow-ups (each answered by the
        bot), and at some point either the poster stops asking or a human
        expert joins — after which the bot no longer auto-replies.

        Each message is labelled so the LLM can tell apart the original
        poster, the bot, and other humans (experts)::

            [User] ...          — the original poster
            [Bot] ...           — an automated bot reply
            [Expert: Name] ...  — a human other than the poster

        Returns ``(transcript, has_expert_reply)`` or ``None`` when the
        conversation has no user question or no bot answer to judge.
        """
        ordered = sorted(messages, key=lambda m: m.created_at)

        question_msg = next(
            (
                m
                for m in ordered
                if m.sender_role == Role.User and (m.content or "").strip()
            ),
            None,
        )
        if question_msg is None:
            return None

        has_bot_answer = any(
            m.sender_role in (Role.System, Role.Assistant) and (m.content or "").strip()
            for m in ordered
        )
        if not has_bot_answer:
            return None

        poster_id = question_msg.sender_id
        has_expert_reply = False
        lines: list[str] = []

        for m in ordered:
            content = (m.content or "").strip()
            if not content:
                continue
            if m.sender_role in (Role.System, Role.Assistant):
                lines.append(f"[Bot]\n{content}")
            elif m.sender_role == Role.User and m.sender_id == poster_id:
                lines.append(f"[User]\n{content}")
            else:
                # A human other than the original poster — an expert.
                has_expert_reply = True
                name = m.sender_name or "expert"
                lines.append(f"[Expert: {name}]\n{content}")

        return "\n\n".join(lines), has_expert_reply

    def _load_eval_prompt(self) -> str:
        prompt = type(self)._eval_prompt
        if prompt is None:
            prompt = _EVAL_PROMPT_PATH.read_text(encoding="utf-8")
            type(self)._eval_prompt = prompt
        return prompt

    async def _evaluate_transcript(
        self, transcript: str
    ) -> tuple[BotAnswerVerdict, str, float]:
        """Call the LLM to judge the bot's answers across the whole thread."""
        model = cfg("AI_FOUNDRY_AGENT_COMPLETION_MODEL", "gpt-5.4")
        openai_client = get_project_client().get_openai_client()

        response = await openai_client.chat.completions.create(
            model=model,
            messages=[
                {"role": "system", "content": self._load_eval_prompt()},
                {"role": "user", "content": transcript},
            ],
            response_format={"type": "json_object"},
            temperature=_EVAL_TEMPERATURE,
        )
        raw = (response.choices[0].message.content or "").strip()
        return self._parse_evaluation(raw)

    @staticmethod
    def _parse_evaluation(raw: str) -> tuple[BotAnswerVerdict, str, float]:
        try:
            data = json.loads(raw)
        except json.JSONDecodeError:
            logger.warning("Evaluation returned invalid JSON: %s", raw[:200])
            return BotAnswerVerdict.Unknown, "Model returned invalid JSON.", 0.0

        try:
            verdict = BotAnswerVerdict(str(data.get("verdict", "")).lower())
        except ValueError:
            verdict = BotAnswerVerdict.Unknown
        reasoning = str(data.get("reasoning", ""))
        try:
            confidence = float(data.get("confidence", 0.0))
        except (TypeError, ValueError):
            confidence = 0.0
        confidence = min(1.0, max(0.0, confidence))
        return verdict, reasoning, confidence
