"""Canonical request protocol shared by the Teams agent and its deployment CLI.

The hosted agent dispatches on this parsed request instead of interpreting free
text, so an unrecognized payload is rejected rather than silently running a
different operation.
"""

from __future__ import annotations

import json

from services.teams_collection_service import parse_timestamp

BACKFILL = "backfill"
SUMMARIZE = "summarize"
OPERATIONS = (BACKFILL, SUMMARIZE)
REQUEST_FIELDS = ("channelId", "operation", "startTime")


def parse_operation(requested) -> dict:
    value = requested
    if isinstance(value, str):
        try:
            value = json.loads(value)
        except ValueError:
            raise ValueError("Expected a JSON operation request.") from None
    if not isinstance(value, dict):
        raise ValueError("Expected a JSON operation request.")
    if set(value) - set(REQUEST_FIELDS):
        raise ValueError(f"Only {', '.join(REQUEST_FIELDS)} are accepted.")
    operation = value.get("operation")
    if operation not in OPERATIONS:
        raise ValueError(f"operation must be one of {', '.join(OPERATIONS)}.")
    request = {"operation": operation}
    channel_id = value.get("channelId")
    if channel_id is not None:
        if not isinstance(channel_id, str) or not channel_id.strip():
            raise ValueError("channelId must be a nonempty string.")
        request["channelId"] = channel_id
    start_time = value.get("startTime")
    if start_time is not None:
        if operation != BACKFILL:
            raise ValueError("startTime applies only to backfill.")
        parse_timestamp(start_time, "startTime")
        request["startTime"] = start_time
    return request


def operation_input(request: dict) -> str:
    return json.dumps(parse_operation(request), sort_keys=True, separators=(",", ":"))
