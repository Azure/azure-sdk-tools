"""Azure OpenAI chat backend and prompt loading."""

from __future__ import annotations

import json
import logging
import os
from functools import lru_cache
from pathlib import Path

logger = logging.getLogger(__name__)

_PROMPT_DIR = Path(__file__).parent / "prompts"


@lru_cache(maxsize=None)
def load_prompt(name: str) -> str:
    """Read the system prompt stored in ``prompts/<name>.md``."""
    return (_PROMPT_DIR / f"{name}.md").read_text(encoding="utf-8").strip()


def build_azure_openai_client(endpoint: str, api_version: str = "2024-12-01-preview"):
    """Build a synchronous AzureOpenAI client (API key if present, else AAD)."""
    from openai import AzureOpenAI

    api_key = os.environ.get("AZURE_OPENAI_API_KEY")
    if api_key:
        return AzureOpenAI(azure_endpoint=endpoint, api_key=api_key, api_version=api_version)
    from azure.identity import DefaultAzureCredential, get_bearer_token_provider

    token_provider = get_bearer_token_provider(
        DefaultAzureCredential(), "https://cognitiveservices.azure.com/.default"
    )
    return AzureOpenAI(
        azure_endpoint=endpoint,
        azure_ad_token_provider=token_provider,
        api_version=api_version,
    )


class ChatLLM:
    """Azure OpenAI chat wrapper (reasoning-model aware)."""

    def __init__(self, client, deployment: str):
        self._client = client
        self._deployment = deployment
        self._reasoning = deployment.lower().startswith(("gpt-5", "gpt5", "o1", "o3", "o4"))

    @property
    def deployment(self) -> str:
        """Return the deployment identity used to generate wiki content."""
        return self._deployment

    def _create(self, system: str, user: str, max_tokens: int, json_mode: bool = False):
        kwargs: dict = {
            "model": self._deployment,
            "messages": [
                {"role": "system", "content": system},
                {"role": "user", "content": user},
            ],
        }
        if json_mode:
            kwargs["response_format"] = {"type": "json_object"}
        if self._reasoning:
            kwargs["max_completion_tokens"] = max_tokens * 4
        else:
            kwargs["temperature"] = 0.1
            kwargs["max_tokens"] = max_tokens
        return self._client.chat.completions.create(**kwargs)

    def complete(self, system: str, user: str, max_tokens: int = 600) -> str:
        """Single chat completion; returns the message content (may be empty)."""
        resp = self._create(system, user, max_tokens)
        return (resp.choices[0].message.content or "").strip()

    def complete_json(self, system: str, user: str, max_tokens: int = 900):
        """Chat completion in JSON mode; returns the parsed object or ``None``."""
        resp = self._create(system, user, max_tokens, json_mode=True)
        raw = (resp.choices[0].message.content or "").strip()
        if not raw:
            return None
        try:
            return json.loads(raw)
        except json.JSONDecodeError:
            logger.warning("could not parse JSON from LLM response (len=%d)", len(raw))
            return None
