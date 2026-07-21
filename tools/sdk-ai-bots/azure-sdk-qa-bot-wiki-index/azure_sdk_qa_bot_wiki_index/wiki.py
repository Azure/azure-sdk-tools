"""Wiki creation pipeline — doc-level LLM synthesis (independent of graph).

Mirrors WeKnora's wiki layer (``wiki_ingest.go`` / ``ChunkTypeWikiPage``): each
source document is synthesised by an LLM into one dense, self-contained
"knowledge page" (``page_type="wiki"``) that is pushed into the shared index and
retrieved like any other chunk (with a rerank-time boost on the agent side).

This pipeline knows **nothing** about entities, relationships, or the graph —
that is the separate :mod:`graph` pipeline's job.
"""

from __future__ import annotations

import logging
from concurrent.futures import ThreadPoolExecutor

from .documents import WikiDoc, make_wiki_doc
from .llm import ChatLLM, Embedder
from .reader import rel_title, source_folder

logger = logging.getLogger(__name__)

_WIKI_SYS = (
    "You are building an expert KNOWLEDGE PAGE from one Azure SDK / TypeSpec "
    "document, so an agent can answer questions FROM internalized knowledge "
    "rather than re-reading raw docs. Extract the concrete, reusable knowledge "
    "the document teaches: definitions, rules, exact decorator / API / property "
    "names and their effects, required steps and their order, constraints, "
    "defaults, valid values, and common gotchas or error causes. Write dense, "
    "declarative facts an expert would remember, as tight bullet points. Include "
    "specific names and syntax. Do NOT use navigation phrases like 'this section "
    "covers' or 'refer to'. Only state knowledge grounded in the document; never "
    "invent APIs or facts. Max ~250 words."
)


def _doc_title(rel: str) -> str:
    """Human-ish title from a rel path: last ``#`` segment, extension stripped."""
    last = rel.split("#")[-1]
    for ext in (".md", ".mdx"):
        if last.endswith(ext):
            last = last[: -len(ext)]
    return last or rel


def synthesize_wiki_page(llm: ChatLLM, doc_title: str, full_text: str) -> str:
    """LLM-synthesise one wiki page from a document's full text."""
    full_text = (full_text or "").strip()
    if not full_text:
        return ""
    user = f"Document: {doc_title}\n\n{full_text[:9000]}"
    try:
        out = llm.complete(_WIKI_SYS, user, max_tokens=600)
        if not out:
            out = llm.complete(_WIKI_SYS, user, max_tokens=1200)
        return out
    except Exception:
        logger.warning("synthesize_wiki_page failed for %s", doc_title, exc_info=True)
        return ""


def build_wiki_pages(
    corpus: list[tuple[str, str]],
    llm: ChatLLM,
    *,
    max_workers: int = 16,
) -> list[WikiDoc]:
    """One synthesised wiki page per source document (LLM, parallel)."""

    def one(item: tuple[str, str]) -> WikiDoc | None:
        source_path, text = item
        folder = source_folder(source_path)
        rel = rel_title(source_path)
        title = _doc_title(rel)
        page = synthesize_wiki_page(llm, title, text)
        if not page:
            return None
        return make_wiki_doc(folder, rel, title, page)

    docs: list[WikiDoc] = []
    with ThreadPoolExecutor(max_workers=max_workers) as ex:
        for d in ex.map(one, corpus):
            if d is not None:
                docs.append(d)
    logger.info("build_wiki_pages: %d wiki pages from %d docs", len(docs), len(corpus))
    return docs


def embed_docs(docs: list[WikiDoc], embedder: Embedder) -> None:
    """Populate each doc's ``text_vector`` from its ``chunk`` (in place)."""
    if not docs:
        return
    vectors = embedder.embed([d.chunk for d in docs])
    for d, v in zip(docs, vectors):
        d.text_vector = v
    logger.info("embed_docs: embedded %d docs", len(docs))
