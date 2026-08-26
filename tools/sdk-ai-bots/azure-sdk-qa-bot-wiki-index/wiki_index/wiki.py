"""Summary-page synthesis for the wiki build."""

from __future__ import annotations

from .llm import ChatLLM, load_prompt

_MAX_PAGE_CHARS = 5000


def doc_title(rel: str) -> str:
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
    system = load_prompt("summary")
    out = llm.complete(system, user, max_tokens=1400)
    if not out:
        out = llm.complete(system, user, max_tokens=1800)
    return (out or "")[:_MAX_PAGE_CHARS]
