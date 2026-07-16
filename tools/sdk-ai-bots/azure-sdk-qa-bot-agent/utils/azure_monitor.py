"""Azure Monitor (Application Insights) utilities.

Two complementary concerns live here:

* **Metrics (write path)** — configures OpenTelemetry metrics and exposes
  pre-built instruments that the backend server uses to record custom
  metrics visible in the Azure Portal under Application Insights > Metrics.
* **Log query (read path)** — a KQL helper that fetches spans by
  ``trace_id``. The OTel ``trace_id`` of a turn maps to the App Insights
  ``operation_Id`` column (set automatically by the Azure Monitor
  OpenTelemetry exporter); we query all spans (``dependencies`` /
  ``requests`` / ``traces``) carrying that ``operation_Id`` against a
  caller-supplied Application Insights ARM resource id.
"""

from __future__ import annotations

import logging
import os
from dataclasses import dataclass
from datetime import timedelta
from typing import Any

from azure.monitor.opentelemetry import configure_azure_monitor
from azure.monitor.query.aio import LogsQueryClient
from opentelemetry import metrics as otel_metrics
from opentelemetry.sdk.metrics.view import View

from utils.azure_credential import get_credential

logger = logging.getLogger(__name__)

_meter = None
_chat_request_counter = None
_chat_duration_histogram = None
_initialized = False


def _normalize_tenant_id(tenant: str | None) -> str:
    """Return a stable tenant id value suitable for metric dimensions."""
    value = (tenant or "").strip()
    return value or "unknown"


def configure_metrics() -> None:
    """Set up OpenTelemetry metrics exported to Azure Monitor.

    Requires the ``APPLICATIONINSIGHTS_CONNECTION_STRING`` env var
    (auto-injected by App Service when Application Insights is linked).
    Safe to call more than once — subsequent calls are no-ops.
    """
    global _meter, _chat_request_counter, _chat_duration_histogram, _initialized
    if _initialized:
        return
    _initialized = True

    conn_str = os.environ.get("APPLICATIONINSIGHTS_CONNECTION_STRING")
    if not conn_str:
        logger.info(
            "APPLICATIONINSIGHTS_CONNECTION_STRING not set — custom metrics disabled"
        )
        return

    configure_azure_monitor(
        connection_string=conn_str,
        enable_live_metrics=True,
        views=[
            View(
                instrument_name="chat_requests",
                attribute_keys={"tenant_id"},
            ),
            View(
                instrument_name="chat_duration",
                attribute_keys={"tenant_id", "success"},
            ),
        ],
    )

    _meter = otel_metrics.get_meter("azure-sdk-qa-bot-server")
    _chat_request_counter = _meter.create_counter(
        name="chat_requests",
        description="Number of chat requests by tenant",
        unit="{request}",
    )
    _chat_duration_histogram = _meter.create_histogram(
        name="chat_duration",
        description="Chat request duration in seconds",
        unit="s",
    )


def record_chat_request(tenant: str) -> None:
    """Increment the chat-request counter for *tenant*."""
    if _chat_request_counter:
        _chat_request_counter.add(1, {"tenant_id": _normalize_tenant_id(tenant)})


def record_chat_duration(tenant: str, elapsed: float, *, success: bool) -> None:
    """Record chat-request latency for *tenant*."""
    if _chat_duration_histogram:
        _chat_duration_histogram.record(
            elapsed,
            {"tenant_id": _normalize_tenant_id(tenant), "success": success},
        )


# --------------------------------------------------------------------------- #
# Log query : fetch spans by trace_id from an App Insights resource.
# --------------------------------------------------------------------------- #

# How far back to search for spans matching a trace_id.
_DEFAULT_LOOKBACK_HOURS = 24 * 7

_logs_client: LogsQueryClient | None = None


def _get_logs_client() -> LogsQueryClient:
    global _logs_client
    if _logs_client is None:
        _logs_client = LogsQueryClient(get_credential())
    return _logs_client


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


async def query_spans_by_trace_id(
    trace_id: str,
    *,
    resource_id: str,
    lookback_hours: int = _DEFAULT_LOOKBACK_HOURS,
) -> list[TraceSpan]:
    """Return all App Insights rows tagged with ``trace_id`` (``operation_Id``).

    Queries the Application Insights resource identified by ``resource_id``
    (an ARM resource id). Searches ``dependencies``, ``requests`` and
    ``traces`` so the caller can reconstruct tool calls, the inbound HTTP
    request, and any log records. Returns an empty list if nothing is found
    (e.g. ingestion lag).
    """
    if not trace_id:
        return []

    client = _get_logs_client()

    # union dep / req / trace, filter by operation_Id (== OTel trace_id).
    query = """
let lookback = timespan;
let target = '@trace_id';
union
    (dependencies
        | where timestamp > ago(lookback)
        | where operation_Id == target
        | extend _table = "dependencies"),
    (requests
        | where timestamp > ago(lookback)
        | where operation_Id == target
        | extend _table = "requests"),
    (traces
        | where timestamp > ago(lookback)
        | where operation_Id == target
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
""".replace("@trace_id", trace_id.replace("'", "''"))

    try:
        result = await client.query_resource(
            resource_id=resource_id,
            query=query,
            timespan=timedelta(hours=lookback_hours),
        )
    except Exception:
        logger.exception("App Insights query failed for trace_id=%s", trace_id)
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
                    duration_ms=_coerce(row[col_idx["duration_ms"]], "float"),
                    success=_coerce(row[col_idx["success"]], "bool"),
                    result_code=_coerce(row[col_idx["result_code"]], "str"),
                    data=_coerce(row[col_idx["data"]], "str"),
                    custom_dimensions=_coerce(row[col_idx["customDimensions"]], "dict"),
                )
            )
    return spans


def _coerce(v: Any, kind: str) -> Any:
    """Normalize a loosely-typed App Insights cell to the given ``kind``.

    App Insights rows arrive inconsistently typed across tables (nulls,
    string-encoded numerics/bools, JSON-string customDimensions), so each
    column is coerced to its ``TraceSpan`` field type: ``"float"`` ->
    ``float | None``, ``"bool"`` -> ``bool | None``, ``"str"`` ->
    ``str | None`` (empty becomes ``None``), ``"dict"`` -> ``dict``.
    """
    if kind == "float":
        try:
            return float(v) if v is not None else None
        except (TypeError, ValueError):
            return None
    if kind == "bool":
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
    if kind == "str":
        if v is None:
            return None
        return str(v) or None
    if kind == "dict":
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
    raise ValueError(f"unknown coercion kind: {kind!r}")
