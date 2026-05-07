"""Azure AI Foundry client singletons.

Each client is created once on first access and reused for the lifetime of
the process.
"""

import asyncio
import logging
import re

from agent_framework import TruncationStrategy
from agent_framework.foundry import FoundryChatClient
from azure.ai.projects.aio import AIProjectClient
from openai import AsyncAzureOpenAI, AsyncOpenAI
from opentelemetry.sdk.trace import SpanProcessor

from config.app_config import get as cfg
from utils.azure_credential import get_credential

logger = logging.getLogger(__name__)

# -- Compaction constants (token counts) -----------------------------------
COMPACTION_TRIGGER_TOKENS = 100000
COMPACTION_TARGET_TOKENS = 80000

_agent_client: FoundryChatClient | None = None
_project_client: AIProjectClient | None = None
_openai_client: AsyncOpenAI | None = None
_embedding_client: AsyncAzureOpenAI | None = None


def get_agent_client() -> FoundryChatClient:
    """Return the shared FoundryChatClient (created once on first call)."""
    global _agent_client
    if _agent_client is None:
        _agent_client = FoundryChatClient(
            project_endpoint=cfg("AI_FOUNDRY_PROJECT_ENDPOINT"),
            model=cfg("AI_FOUNDRY_AGENT_COMPLETION_MODEL"),
            credential=get_credential(),
            compaction_strategy=TruncationStrategy(
                max_n=COMPACTION_TRIGGER_TOKENS,
                compact_to=COMPACTION_TARGET_TOKENS,
            ),
        )
    return _agent_client


def get_project_client() -> AIProjectClient:
    """Return the shared AIProjectClient (created once on first call)."""
    global _project_client
    if _project_client is None:
        endpoint = cfg("AI_FOUNDRY_PROJECT_ENDPOINT")
        if not endpoint:
            raise RuntimeError(
                "AI_FOUNDRY_PROJECT_ENDPOINT is required in App Configuration."
            )
        _project_client = AIProjectClient(
            endpoint=endpoint,
            credential=get_credential(),
            allow_preview=True,
        )
    return _project_client


def get_openai_client() -> AsyncOpenAI:
    """Return the shared OpenAI client (created once on first call)."""
    global _openai_client
    if _openai_client is None:
        agent_name = cfg("AI_FOUNDRY_AGENT_NAME", "azure-sdk-chat-agent")
        # Hosted agents in refreshed preview must be called via per-agent endpoint.
        _openai_client = get_project_client().get_openai_client(agent_name=agent_name)
    return _openai_client


def get_embedding_client() -> AsyncAzureOpenAI:
    """Return a dedicated Azure OpenAI client for embedding generation.

    The embedding model is deployed on the Azure OpenAI resource directly
    (``*.openai.azure.com``), not through the AI Foundry project proxy
    (``*.services.ai.azure.com``).  This client uses the direct endpoint.
    """
    global _embedding_client
    if _embedding_client is not None:
        return _embedding_client

    try:
        endpoint = cfg("AI_FOUNDRY_PROJECT_ENDPOINT", "")
        # Extract resource name from AI Foundry endpoint
        # e.g. https://<resource>.services.ai.azure.com/... → <resource>
        m = re.search(r"https://([^.]+)\.", endpoint)
        resource_name = m.group(1) if m else ""
        azure_openai_endpoint = f"https://{resource_name}.openai.azure.com"

        _embedding_client = AsyncAzureOpenAI(
            azure_endpoint=azure_openai_endpoint,
            api_version="2024-02-01",
            azure_ad_token_provider=_get_token_provider(),
        )
        return _embedding_client
    except Exception:
        logger.info("Failed to create embedding client", exc_info=True)
        raise


def _get_token_provider():
    """Return a callable that provides Azure AD tokens for Azure OpenAI."""
    credential = get_credential()

    async def _provider():
        token = await credential.get_token(
            "https://cognitiveservices.azure.com/.default"
        )
        return token.token

    return _provider


# App Insights rejects telemetry items > 65 536 bytes.  The agent
# framework records full tool arguments/results as span attributes
# (``gen_ai.content.prompt`` / ``tool.call.results`` etc.) which can
# easily exceed this limit for knowledge-heavy tools.  This processor
# truncates oversized string attributes in ``on_end`` *before* the
# Azure Monitor exporter serialises them, so the span still gets
# exported with a truncation marker instead of being silently dropped.
_MAX_ATTR_VALUE_LEN = 8192  # 8 KB per attribute — well under the 65 KB item limit


class SpanAttributeTruncator(SpanProcessor):
    """Truncate large span attribute values so App Insights does not drop them."""

    def __init__(self, max_len: int = _MAX_ATTR_VALUE_LEN) -> None:
        self._max_len = max_len

    def on_start(self, span, parent_context=None) -> None:  # noqa: D401
        pass

    def on_end(self, span) -> None:
        raw_attrs = getattr(span, "_attributes", None)
        if raw_attrs is None:
            return
        for key in list(raw_attrs.keys()):
            val = raw_attrs.get(key)
            if isinstance(val, str) and len(val) > self._max_len:
                raw_attrs[key] = (
                    val[: self._max_len] + f"... [truncated from {len(val)} chars]"
                )

    def shutdown(self) -> None:
        pass

    def force_flush(self, timeout_millis=None) -> bool:
        return True


class FoundryAgentSpanEnricher(SpanProcessor):
    """Enriches the top-level ``HostedAgents-*`` span emitted by the platform.

    The platform emits a root span named ``HostedAgents-<response_id>`` with
    ``gen_ai.provider.name = "AzureAI Hosted Agents"`` and sets
    ``azure.ai.agentserver.conversation_id`` — but **not**
    ``gen_ai.conversation.id``.  Without the standard key the Foundry Traces
    "Conversations" tab shows ``--``.

    This processor copies the conversation id to the standard attribute in
    ``on_end`` (after the platform has set it).  It does **not** touch child
    ``chat`` or ``execute_tool`` spans — enriching those would create
    duplicate rows in the Traces view because each carries a distinct
    ``response_id``.
    """

    def __init__(self, project_id: str, agent_name: str, agent_id: str) -> None:
        self._project_id = project_id
        self._agent_name = agent_name
        self._agent_id = agent_id

    @staticmethod
    def _is_hosted_agent_span(span) -> bool:
        """Return True for the platform's top-level span."""
        attrs = getattr(span, "attributes", None) or {}
        return (
            attrs.get("gen_ai.provider.name") == "AzureAI Hosted Agents"
            or attrs.get("gen_ai.operation.name") == "invoke_agent"
        )

    def on_start(self, span, parent_context=None) -> None:
        """Inject Foundry attributes on the top-level span."""
        if not self._is_hosted_agent_span(span):
            return
        span.set_attribute("microsoft.foundry.project.id", self._project_id)
        span.set_attribute("gen_ai.agent.name", self._agent_name)
        span.set_attribute("gen_ai.agent.id", self._agent_id)

    def on_end(self, span) -> None:
        if not self._is_hosted_agent_span(span):
            return
        # Copy conversation id to the standard attribute if missing
        raw_attrs = getattr(span, "_attributes", None)
        if raw_attrs is None:
            return
        conv_id = raw_attrs.get("azure.ai.agentserver.conversation_id")
        if conv_id and not raw_attrs.get("gen_ai.conversation.id"):
            raw_attrs["gen_ai.conversation.id"] = conv_id
        logger.debug(
            "Span ended: name=%s conv=%s agent=%s",
            span.name,
            conv_id,
            raw_attrs.get("gen_ai.agent.name"),
        )

    def shutdown(self) -> None:
        pass

    def force_flush(self, timeout_millis=None) -> bool:
        return True


async def close_clients() -> None:
    """Close all clients.  Safe to call even if never created."""
    global _agent_client, _project_client, _openai_client, _embedding_client
    if _embedding_client is not None:
        await _embedding_client.close()
        _embedding_client = None
    if _openai_client is not None:
        await _openai_client.close()
        _openai_client = None
    if _agent_client is not None:
        _agent_client = None
    if _project_client is not None:
        await _project_client.close()
        _project_client = None
