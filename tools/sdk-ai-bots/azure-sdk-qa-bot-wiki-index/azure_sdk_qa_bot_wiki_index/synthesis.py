"""LLM synthesis + embedding backends (Azure OpenAI).

* :class:`Synthesizer` — gpt-5.x reasoning-model chat completions for knowledge
  extraction (per-doc summary cards) and cross-document entity / concept pages.
* :class:`Embedder` — the index's own embedding model (text-embedding-ada-002)
  so generated pages share the KB vector space and are retrievable by the same
  ``VectorizableTextQuery``.

Auth is AAD (``DefaultAzureCredential``) against the configured Azure OpenAI
endpoint; no API keys.
"""

from __future__ import annotations

import logging
import os
from typing import Sequence

logger = logging.getLogger(__name__)


# --------------------------------------------------------------------------- #
# Prompts
# --------------------------------------------------------------------------- #
_SUMMARY_SYS = (
    "You are building an expert KNOWLEDGE CARD from Azure SDK / TypeSpec "
    "documentation, so an agent can answer questions FROM internalized knowledge "
    "rather than re-reading raw docs. Extract the concrete, reusable knowledge "
    "the document teaches: definitions, rules, exact decorator / API / property "
    "names and their effects, required steps and their order, constraints, "
    "defaults, valid values, and common gotchas or error causes. Write dense, "
    "declarative facts an expert would remember, as tight bullet points. Include "
    "specific names and syntax. Do NOT use navigation phrases like 'this section "
    "covers' or 'refer to'. Only state knowledge grounded in the document; never "
    "invent APIs or facts. Max ~250 words."
)
_ENTITY_SYS = (
    "You are writing a cross-document ENTITY page for one Azure SDK / TypeSpec "
    "symbol (a decorator, API, or type — e.g. `@added`, `TrackedResource`). Given "
    "excerpts from multiple documents that mention it, synthesise everything an "
    "expert must know about THIS symbol: what it does, exact signature/usage, "
    "when to use it, interactions with related symbols, constraints, and common "
    "mistakes. Merge duplicates across documents; keep the most specific facts. "
    "Tight declarative bullets, exact syntax, no navigation phrases. Max ~220 words."
)
_CONCEPT_SYS = (
    "You are writing a cross-document CONCEPT page for one Azure SDK / TypeSpec "
    "topic (e.g. 'API versioning', 'long-running operations', 'pagination'). "
    "Given excerpts from multiple documents on the topic, synthesise the core "
    "rules, the decorators/APIs involved, the correct approach, and the pitfalls "
    "an expert knows. Merge duplicates; keep specific, actionable facts. Tight "
    "declarative bullets, exact names/syntax, no navigation phrases. Max ~220 words."
)


class Synthesizer:
    """Azure OpenAI chat synthesizer (handles reasoning models like gpt-5.x)."""

    def __init__(self, client, deployment: str):
        self._client = client
        self._deployment = deployment
        dl = deployment.lower()
        self._reasoning = dl.startswith(("gpt-5", "gpt5", "o1", "o3", "o4"))

    def _complete(self, system: str, user: str, max_tokens: int) -> str:
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
                temperature=0.2,
                max_tokens=max_tokens,
            )
        return (resp.choices[0].message.content or "").strip()

    def summary_card(self, doc_title: str, full_text: str) -> str:
        full_text = full_text.strip()
        if not full_text:
            return ""
        user = f"Document: {doc_title}\n\n{full_text[:9000]}"
        try:
            out = self._complete(_SUMMARY_SYS, user, max_tokens=600)
            if not out:
                out = self._complete(_SUMMARY_SYS, user, max_tokens=1200)
            return out
        except Exception:
            logger.warning("summary_card failed for %s", doc_title, exc_info=True)
            return ""

    def entity_page(self, entity: str, excerpts: Sequence[str]) -> str:
        body = "\n\n---\n\n".join(e.strip() for e in excerpts if e.strip())
        if not body:
            return ""
        user = f"Symbol: {entity}\n\nExcerpts mentioning it:\n{body[:9000]}"
        try:
            out = self._complete(_ENTITY_SYS, user, max_tokens=550)
            return out or self._complete(_ENTITY_SYS, user, max_tokens=1100)
        except Exception:
            logger.warning("entity_page failed for %s", entity, exc_info=True)
            return ""

    def concept_page(self, concept: str, excerpts: Sequence[str]) -> str:
        body = "\n\n---\n\n".join(e.strip() for e in excerpts if e.strip())
        if not body:
            return ""
        user = f"Topic: {concept}\n\nExcerpts on the topic:\n{body[:9000]}"
        try:
            out = self._complete(_CONCEPT_SYS, user, max_tokens=550)
            return out or self._complete(_CONCEPT_SYS, user, max_tokens=1100)
        except Exception:
            logger.warning("concept_page failed for %s", concept, exc_info=True)
            return ""


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
