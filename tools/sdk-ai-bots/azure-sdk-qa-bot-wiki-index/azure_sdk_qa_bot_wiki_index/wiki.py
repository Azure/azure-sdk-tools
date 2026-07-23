"""Wiki creation pipeline for summary, entity, concept, and index pages."""

from __future__ import annotations

import logging
from concurrent.futures import ThreadPoolExecutor

from .llm import ChatLLM
from .pages import PAGE_SUMMARY, WikiPage, make_slug
from .reader import rel_title, source_folder
from .wiki_extract import GRAN_STANDARD, map_extract
from .wiki_reduce import reduce_pages

logger = logging.getLogger(__name__)

_MAX_PAGE_CHARS = 5000

_SUMMARY_SYS = (
    "You are building a comprehensive expert KNOWLEDGE PAGE from one Azure SDK / "
    "TypeSpec document, so an agent can answer questions FROM internalized "
    "knowledge rather than re-reading raw docs. Capture ALL the concrete, "
    "reusable knowledge the document teaches — be thorough, not terse:\n"
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
    try:
        out = llm.complete(_SUMMARY_SYS, user, max_tokens=1400)
        if not out:
            out = llm.complete(_SUMMARY_SYS, user, max_tokens=1800)
        return (out or "")[:_MAX_PAGE_CHARS]
    except Exception:
        logger.warning("synthesize_summary failed for %s", doc_title, exc_info=True)
        return ""


def build_summary_pages(
    corpus: list[tuple[str, str]],
    llm: ChatLLM,
    *,
    max_workers: int = 16,
) -> list[WikiPage]:
    """One summary page per source document (LLM, parallel)."""

    def one(item: tuple[str, str]) -> WikiPage | None:
        source_path, text = item
        folder = source_folder(source_path)
        rel = rel_title(source_path)
        title = _doc_title(rel)
        body = synthesize_summary(llm, title, text)
        if not body:
            return None
        return WikiPage(
            slug=make_slug(PAGE_SUMMARY, rel),
            page_type=PAGE_SUMMARY,
            title=f"{title} (knowledge)",
            content=body,
            context_id=folder,  # inherits source scope → existing tenant filters
            source_refs=[rel],
            orig_title=rel,  # drives get_link back to the real doc
        )

    pages: list[WikiPage] = []
    with ThreadPoolExecutor(max_workers=max_workers) as ex:
        for p in ex.map(one, corpus):
            if p is not None:
                pages.append(p)
    logger.info("build_summary_pages: %d summary pages from %d docs", len(pages), len(corpus))
    return pages


def build_wiki(
    corpus: list[tuple[str, str]],
    llm: ChatLLM,
    *,
    min_docs: int = 2,
    granularity: str = GRAN_STANDARD,
) -> list[WikiPage]:
    """Build summary, entity, concept, and index pages."""
    pages: list[WikiPage] = build_summary_pages(corpus, llm)
    extractions = map_extract(corpus, llm, granularity=granularity)
    pages += reduce_pages(extractions, llm, min_docs=min_docs)
    logger.info("build_wiki: %d total wiki pages", len(pages))
    return pages
