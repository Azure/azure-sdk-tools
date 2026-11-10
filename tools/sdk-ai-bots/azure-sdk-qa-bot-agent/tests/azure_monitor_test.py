"""Hermetic regression tests for historical trace lookup."""

from datetime import timedelta
from types import SimpleNamespace
from unittest.mock import AsyncMock, patch

import pytest
from azure.monitor.query import LogsQueryResult

from utils.azure_monitor import query_spans_by_trace_id


@pytest.mark.asyncio
@pytest.mark.parametrize("lookback_hours", [None, 24])
async def test_trace_lookup_searches_retained_history_unless_explicitly_bounded(
    lookback_hours,
) -> None:
    columns = [
        "timestamp", "_table", "name", "type", "duration_ms",
        "success", "result_code", "data", "customDimensions",
    ]
    historical_row = [
        "2026-08-10T19:29:41Z", "dependencies", "search_knowledge_base",
        "tool", 10.0, True, "0", "", {},
    ]
    client = SimpleNamespace(
        query_resource=AsyncMock(
            return_value=LogsQueryResult(
                tables=[SimpleNamespace(columns=columns, rows=[historical_row])]
            )
        )
    )

    kwargs = {} if lookback_hours is None else {"lookback_hours": lookback_hours}
    with patch("utils.azure_monitor._get_logs_client", return_value=client):
        spans = await query_spans_by_trace_id(
            "historical-trace", resource_id="insights-resource",
            **kwargs,
        )

    assert spans[0].timestamp == historical_row[0]
    request = client.query_resource.await_args.kwargs
    assert request["timespan"] == (
        None if lookback_hours is None else timedelta(hours=lookback_hours)
    )
    assert "let target = 'historical-trace'" in request["query"]
