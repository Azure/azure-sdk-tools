"""Offline regression tests for user feedback persistence in Cosmos DB."""

from __future__ import annotations

import asyncio
import json
import sys
from datetime import datetime, timedelta, timezone
from pathlib import Path
from unittest.mock import AsyncMock, patch
from uuid import UUID

import pytest
from pydantic import ValidationError

_PROJECT_ROOT = str(Path(__file__).resolve().parent.parent)
if _PROJECT_ROOT not in sys.path:
    sys.path.insert(0, _PROJECT_ROOT)

from models.conversation import ConversationType
from models.feedback import FeedbackRequest, FeedbackResponse, Reaction
from services.feedback_service import FeedbackService
from utils import azure_cosmosdb


@pytest.fixture(autouse=True)
def reopen_qa_record():
    with patch("services.feedback_service.requeue_qa_record_for_analysis", return_value=False) as reopen:
        yield reopen


@pytest.mark.asyncio
@pytest.mark.parametrize("reaction", list(Reaction))
async def test_process_saves_all_fields_before_qa_reset(reaction, reopen_qa_record):
    req = FeedbackRequest(
        tenant_id="typespec",
        channel_id="19:channel@thread.tacv2",
        conversation_id="19:channel@thread.tacv2;messageid=123456789",
        conversation_type=ConversationType.teams_channel,
        reaction=reaction,
        comment="More details are needed",
        reasons=["Incorrect", "Incomplete"],
        link="https://teams.microsoft.com/l/message/example",
        user_name="Test User",
    )
    original = req.model_dump(mode="json")
    container = AsyncMock()

    async def reopen_record(**kwargs):
        container.create_item.assert_awaited_once()
        return True

    reopen_qa_record.side_effect = reopen_record

    before = datetime.now(timezone.utc)
    with patch("services.feedback_service.get_feedback_container", return_value=container):
        result = await FeedbackService().process(req)

    after = datetime.now(timezone.utc)
    container.create_item.assert_awaited_once()
    document = container.create_item.call_args.kwargs["body"]
    assert UUID(document["id"]).version == 4
    created_at = datetime.fromisoformat(document["created_at"])
    assert created_at.utcoffset() == timedelta(0)
    assert before <= created_at <= after
    assert {k: v for k, v in document.items() if k not in {"id", "created_at"}} == original
    assert json.loads(json.dumps(document)) == document
    assert type(document["reaction"]) is str
    assert document["reasons"] == req.reasons
    assert document["conversation_id"] == "19:channel@thread.tacv2;messageid=123456789"
    assert document["conversation_type"] == "teams_channel"
    assert type(document["conversation_type"]) is str
    assert req.model_dump(mode="json") == original
    assert result.saved is True
    assert result.issue_url is None
    if reaction == Reaction.bad:
        reopen_qa_record.assert_awaited_once_with(
            record_id="teams_channel:19:channel@thread.tacv2;messageid=123456789",
            tenant_id="typespec",
        )
    else:
        reopen_qa_record.assert_not_awaited()


@pytest.mark.asyncio
async def test_default_feedback_and_concurrent_submissions_have_distinct_ids():
    container = AsyncMock()
    req = FeedbackRequest()
    with patch("services.feedback_service.get_feedback_container", return_value=container):
        results = await asyncio.gather(*(FeedbackService().process(req) for _ in range(10)))

    assert all(result == FeedbackResponse(saved=True) for result in results)
    documents = [call.kwargs["body"] for call in container.create_item.await_args_list]
    assert len(documents) == 10
    assert len({doc["id"] for doc in documents}) == 10
    for document in documents:
        assert document["tenant_id"] == "unknown"
        assert document["reaction"] == "unknown"
        assert document["reasons"] == []
        for field in (
            "channel_id", "comment", "link", "user_name",
            "conversation_id", "conversation_type",
        ):
            assert document[field] is None


def test_feedback_request_parses_conversation_type_from_json():
    req = FeedbackRequest.model_validate_json(json.dumps({
        "tenant_id": "typespec",
        "conversation_id": "19:channel@thread.tacv2;messageid=123456789",
        "conversation_type": "teams_channel",
        "reaction": "good",
    }))
    assert req.conversation_type is ConversationType.teams_channel
    assert req.conversation_id == "19:channel@thread.tacv2;messageid=123456789"


def test_feedback_request_rejects_unsupported_conversation_type():
    with pytest.raises(ValidationError):
        FeedbackRequest.model_validate({"conversation_type": "unsupported"})


@pytest.mark.asyncio
@pytest.mark.parametrize("failure_stage", ["container", "write"])
async def test_storage_failure_propagates_without_resetting_qa(failure_stage, reopen_qa_record):
    container = AsyncMock()
    error = RuntimeError("Storage unavailable")
    if failure_stage == "write":
        container.create_item.side_effect = error
    with patch(
        "services.feedback_service.get_feedback_container",
        return_value=container,
        side_effect=error if failure_stage == "container" else None,
    ):
        with pytest.raises(RuntimeError, match="Storage unavailable"):
            await FeedbackService().process(FeedbackRequest(
                reaction=Reaction.bad,
                tenant_id="typespec",
                conversation_id="thread",
                conversation_type=ConversationType.teams_channel,
            ))
    reopen_qa_record.assert_not_awaited()


@pytest.mark.asyncio
@pytest.mark.parametrize("coordinates", [
    {},
    {"conversation_id": "thread"},
    {"conversation_type": "teams_channel"},
    {"conversation_id": "", "conversation_type": "teams_channel"},
])
async def test_bad_feedback_without_coordinates_is_saved_without_reset(coordinates, reopen_qa_record):
    with patch("services.feedback_service.get_feedback_container", return_value=AsyncMock()):
        result = await FeedbackService().process(FeedbackRequest(
            reaction=Reaction.bad, **coordinates,
        ))
    assert result.saved
    reopen_qa_record.assert_not_awaited()


@pytest.mark.asyncio
async def test_reset_failure_propagates_after_feedback_is_saved(reopen_qa_record):
    reopen_qa_record.side_effect = RuntimeError("Reset unavailable")
    container = AsyncMock()
    with patch("services.feedback_service.get_feedback_container", return_value=container):
        with pytest.raises(RuntimeError, match="Reset unavailable"):
            await FeedbackService().process(FeedbackRequest(
                tenant_id="typespec",
                conversation_id="thread",
                conversation_type=ConversationType.teams_channel,
                reaction=Reaction.bad,
            ))
    container.create_item.assert_awaited_once()


@pytest.fixture
def cosmos_cache(monkeypatch):
    for name in (
        "_client", "_mapping_container", "_message_container",
        "_episode_container", "_qa_records_container", "_feedback_container",
    ):
        monkeypatch.setattr(azure_cosmosdb, name, None)
    monkeypatch.setattr(azure_cosmosdb, "_container_lock", asyncio.Lock())


@pytest.mark.asyncio
async def test_feedback_container_is_cached_and_reset_on_close(cosmos_cache, monkeypatch):
    container = AsyncMock()
    client = AsyncMock()
    monkeypatch.setattr(azure_cosmosdb, "_client", client)
    with patch.object(azure_cosmosdb, "_get_container", return_value=container) as get:
        results = await asyncio.gather(*(azure_cosmosdb.get_feedback_container() for _ in range(10)))
        assert all(result is container for result in results)
        get.assert_awaited_once_with(container_name="feedback-records")
        await azure_cosmosdb.close_cosmos_client()
        assert azure_cosmosdb._feedback_container is None
        client.__aexit__.assert_awaited_once_with(None, None, None)
        assert await azure_cosmosdb.get_feedback_container() is container
        assert get.await_count == 2


@pytest.mark.asyncio
async def test_feedback_container_failure_is_not_cached(cosmos_cache):
    container = AsyncMock()
    with patch.object(
        azure_cosmosdb, "_get_container",
        side_effect=[RuntimeError("Container missing"), container],
    ) as get:
        with pytest.raises(RuntimeError, match="Container missing"):
            await azure_cosmosdb.get_feedback_container()
        assert azure_cosmosdb._feedback_container is None
        assert await azure_cosmosdb.get_feedback_container() is container
        assert get.await_count == 2