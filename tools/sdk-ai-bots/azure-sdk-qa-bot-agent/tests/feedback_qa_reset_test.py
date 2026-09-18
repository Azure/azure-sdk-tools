"""Offline tests for the conditional Cosmos patch used by negative feedback."""

from __future__ import annotations

import asyncio
import sys
from datetime import datetime, timezone
from pathlib import Path
from unittest.mock import AsyncMock, patch

import pytest
from azure.cosmos import exceptions

_PROJECT_ROOT = str(Path(__file__).resolve().parent.parent)
if _PROJECT_ROOT not in sys.path:
    sys.path.insert(0, _PROJECT_ROOT)

from models.conversation import BotAnswerVerdict
from models.qa_record import QARecord, QAStatus
from utils import azure_cosmosdb


@pytest.mark.asyncio
async def test_reset_uses_atomic_no_issue_guard_and_clears_only_assessment():
    container = AsyncMock()
    record_id = "teams_channel:19:channel@thread.tacv2;messageid=123"
    before = datetime.now(timezone.utc)
    with patch.object(azure_cosmosdb, "get_qa_records_container", return_value=container):
        assert await azure_cosmosdb.requeue_qa_record_for_analysis(
            record_id=record_id, tenant_id="typespec",
        )
    after = datetime.now(timezone.utc)

    container.patch_item.assert_awaited_once()
    args = container.patch_item.call_args.kwargs
    assert args["item"] == record_id
    assert args["partition_key"] == "typespec"
    # This predicate must be evaluated by Cosmos at write time, not by a
    # client-side read followed by an unconditional upsert. Any feedback
    # object (created/running/remediation/validation) prevents reopening.
    assert args["filter_predicate"] == (
        f"FROM c WHERE c.qa_status = '{QAStatus.finished.value}' "
        f"AND c.verdict = '{BotAnswerVerdict.Correct.value}' "
        "AND (NOT IS_DEFINED(c.feedback) OR IS_NULL(c.feedback))"
    )
    operations = args["patch_operations"]
    assert all(op["op"] == "set" for op in operations)
    changes = {op["path"].removeprefix("/"): op["value"] for op in operations}
    updated_at = datetime.fromisoformat(changes.pop("updated_at"))
    assert before <= updated_at <= after
    assert changes == {
        "qa_status": QAStatus.ongoing.value,
        "verdict": BotAnswerVerdict.Unknown.value,
        "reasoning": None,
        "confidence": None,
        "evaluated_at": None,
    }
    # The reset document remains valid and retains conversation metadata.
    record = QARecord(
        id=record_id, tenant_id="typespec", conversation_id="thread",
        conversation_type="teams_channel", qa_status=QAStatus.finished,
        verdict=BotAnswerVerdict.Correct, reasoning="Previously correct",
        confidence=0.9, evaluated_at=before, first_seen_at=before,
        created_at=before, updated_at=before,
    )
    reopened = QARecord.from_cosmos({**record.to_cosmos(), **changes, "updated_at": updated_at})
    assert reopened.qa_status == QAStatus.ongoing
    assert reopened.feedback is None
    assert reopened.first_seen_at == record.first_seen_at
    container.read_item.assert_not_awaited()
    container.upsert_item.assert_not_awaited()


@pytest.mark.asyncio
@pytest.mark.parametrize("status_code", [404, 412])
async def test_missing_or_ineligible_record_is_noop(status_code):
    container = AsyncMock()
    container.patch_item.side_effect = exceptions.CosmosHttpResponseError(
        status_code=status_code, message="Missing record or predicate not satisfied",
    )
    with patch.object(azure_cosmosdb, "get_qa_records_container", return_value=container):
        assert not await azure_cosmosdb.requeue_qa_record_for_analysis(
            record_id="teams_channel:thread", tenant_id="typespec",
        )
    assert container.patch_item.await_count == 1
    container.upsert_item.assert_not_awaited()


@pytest.mark.asyncio
@pytest.mark.parametrize("status_code", [403, 429, 503])
async def test_unexpected_storage_errors_propagate(status_code):
    container = AsyncMock()
    error = exceptions.CosmosHttpResponseError(status_code=status_code, message="Storage failure")
    container.patch_item.side_effect = error
    with patch.object(azure_cosmosdb, "get_qa_records_container", return_value=container):
        with pytest.raises(exceptions.CosmosHttpResponseError) as caught:
            await azure_cosmosdb.requeue_qa_record_for_analysis(
                record_id="teams_channel:thread", tenant_id="typespec",
            )
    assert caught.value is error


@pytest.mark.asyncio
async def test_duplicate_reset_does_not_retry_after_state_changes():
    container = AsyncMock()
    # Cosmos accepts the first patch; a later patch fails its predicate once
    # the row is ongoing or a worker has moved it into active analysis.
    container.patch_item.side_effect = [
        {}, exceptions.CosmosHttpResponseError(status_code=412, message="State changed"),
    ]
    with patch.object(azure_cosmosdb, "get_qa_records_container", return_value=container):
        results = await asyncio.gather(*(
            azure_cosmosdb.requeue_qa_record_for_analysis(
                record_id="teams_channel:thread", tenant_id="typespec",
            ) for _ in range(2)
        ))
    assert results == [True, False]
    assert container.patch_item.await_count == 2
    container.upsert_item.assert_not_awaited()