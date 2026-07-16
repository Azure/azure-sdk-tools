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
from .graph import build_edges, concept_slug, entity_slug
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
    return build_graph_pages(corpus, synth, want_entity=True, want_concept=False, max_workers=max_workers)


def build_concept_pages(
    corpus: list[tuple[str, str]],
    synth: Synthesizer,
    *,
    max_workers: int = 8,
) -> list[WikiDoc]:
    return build_graph_pages(corpus, synth, want_entity=False, want_concept=True, max_workers=max_workers)


def build_graph_pages(
    corpus: list[tuple[str, str]],
    synth: Synthesizer,
    *,
    want_entity: bool = True,
    want_concept: bool = True,
    max_workers: int = 12,
) -> list[WikiDoc]:
    """Build entity and/or concept pages, wiring lightweight graph edges into
    each node's ``related_slugs`` (only links to node types also being built)."""
    entities = discover_entities(corpus)
    concepts = discover_concepts(corpus)
    edges = build_edges(corpus, entities, concepts)

    def neighbours(slug: str) -> list[str]:
        out = []
        for nbr in edges.get(slug, []):
            if nbr.startswith("entity:") and want_entity:
                out.append(nbr)
            elif nbr.startswith("concept:") and want_concept:
                out.append(nbr)
        return out

    docs: list[WikiDoc] = []

    if want_entity:
        def one_entity(item: tuple[str, list[str]]) -> WikiDoc | None:
            name, excerpts = item
            page = synth.entity_page(name, excerpts)
            if not page:
                return None
            return make_entity_doc(name, page, source_refs=[], related=neighbours(entity_slug(name)))

        with ThreadPoolExecutor(max_workers=max_workers) as ex:
            for d in ex.map(one_entity, list(entities.items())):
                if d is not None:
                    docs.append(d)

    if want_concept:
        def one_concept(item: tuple[str, list[str]]) -> WikiDoc | None:
            name, excerpts = item
            page = synth.concept_page(name, excerpts)
            if not page:
                return None
            return make_concept_doc(name, page, source_refs=[], related=neighbours(concept_slug(name)))

        with ThreadPoolExecutor(max_workers=max(4, max_workers // 2)) as ex:
            for d in ex.map(one_concept, list(concepts.items())):
                if d is not None:
                    docs.append(d)

    logger.info("build_graph_pages: %d nodes (entity=%s concept=%s)", len(docs), want_entity, want_concept)
    return docs


def embed_docs(docs: list[WikiDoc], embedder: Embedder) -> None:
    """Populate each doc's ``text_vector`` from its ``chunk`` (in place)."""
    if not docs:
        return
    vectors = embedder.embed([d.chunk for d in docs])
    for d, v in zip(docs, vectors):
        d.text_vector = v
    logger.info("embed_docs: embedded %d docs", len(docs))
