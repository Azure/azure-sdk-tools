"""Shared LLM + embedding backends (Azure OpenAI), used by both pipelines.

* :class:`ChatLLM` — a thin chat-completions wrapper that transparently handles
  reasoning models (gpt-5.x / o-series need ``max_completion_tokens`` and reject
  a custom temperature) and offers a ``complete_json`` helper that tolerates
  prose-wrapped JSON (mirrors WeKnora's ``ParseLLMJsonResponse``).
* :class:`Embedder` — the index's own embedding model (text-embedding-ada-002)
  so generated pages share the KB vector space.

Auth is AAD (``DefaultAzureCredential``) unless ``AZURE_OPENAI_API_KEY`` is set.
"""

from __future__ import annotations

import json
import logging
import os
import re
from typing import Sequence

logger = logging.getLogger(__name__)

_JSON_FENCE_RE = re.compile(r"```(?:json)?\s*(.*?)```", re.DOTALL)


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
        dl = deployment.lower()
        self._reasoning = dl.startswith(("gpt-5", "gpt5", "o1", "o3", "o4"))

    def complete(self, system: str, user: str, max_tokens: int = 600) -> str:
        """Single chat completion; returns the message content (may be empty)."""
        messages = [
            {"role": "system", "content": system},
            {"role": "user", "content": user},
        ]
        if self._reasoning:
            resp = self._client.chat.completions.create(
                model=self._deployment,
                messages=messages,
                max_completion_tokens=max_tokens * 4,
            )
        else:
            resp = self._client.chat.completions.create(
                model=self._deployment,
                messages=messages,
                temperature=0.1,
                max_tokens=max_tokens,
            )
        return (resp.choices[0].message.content or "").strip()

    def complete_json(self, system: str, user: str, max_tokens: int = 900):
        """Chat completion whose content is parsed as JSON (fence/prose tolerant).

        Returns the parsed object, or ``None`` if nothing parseable came back.
        """
        raw = self.complete(system, user, max_tokens=max_tokens)
        if not raw:
            return None
        return _parse_json_response(raw)


def _parse_json_response(raw: str):
    """Parse JSON that may be fenced or wrapped in prose (WeKnora-style)."""
    raw = raw.strip()
    for candidate in _json_candidates(raw):
        try:
            return json.loads(candidate)
        except json.JSONDecodeError:
            continue
    logger.warning("could not parse JSON from LLM response (len=%d)", len(raw))
    return None


def _json_candidates(raw: str):
    """Yield progressively looser JSON substrings to attempt to parse."""
    yield raw
    m = _JSON_FENCE_RE.search(raw)
    if m:
        yield m.group(1).strip()
    # first {...} or [...] span
    for opener, closer in (("[", "]"), ("{", "}")):
        i = raw.find(opener)
        j = raw.rfind(closer)
        if 0 <= i < j:
            yield raw[i : j + 1]


class Embedder:
    """Azure OpenAI embeddings using the index's own model (shared vector space)."""

    def __init__(self, client, deployment: str, batch_size: int = 16):
        self._client = client
        self._deployment = deployment
        self._batch = batch_size

    def embed(self, texts: Sequence[str]) -> list[list[float]]:
        out: list[list[float]] = []
        items = [t[:8000] or " " for t in texts]
        for i in range(0, len(items), self._batch):
            resp = self._client.embeddings.create(
                model=self._deployment, input=items[i : i + self._batch]
            )
            out.extend(d.embedding for d in resp.data)
        return out

    def embed_one(self, text: str) -> list[float]:
        return self.embed([text])[0]
