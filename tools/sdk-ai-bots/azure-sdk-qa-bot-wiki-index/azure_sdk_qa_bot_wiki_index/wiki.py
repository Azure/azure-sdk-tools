"""Summary-page synthesis for the wiki build."""

from __future__ import annotations

import logging

from .llm import ChatLLM

logger = logging.getLogger(__name__)

_MAX_PAGE_CHARS = 5000

_SUMMARY_SYS = (
    "You are building a comprehensive expert KNOWLEDGE PAGE from one Azure SDK "
    "document (TypeSpec, a per-language SDK, ARM/data-plane guidance, a release/"
    "onboarding process, or tooling), so an agent can answer questions FROM "
    "internalized knowledge rather than re-reading raw docs. Capture ALL the "
    "concrete, reusable knowledge the document teaches — be thorough, not terse:\n"
    "- Definitions and purpose of each concept/decorator/API/type it covers.\n"
    "- Exact names and signatures (decorators with @, operations, models, "
    "properties) and their precise effects.\n"
    "- Rules, requirements, constraints, defaults, valid/invalid values, and the "
    "EXACT conditions and exceptions (e.g. 'X cannot be suppressed', 'requires "
    "PUBLIC visibility', 'beta may ship alongside GA').\n"
    "- Required steps and their order; how pieces interact.\n"
    "- Short code/usage examples when the document shows them.\n"
    "- Common gotchas, error causes, and their fixes.\n"
    "Organize under clear markdown headings that follow the document's own "
    "structure. Write dense, declarative facts an expert would remember. Keep "
    "every specific name, value, and syntax verbatim. Do NOT use navigation "
    "phrases like 'this section covers' or 'refer to'. Only state knowledge "
    "grounded in the document; never invent APIs or facts. Aim for 400-800 words "
    "(shorter only if the document is genuinely small)."
)


def _doc_title(rel: str) -> str:
    """Human-ish title from a rel path: last ``#`` segment, extension stripped."""
    last = rel.split("#")[-1]
    for ext in (".md", ".mdx"):
        if last.endswith(ext):
            last = last[: -len(ext)]
    return last or rel


def synthesize_summary(llm: ChatLLM, doc_title: str, full_text: str) -> str:
    """LLM-synthesise one summary page from a document's full text."""
    full_text = (full_text or "").strip()
    if not full_text:
        return ""
    user = f"Document: {doc_title}\n\n{full_text[:16000]}"
    out = llm.complete(_SUMMARY_SYS, user, max_tokens=1400)
    if not out:
        out = llm.complete(_SUMMARY_SYS, user, max_tokens=1800)
    return (out or "")[:_MAX_PAGE_CHARS]



