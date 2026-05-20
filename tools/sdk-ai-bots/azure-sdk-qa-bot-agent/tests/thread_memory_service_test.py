"""Tests for episode extraction in ThreadMemoryService."""

from __future__ import annotations

import json
import sys
from datetime import datetime, timezone
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import AsyncMock, patch

import pytest

_PROJECT_ROOT = str(Path(__file__).resolve().parent.parent)
if _PROJECT_ROOT not in sys.path:
    sys.path.insert(0, _PROJECT_ROOT)

from models.conversation import ConversationMessage, ConversationType, Role
from models.episode import Episode, EpisodeDocument
from services.thread_memory_service import ThreadMemoryService


def _make_message(
    sender_id: str, sender_name: str, content: str,
    sender_role: Role = Role.User,
) -> ConversationMessage:
    return ConversationMessage(
        id="test-msg",
        channel_id="channel-1",
        sender_role=sender_role,
        sender_id=sender_id,
        sender_name=sender_name,
        content=content,
        created_at=datetime.now(timezone.utc),
        conversation_id="conv-001",
        conversation_type=ConversationType.teams_channel,
    )

# ---------------------------------------------------------------------------
# Fixtures: sample thread messages
# ---------------------------------------------------------------------------

_EXPERT_THREAD = [
    {
        "sender_role": "user",
        "sender_id": "user_alice",
        "sender_name": "Alice",
        "content": "tsp-client fails with 'emitter not found' when I try to generate the Python SDK.",
        "conversation_id": "conv-001",
        "conversation_partition": "teams_channel:conv-001",
    },
    {
        "sender_role": "system",
        "sender_id": "bot_qa",
        "sender_name": "QA Bot",
        "content": "It sounds like your tspconfig.yaml might be missing the Python emitter entry.",
    },
    {
        "sender_role": "user",
        "sender_id": "user_alice",
        "sender_name": "Alice",
        "content": "I checked and the emitter section is empty. What should I add?",
    },
    {
        "sender_role": "user",
        "sender_id": "user_bob_expert",
        "sender_name": "Bob (Expert)",
        "content": "You need to add @azure-tools/typespec-python to the emitters section. "
        "Make sure the version matches your TypeSpec compiler. Then re-run tsp-client generate.",
    },
    {
        "sender_role": "user",
        "sender_id": "user_alice",
        "sender_name": "Alice",
        "content": "That worked! Thanks Bob.",
    },
]

_SHORT_THREAD = [
    {
        "sender_role": "user",
        "sender_id": "user_alice",
        "sender_name": "Alice",
        "content": "Where is the docs link?",
        "conversation_id": "conv-002",
    },
    {
        "sender_role": "system",
        "sender_id": "bot_qa",
        "sender_name": "QA Bot",
        "content": "Here: https://docs.example.com",
    },
]

_NO_EXPERT_THREAD = [
    {
        "sender_role": "user",
        "sender_id": "user_alice",
        "sender_name": "Alice",
        "content": "I have an issue with the SDK.",
        "conversation_id": "conv-003",
    },
    {
        "sender_role": "system",
        "sender_id": "bot_qa",
        "sender_name": "QA Bot",
        "content": "Can you tell me more?",
    },
    {
        "sender_role": "user",
        "sender_id": "user_alice",
        "sender_name": "Alice",
        "content": "Never mind, I figured it out.",
    },
    {
        "sender_role": "system",
        "sender_id": "bot_qa",
        "sender_name": "QA Bot",
        "content": "Glad to hear that!",
    },
]


# ---------------------------------------------------------------------------
# Episode model tests
# ---------------------------------------------------------------------------


class TestEpisodeModel:
    def test_valid_episode(self):
        ep = Episode(
            trigger="tsp-client fails with emitter not found",
            symptoms=["Error: emitter not found"],
            reasoning_chain=["Check tspconfig.yaml", "Add emitter entry"],
            resolution="Add @azure-tools/typespec-python emitter",
            key_insight="Generation failures are usually config issues",
        )
        assert ep.confidence == 1.0
        assert len(ep.reasoning_chain) == 2

    def test_reasoning_chain_too_short(self):
        with pytest.raises(Exception):
            Episode(
                trigger="test",
                resolution="test",
                key_insight="test",
            )

    def test_confidence_bounds(self):
        with pytest.raises(Exception):
            Episode(
                trigger="test",
                reasoning_chain=["step 1", "step 2"],
                resolution="test",
                key_insight="test",
                confidence=1.5,
            )

    def test_episode_document_from_episode(self):
        ep = Episode(
            trigger="Decorator error",
            symptoms=["compile error"],
            reasoning_chain=["Check syntax", "Fix decorator"],
            resolution="Use correct decorator",
            key_insight="Always check TypeSpec docs for decorator syntax",
            confidence=0.9,
        )
        doc = EpisodeDocument.from_episode(
            ep,
            tenant_id="typespec_channel_qa_bot",
            source_thread_id="conv-100",
            embedding=[0.1, 0.2, 0.3],
            message_count=5,
        )
        assert doc.tenant_id == "typespec_channel_qa_bot"
        assert doc.source_thread_id == "conv-100"
        assert doc.embedding == [0.1, 0.2, 0.3]
        assert doc.confidence == 0.9
        assert doc.message_count == 5
        assert doc.id == "episode-typespec_channel_qa_bot-conv-100"

    def test_to_searchable_text(self):
        doc = EpisodeDocument(
            id="episode-test-conv-1",
            tenant_id="test",
            trigger="tsp-client fails",
            symptoms=["error: not found", "no output"],
            reasoning_chain=["step1", "step2"],
            resolution="fix config",
            key_insight="check config first",
            source_thread_id="conv-1",
        )
        text = doc.to_searchable_text()
        assert "tsp-client fails" in text
        assert "error: not found" in text
        assert "no output" in text


# ---------------------------------------------------------------------------
# Quality gate tests
# ---------------------------------------------------------------------------


class TestQualityGate:
    def test_qualifies_expert_thread(self):
        msg = _make_message("user_bob_expert", "Bob (Expert)", _EXPERT_THREAD[3]["content"])
        assert ThreadMemoryService._qualifies_for_episode(msg, _EXPERT_THREAD) is True

    def test_rejects_short_thread(self):
        msg = _make_message("bot_qa", "QA Bot", _SHORT_THREAD[-1]["content"], sender_role=Role.System)
        assert ThreadMemoryService._qualifies_for_episode(msg, _SHORT_THREAD) is False

    def test_rejects_no_expert_thread(self):
        msg = _make_message("bot_qa", "QA Bot", _NO_EXPERT_THREAD[-1]["content"], sender_role=Role.System)
        assert ThreadMemoryService._qualifies_for_episode(msg, _NO_EXPERT_THREAD) is False

    def test_rejects_empty_thread(self):
        msg = _make_message("user_alice", "Alice", "test")
        assert ThreadMemoryService._qualifies_for_episode(msg, []) is False


# ---------------------------------------------------------------------------
# Thread formatting tests
# ---------------------------------------------------------------------------


class TestThreadFormatting:
    def test_format_thread_includes_all_speakers(self):
        formatted = ThreadMemoryService._format_thread(_EXPERT_THREAD)
        assert "[Alice]" in formatted
        assert "[Bot: QA Bot]" in formatted
        assert "[Bob (Expert)]" in formatted

    def test_format_thread_preserves_content(self):
        formatted = ThreadMemoryService._format_thread(_EXPERT_THREAD)
        assert "emitter not found" in formatted
        assert "@azure-tools/typespec-python" in formatted

    def test_format_thread_skips_empty_content(self):
        thread = [{"sender_role": "user", "sender_name": "X", "content": ""}]
        formatted = ThreadMemoryService._format_thread(thread)
        assert formatted == ""


# ---------------------------------------------------------------------------
# LLM response parsing tests
# ---------------------------------------------------------------------------


class TestResponseParsing:
    def test_parse_valid_episode(self):
        raw = json.dumps({
            "trigger": "tsp-client fails",
            "symptoms": ["error msg"],
            "reasoning_chain": ["Check A", "Check B"],
            "resolution": "Fix config",
            "key_insight": "Config is key",
            "confidence": 0.9,
        })
        result = ThreadMemoryService._parse_episode(raw)
        assert result is not None
        assert result.trigger == "tsp-client fails"
        assert result.confidence == 0.9

    def test_parse_null_response(self):
        result = ThreadMemoryService._parse_episode("null")
        assert result is None

    def test_parse_wrapped_episode(self):
        raw = json.dumps({
            "episode": {
                "trigger": "compile error",
                "symptoms": [],
                "reasoning_chain": ["step 1", "step 2"],
                "resolution": "fix it",
                "key_insight": "read docs",
            }
        })
        result = ThreadMemoryService._parse_episode(raw)
        assert result is not None
        assert result.trigger == "compile error"

    def test_parse_invalid_json(self):
        result = ThreadMemoryService._parse_episode("not json at all")
        assert result is None

    def test_parse_invalid_schema(self):
        raw = json.dumps({"trigger": "test"})  # missing required fields
        result = ThreadMemoryService._parse_episode(raw)
        assert result is None


# ---------------------------------------------------------------------------
# Full extraction with mocked LLM
# ---------------------------------------------------------------------------


class TestExtractEpisode:
    @pytest.fixture
    def mock_openai_response(self):
        """A canned chat.completions response."""
        episode_json = json.dumps({
            "trigger": "tsp-client fails with emitter not found",
            "symptoms": ["Error: emitter not found"],
            "reasoning_chain": [
                "Check if tspconfig.yaml exists",
                "Verify emitter section includes Python emitter",
            ],
            "resolution": "Add @azure-tools/typespec-python emitter to tspconfig.yaml",
            "key_insight": "Generation failures are usually config issues",
            "confidence": 0.95,
        })
        choice = SimpleNamespace(message=SimpleNamespace(content=episode_json))
        return SimpleNamespace(choices=[choice])

    @pytest.fixture
    def mock_embedding_response(self):
        data_item = SimpleNamespace(embedding=[0.1] * 1536)
        return SimpleNamespace(data=[data_item])

    @pytest.mark.asyncio
    async def test_call_llm_success(self, mock_openai_response):
        svc = ThreadMemoryService()

        mock_openai = AsyncMock()
        mock_openai.chat.completions.create = AsyncMock(return_value=mock_openai_response)

        with patch("services.thread_memory_service.get_openai_client", return_value=mock_openai), \
             patch("services.thread_memory_service.cfg", return_value="gpt-4.1"):
            formatted = svc._format_thread(_EXPERT_THREAD)
            episode = await svc._call_llm(formatted)

        assert episode is not None
        assert episode.trigger == "tsp-client fails with emitter not found"
        assert episode.confidence == 0.95

    @pytest.mark.asyncio
    async def test_qualifies_rejects_short_thread(self):
        svc = ThreadMemoryService()
        msg = _make_message("bot_qa", "QA Bot", _SHORT_THREAD[-1]["content"], sender_role=Role.System)
        assert not svc._qualifies_for_episode(msg, _SHORT_THREAD)

    @pytest.mark.asyncio
    async def test_call_llm_handles_null_response(self):
        svc = ThreadMemoryService()

        null_choice = SimpleNamespace(message=SimpleNamespace(content="null"))
        null_response = SimpleNamespace(choices=[null_choice])

        mock_openai = AsyncMock()
        mock_openai.chat.completions.create = AsyncMock(return_value=null_response)

        with patch("services.thread_memory_service.get_openai_client", return_value=mock_openai), \
             patch("services.thread_memory_service.cfg", return_value="gpt-4.1"):
            formatted = svc._format_thread(_EXPERT_THREAD)
            episode = await svc._call_llm(formatted)

        assert episode is None
