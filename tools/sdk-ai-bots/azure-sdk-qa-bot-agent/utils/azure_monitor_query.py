"""App Insights KQL helper for fetching chat-agent spans by ``response_id``.

The chat agent emits hosted-agent spans with ``response_id`` as a custom
attribute (set by ``FoundryAgentSpanEnricher`` — see
``utils/azure_ai_foundry.py``). To reconstruct what happened on a given
turn, we query App Insights for all spans (``dependencies`` / ``requests``
/ ``traces``) carrying that ``response_id``.

The Application Insights resource is selected via the
``APPLICATIONINSIGHTS_RESOURCE_ID`` env var (an ARM resource id of the
form ``/subscriptions/<sub>/resourceGroups/<rg>/providers/microsoft.insights/components/<name>``).
This is the resource id that ``azure-monitor-query`` expects.
"""

from __future__ import annotations

import logging
import os
from dataclasses import dataclass
from datetime import timedelta
from typing import Any

from azure.monitor.query.aio import LogsQueryClient

from utils.azure_credential import get_credential

logger = logging.getLogger(__name__)

_RESOURCE_ID_ENV = "APPLICATIONINSIGHTS_RESOURCE_ID"
# How far back to search for spans matching a response_id.
_DEFAULT_LOOKBACK_HOURS = 24

_client: LogsQueryClient | None = None


def _get_client() -> LogsQueryClient:
    global _client
    if _client is None:
        _client = LogsQueryClient(get_credential())
    return _client


def _get_resource_id() -> str:
    rid = os.environ.get(_RESOURCE_ID_ENV, "").strip()
    if not rid:
        raise RuntimeError(
            f"{_RESOURCE_ID_ENV} is not set; cannot query App Insights for "
            "chat-agent traces."
        )
    return rid


@dataclass
class TraceSpan:
    """Normalized App Insights row for downstream agent consumption."""

    timestamp: str
    table: str  # "dependencies" | "requests" | "traces"
    name: str
    type: str
    duration_ms: float | None
    success: bool | None
    result_code: str | None
    data: str | None
    custom_dimensions: dict[str, Any]


async def query_spans_by_response_id(
    response_id: str,
    *,
    lookback_hours: int = _DEFAULT_LOOKBACK_HOURS,
) -> list[TraceSpan]:
    """Return all App Insights rows tagged with ``response_id``.

    Searches ``dependencies``, ``requests`` and ``traces`` so the caller can
    reconstruct tool calls, the inbound HTTP request, and any log records.
    Returns an empty list if nothing is found (e.g. ingestion lag).
    """
    if not response_id:
        return []

    resource_id = _get_resource_id()
    client = _get_client()

    # union dep / req / trace, filter by custom dim response_id.
    query = """
let lookback = timespan;
let target = '@response_id';
union
    (dependencies
        | where timestamp > ago(lookback)
        | where tostring(customDimensions["response_id"]) == target
        | extend _table = "dependencies"),
    (requests
        | where timestamp > ago(lookback)
        | where tostring(customDimensions["response_id"]) == target
        | extend _table = "requests"),
    (traces
        | where timestamp > ago(lookback)
        | where tostring(customDimensions["response_id"]) == target
        | extend _table = "traces")
| order by timestamp asc
| project timestamp,
          _table,
          name = coalesce(tostring(name), tostring(message), ""),
          type = coalesce(tostring(type), tostring(itemType), ""),
          duration_ms = todouble(duration),
          success = tobool(success),
          result_code = tostring(resultCode),
          data = tostring(data),
          customDimensions
""".replace("@response_id", response_id.replace("'", "''"))

    try:
        result = await client.query_resource(
            resource_id=resource_id,
            query=query,
            timespan=timedelta(hours=lookback_hours),
        )
    except Exception:
        logger.exception("App Insights query failed for response_id=%s", response_id)
        return []

    spans: list[TraceSpan] = []
    for table in result.tables:
        col_idx = {col: i for i, col in enumerate(table.columns)}
        for row in table.rows:
            spans.append(
                TraceSpan(
                    timestamp=str(row[col_idx["timestamp"]]),
                    table=str(row[col_idx["_table"]]),
                    name=str(row[col_idx["name"]] or ""),
                    type=str(row[col_idx["type"]] or ""),
                    duration_ms=_as_float(row[col_idx["duration_ms"]]),
                    success=_as_bool(row[col_idx["success"]]),
                    result_code=_as_str_or_none(row[col_idx["result_code"]]),
                    data=_as_str_or_none(row[col_idx["data"]]),
                    custom_dimensions=_as_dict(row[col_idx["customDimensions"]]),
                )
            )
    return spans


def _as_float(v: Any) -> float | None:
    try:
        return float(v) if v is not None else None
    except (TypeError, ValueError):
        return None


def _as_bool(v: Any) -> bool | None:
    if v is None:
        return None
    if isinstance(v, bool):
        return v
    s = str(v).lower()
    if s in {"true", "1"}:
        return True
    if s in {"false", "0"}:
        return False
    return None


def _as_str_or_none(v: Any) -> str | None:
    if v is None:
        return None
    s = str(v)
    return s or None


def _as_dict(v: Any) -> dict[str, Any]:
    if isinstance(v, dict):
        return v
    if v is None:
        return {}
    # App Insights returns customDimensions as JSON; fall back to string.
    try:
        import json

        return json.loads(v) if isinstance(v, str) else dict(v)
    except Exception:
        return {}
