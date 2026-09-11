"""Inject the request tenant into native tool invocation context."""

from __future__ import annotations

import re
from collections.abc import Awaitable, Callable, Sequence

from agent_framework import AgentContext, AgentMiddleware, Message

from config.tenant_config import TenantID, get_tenant_config

_TENANT_PATTERN = re.compile(
    r"\[tenant_context\]\s+original_tenant_id=([a-z0-9_]+)"
)
class TenantContextMiddleware(AgentMiddleware):
    async def process(
        self,
        context: AgentContext,
        call_next: Callable[[], Awaitable[None]],
    ) -> None:
        tenant_id = _tenant_from_messages(context.messages)
        if tenant_id is not None:
            context.function_invocation_kwargs["tenant_id"] = tenant_id.value
        await call_next()


def _tenant_from_messages(messages: Sequence[Message]) -> TenantID | None:
    for message in messages:
        match = _TENANT_PATTERN.search(getattr(message, "text", "") or "")
        if match is None:
            continue
        tenant_id = TenantID(match.group(1))
        if get_tenant_config(tenant_id) is None:
            raise RuntimeError(f"tenant has no configuration: {tenant_id.value}")
        return tenant_id
    return None
