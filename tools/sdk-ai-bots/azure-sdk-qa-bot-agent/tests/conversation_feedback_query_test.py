"""Offline tests for conversation-scoped explicit feedback queries."""

from types import SimpleNamespace
from unittest.mock import Mock, patch

import pytest

from utils.azure_cosmosdb import query_conversation_feedback


@pytest.mark.asyncio
async def test_feedback_query_is_parameterized_cross_partition_and_returns_all_records():
    rows = [{"user_name": f"User {i}", "reaction": "good"} for i in range(150)]

    async def results():
        for row in rows:
            yield row

    query_items = Mock(return_value=results())
    thread = "channel;messageid=123' OR true"
    conversation_type = "teams_channel' OR true"
    with patch(
        "utils.azure_cosmosdb.get_feedback_container",
        return_value=SimpleNamespace(query_items=query_items),
    ):
        actual = await query_conversation_feedback(thread, conversation_type)

    assert actual == rows
    query_items.assert_called_once()
    args = query_items.call_args.kwargs
    assert "partition_key" not in args
    assert args["parameters"] == [
        {"name": "@conversation_id", "value": thread},
        {"name": "@conversation_type", "value": conversation_type},
    ]
    assert conversation_type not in args["query"] and thread not in args["query"]
    assert "tenant_id" not in args["query"]
    assert "c.conversation_id = @conversation_id" in args["query"]
    assert "c.conversation_type = @conversation_type" in args["query"]
    assert "TOP" not in args["query"]
    assert "LIMIT" not in args["query"]
    assert "ORDER BY c.created_at DESC" in args["query"]
    assert "c.user_name" in args["query"]
    assert "c.id" not in args["query"]
    assert "c.link" not in args["query"]


@pytest.mark.asyncio
@pytest.mark.parametrize("coordinates", [("", "teams_channel"), ("thread", "")])
async def test_feedback_query_requires_all_scope_coordinates(coordinates):
    with patch("utils.azure_cosmosdb.get_feedback_container") as get:
        with pytest.raises(ValueError, match="coordinates"):
            await query_conversation_feedback(*coordinates)
        get.assert_not_awaited()