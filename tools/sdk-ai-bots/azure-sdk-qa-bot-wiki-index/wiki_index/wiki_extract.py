"""Map phase for per-document entity and concept extraction."""

from __future__ import annotations

import logging
from dataclasses import dataclass, field

from openai import OpenAIError

from .llm import ChatLLM, load_prompt

logger = logging.getLogger(__name__)

# Maximum document characters sent to the extraction LLM call.
_MAX_EXTRACT_CHARS = 12000


@dataclass
class ExtractedItem:
    """One entity or concept mention from a single document."""

    name: str
    type: str = ""
    description: str = ""
    source_ref: str = ""
    aliases: list[str] = field(default_factory=list)
    details: str = ""


@dataclass
class DocExtraction:
    """All items extracted from one document."""

    source_ref: str
    entities: list[ExtractedItem] = field(default_factory=list)
    concepts: list[ExtractedItem] = field(default_factory=list)
    failed: bool = False


def _norm(s: object) -> str:
    return str(s or "").strip()


def _alias_list(raw: object) -> list[str]:
    if not isinstance(raw, list):
        return []
    out: list[str] = []
    for a in raw:
        a = _norm(a)
        if a and a.lower() not in {x.lower() for x in out}:
            out.append(a)
    return out


def extract_doc(llm: ChatLLM, source_ref: str, text: str) -> DocExtraction:
    """Run the combined entity+concept extraction for one document."""
    out = DocExtraction(source_ref=source_ref)
    text = (text or "").strip()
    if not text:
        return out
    user = f"Document: {source_ref}\n\n{text[:_MAX_EXTRACT_CHARS]}"
    try:
        parsed = llm.complete_json(load_prompt("extract"), user, max_tokens=1200)
    except OpenAIError:
        logger.warning("extract_doc failed for %s", source_ref, exc_info=True)
        out.failed = True
        return out
    if not isinstance(parsed, dict):
        logger.warning("extract_doc returned invalid JSON for %s", source_ref)
        out.failed = True
        return out
    for e in parsed.get("entities", []) or []:
        if isinstance(e, dict) and _norm(e.get("name")):
            out.entities.append(
                ExtractedItem(
                    name=_norm(e.get("name")),
                    type=_norm(e.get("type")),
                    description=_norm(e.get("description")),
                    source_ref=source_ref,
                    aliases=_alias_list(e.get("aliases")),
                    details=_norm(e.get("details")),
                )
            )
    for c in parsed.get("concepts", []) or []:
        if isinstance(c, dict) and _norm(c.get("name")):
            out.concepts.append(
                ExtractedItem(
                    name=_norm(c.get("name")),
                    description=_norm(c.get("description")),
                    source_ref=source_ref,
                    aliases=_alias_list(c.get("aliases")),
                    details=_norm(c.get("details")),
                )
            )
    return out
