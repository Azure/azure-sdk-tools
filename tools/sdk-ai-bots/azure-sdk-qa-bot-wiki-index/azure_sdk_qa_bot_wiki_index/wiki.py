"""Wiki creation pipeline — WeKnora-faithful MapReduce over the corpus.

Produces the four WeKnora wiki page types as :class:`WikiPage` objects:

* **summary**  — one dense page per source document (this module).
* **entity**   — cross-document, per symbol (map+reduce, :mod:`wiki_reduce`).
* **concept**  — cross-document, per topic (map+reduce).
* **index**    — navigation page.

Entities/concepts are extracted per document (:mod:`wiki_extract`, the *map*
phase) then aggregated + synthesised + cross-linked (:mod:`wiki_reduce`, the
*reduce* phase). Summary pages are produced per document here.
"""

from __future__ import annotations

import logging
from concurrent.futures import ThreadPoolExecutor

from .llm import ChatLLM
from .pages import PAGE_SUMMARY, WikiPage, make_slug
from .reader import rel_title, source_folder
from .wiki_extract import map_extract
from .wiki_reduce import reduce_pages

logger = logging.getLogger(__name__)

_MAX_PAGE_CHARS = 1800

_SUMMARY_SYS = (
    "You are building an expert KNOWLEDGE PAGE from one Azure SDK / TypeSpec "
    "document, so an agent can answer questions FROM internalized knowledge "
    "rather than re-reading raw docs. Extract the concrete, reusable knowledge "
    "the document teaches: definitions, rules, exact decorator / API / property "
    "names and their effects, required steps and their order, constraints, "
    "defaults, valid values, and common gotchas or error causes. Write dense, "
    "declarative facts an expert would remember, as tight bullet points. Include "
    "specific names and syntax. Do NOT use navigation phrases like 'this section "
    "covers' or 'refer to'. Only state knowledge grounded in the document; never "
    "invent APIs or facts. Max ~230 words."
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
    user = f"Document: {doc_title}\n\n{full_text[:9000]}"
    try:
        out = llm.complete(_SUMMARY_SYS, user, max_tokens=560)
        if not out:
            out = llm.complete(_SUMMARY_SYS, user, max_tokens=1100)
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
) -> list[WikiPage]:
    """Full WeKnora-faithful wiki build: summary + (map→reduce) entity/concept/index."""
    pages: list[WikiPage] = build_summary_pages(corpus, llm)
    extractions = map_extract(corpus, llm)
    pages += reduce_pages(extractions, llm, min_docs=min_docs)
    logger.info("build_wiki: %d total wiki pages", len(pages))
    return pages
