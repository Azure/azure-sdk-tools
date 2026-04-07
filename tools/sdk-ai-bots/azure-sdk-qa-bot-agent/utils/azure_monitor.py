"""Azure Monitor (Application Insights) metrics utilities.

Configures OpenTelemetry metrics and exposes pre-built instruments that
the backend server can use to record custom metrics visible in the Azure
Portal under Application Insights > Metrics.
"""

import logging
import os

logger = logging.getLogger(__name__)

_meter = None
_chat_request_counter = None
_chat_duration_histogram = None
_initialized = False


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

    from azure.monitor.opentelemetry import configure_azure_monitor

    configure_azure_monitor(
        connection_string=conn_str,
        enable_live_metrics=True,
    )

    from opentelemetry import metrics as otel_metrics

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
        _chat_request_counter.add(1, {"tenant": tenant})


def record_chat_duration(tenant: str, elapsed: float, *, success: bool) -> None:
    """Record chat-request latency for *tenant*."""
    if _chat_duration_histogram:
        _chat_duration_histogram.record(elapsed, {"tenant": tenant, "success": success})
