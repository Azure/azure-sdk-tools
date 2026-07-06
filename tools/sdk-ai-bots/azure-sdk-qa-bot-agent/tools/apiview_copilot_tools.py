"""APIView Copilot tool — delegates API-design review to the AVC agent.

Forwards a question (optionally with raw TypeSpec/Swagger content) to AVC's
``/agent/chat`` endpoint. AVC cannot fetch ``apiview.dev`` content, so we pass
the API surface inline and AVC returns the guideline-grounded review.
"""

from __future__ import annotations

import asyncio
import logging
import time
from typing import Annotated

import httpx
from pydantic import BaseModel

from config.app_config import get as cfg
from tools import tool
from utils.azure_credential import get_credential

logger = logging.getLogger(__name__)

# Endpoint and token scope come from Azure App Configuration.
_ENDPOINT_KEY = "APIVIEW_COPILOT_ENDPOINT"
_SCOPE_KEY = "APIVIEW_COPILOT_SCOPE"

_HTTP_TIMEOUT_SECONDS = 120.0
_MAX_SPEC_CHARS = 60000
_TOKEN_REFRESH_BUFFER_SECS = 300

_token_cache: tuple[str, float] | None = None
_token_cache_lock = asyncio.Lock()


class ApiViewCopilotResult(BaseModel):
    """Result for ``ask_apiview_copilot``."""

    success: bool
    response: str = ""
    thread_id: str = ""
    error: str | None = None


async def _get_token() -> str:
    """Mint (and cache) an AAD token for the AVC application."""
    global _token_cache
    now = time.monotonic()
    if _token_cache is not None and _token_cache[1] > now:
        return _token_cache[0]

    async with _token_cache_lock:
        if _token_cache is not None and _token_cache[1] > time.monotonic():
            return _token_cache[0]

        scope = cfg(_SCOPE_KEY, "")
        if not scope:
            raise RuntimeError(
                f"{_SCOPE_KEY} is not configured. Set it in Azure App Configuration."
            )
        credential = get_credential()
        access_token = await credential.get_token(scope)
        ttl = max(access_token.expires_on - int(time.time()) - _TOKEN_REFRESH_BUFFER_SECS, 0)
        _token_cache = (access_token.token, time.monotonic() + ttl)
        return access_token.token


def _build_user_input(
    question: str, spec_content: str | None, language: str | None
) -> str:
    """Compose the AVC ``userInput`` from the question and optional spec."""
    parts: list[str] = [question.strip()]
    if language:
        parts.append(f"\nLanguage: {language.strip()}")
    if spec_content:
        surface = spec_content.strip()
        if len(surface) > _MAX_SPEC_CHARS:
            surface = surface[:_MAX_SPEC_CHARS] + "\n... [truncated]"
        parts.append("\nAPI surface to review:\n" + surface)
    return "\n".join(parts)


class ApiViewCopilotTools:
    """Tools that delegate API-design questions to the APIView Copilot agent."""

    @tool
    async def ask_apiview_copilot(
        self,
        *,
        question: Annotated[
            str,
            "The API-design question or review instruction, e.g. 'Review this "
            "TypeSpec against Azure API guidelines' or 'What is the guideline "
            "for naming async methods in Python?'.",
        ],
        spec_content: Annotated[
            str | None,
            "The raw API surface to review (TypeSpec/Swagger/SDK listing). Fetch "
            "it yourself and pass inline; do NOT pass an apiview.dev URL. Omit "
            "for pure guideline questions.",
        ] = None,
        language: Annotated[
            str | None,
            "API language, e.g. 'typespec', 'python', 'dotnet', 'java', 'typescript'," \
            "'go'. Provide it when known.",
        ] = None,
        thread_id: Annotated[
            str | None,
            "An AVC thread id from a previous call to continue the conversation.",
        ] = None,
    ) -> ApiViewCopilotResult:
        """Ask APIView Copilot an Azure SDK API-design question or request a review.

        Use for API design guideline Q&A and API/spec review. Prefer over
        ``search_knowledge_base`` when the user wants a review of an API surface
        or a language-specific guideline ruling; it complements knowledge search.
        """
        endpoint = cfg(_ENDPOINT_KEY, "")
        if not endpoint:
            return ApiViewCopilotResult(
                success=False,
                error=(
                    f"{_ENDPOINT_KEY} is not configured. "
                    "Set it in Azure App Configuration."
                ),
            )
        user_input = _build_user_input(question, spec_content, language)

        payload: dict[str, str] = {"userInput": user_input}
        if thread_id:
            payload["threadId"] = thread_id

        try:
            token = await _get_token()
        except Exception as e:  # noqa: BLE001 — surface auth failures to the agent
            logger.exception("Failed to acquire APIView Copilot token")
            return ApiViewCopilotResult(
                success=False,
                error=f"Could not authenticate to APIView Copilot: {e}",
            )

        try:
            async with httpx.AsyncClient(timeout=_HTTP_TIMEOUT_SECONDS) as client:
                resp = await client.post(
                    endpoint,
                    headers={"Authorization": f"Bearer {token}"},
                    json=payload,
                )
        except httpx.HTTPError as e:
            logger.exception("APIView Copilot request failed")
            return ApiViewCopilotResult(
                success=False, error=f"APIView Copilot request failed: {e}"
            )

        if resp.status_code == httpx.codes.FORBIDDEN:
            return ApiViewCopilotResult(
                success=False,
                error=(
                    "APIView Copilot returned 403 (Unauthorized). The agent "
                    "identity likely lacks the required app role (Read/App.Read) "
                    "on the APIView Copilot application."
                ),
            )
        if resp.status_code == httpx.codes.TOO_MANY_REQUESTS:
            return ApiViewCopilotResult(
                success=False,
                error="APIView Copilot is rate limited (429). Try again shortly.",
            )
        if resp.is_error:
            return ApiViewCopilotResult(
                success=False,
                error=f"APIView Copilot returned {resp.status_code}: {resp.text[:500]}",
            )

        data = resp.json()
        return ApiViewCopilotResult(
            success=True,
            response=data.get("response", ""),
            thread_id=data.get("threadId", ""),
        )
