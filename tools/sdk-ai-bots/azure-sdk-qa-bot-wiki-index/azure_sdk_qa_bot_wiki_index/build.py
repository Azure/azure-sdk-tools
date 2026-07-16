"""Build orchestration — generate wiki pages and push them into the KB index."""

from __future__ import annotations

import logging
from concurrent.futures import ThreadPoolExecutor

from .documents import (
    WikiDoc,
    make_concept_doc,
    make_entity_doc,
    make_summary_doc,
)
from .extraction import discover_concepts, discover_entities
from .reader import rel_title, source_folder
from .synthesis import Embedder, Synthesizer

logger = logging.getLogger(__name__)


def _doc_title(rel: str) -> str:
    """Human-ish title from a rel path: last ``#`` segment, extension stripped."""
    last = rel.split("#")[-1]
    for ext in (".md", ".mdx"):
        if last.endswith(ext):
            last = last[: -len(ext)]
    return last or rel


def build_summary_cards(
    corpus: list[tuple[str, str]],
    synth: Synthesizer,
    *,
    max_workers: int = 16,
) -> list[WikiDoc]:
    """One summary knowledge card per source document (LLM, parallel)."""

    def one(item: tuple[str, str]) -> WikiDoc | None:
        source_path, text = item
        folder = source_folder(source_path)
        rel = rel_title(source_path)
        title = _doc_title(rel)
        card = synth.summary_card(title, text)
        if not card:
            return None
        return make_summary_doc(folder, rel, title, card)

    docs: list[WikiDoc] = []
    with ThreadPoolExecutor(max_workers=max_workers) as ex:
        for d in ex.map(one, corpus):
            if d is not None:
                docs.append(d)
    logger.info("build_summary_cards: %d cards from %d docs", len(docs), len(corpus))
    return docs


def build_entity_pages(
    corpus: list[tuple[str, str]],
    synth: Synthesizer,
    *,
    max_workers: int = 12,
) -> list[WikiDoc]:
    entities = discover_entities(corpus)
    items = list(entities.items())

    def one(item: tuple[str, list[str]]) -> WikiDoc | None:
        name, excerpts = item
        page = synth.entity_page(name, excerpts)
        if not page:
            return None
        return make_entity_doc(name, page, source_refs=[], related=[])

    docs: list[WikiDoc] = []
    with ThreadPoolExecutor(max_workers=max_workers) as ex:
        for d in ex.map(one, items):
            if d is not None:
                docs.append(d)
    logger.info("build_entity_pages: %d entity pages", len(docs))
    return docs


def build_concept_pages(
    corpus: list[tuple[str, str]],
    synth: Synthesizer,
    *,
    max_workers: int = 8,
) -> list[WikiDoc]:
    concepts = discover_concepts(corpus)
    items = list(concepts.items())

    def one(item: tuple[str, list[str]]) -> WikiDoc | None:
        name, excerpts = item
        page = synth.concept_page(name, excerpts)
        if not page:
            return None
        return make_concept_doc(name, page, source_refs=[], related=[])

    docs: list[WikiDoc] = []
    with ThreadPoolExecutor(max_workers=max_workers) as ex:
        for d in ex.map(one, items):
            if d is not None:
                docs.append(d)
    logger.info("build_concept_pages: %d concept pages", len(docs))
    return docs


def embed_docs(docs: list[WikiDoc], embedder: Embedder) -> None:
    """Populate each doc's ``text_vector`` from its ``chunk`` (in place)."""
    if not docs:
        return
    vectors = embedder.embed([d.chunk for d in docs])
    for d, v in zip(docs, vectors):
        d.text_vector = v
    logger.info("embed_docs: embedded %d docs", len(docs))
