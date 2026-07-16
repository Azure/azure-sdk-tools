"""Lightweight knowledge-graph edges between wiki nodes.

The entity and concept pages are the graph **nodes**; this module computes the
**edges** (typed links) between them, deterministically and without an LLM:

* **entity ↔ concept** (``belongs-to``): a decorator entity is linked to a
  concept when its token appears in the concept's keyword set (e.g. ``@added`` →
  ``API versioning``).
* **entity ↔ entity** (``co-occurs``): two decorators are linked when they appear
  together in enough documents (shared-document overlap above a threshold).

Edges are written onto each node's ``related_slugs`` field (slugs
``entity:@x`` / ``concept:Y``), so query-time expansion can walk one hop from a
retrieved node to its neighbours — a light graph, no separate store.
"""

from __future__ import annotations

import logging
import re
from collections import defaultdict

from .extraction import _CONCEPT_SEEDS, _DECORATOR_RE, _NON_DECORATORS

logger = logging.getLogger(__name__)

# belongs-to (entity↔concept) outranks raw co-occurrence counts on truncation.
_BELONGS_TO_WEIGHT = 1000


def entity_slug(name: str) -> str:
    return f"entity:{name}"


def concept_slug(name: str) -> str:
    return f"concept:{name}"


def _concept_tokens(keywords: list[str]) -> set[str]:
    """Decorator-like tokens a concept's keywords reference (``@added`` → ``added``)."""
    toks: set[str] = set()
    for kw in keywords:
        for m in _DECORATOR_RE.findall(kw):
            toks.add(m.lstrip("@").lower())
    return toks


def build_edges(
    corpus: list[tuple[str, str]],
    entities: dict[str, list[str]],
    concepts: dict[str, list[str]],
    *,
    entity_cooccur_min_docs: int = 3,
    max_related_per_node: int = 6,
) -> dict[str, list[str]]:
    """Return ``{node_slug: [related_slug, ...]}`` for entity/concept nodes.

    Edges are undirected (added to both endpoints). Each node's neighbour list is
    truncated to ``max_related_per_node`` strongest links.
    """
    entity_names = list(entities.keys())  # e.g. "@added"
    entity_keys = {name: name.lstrip("@").lower() for name in entity_names}

    # ---- entity ↔ concept (keyword membership) ----
    weighted: dict[str, dict[str, int]] = defaultdict(lambda: defaultdict(int))

    def link(a: str, b: str, w: int) -> None:
        weighted[a][b] += w
        weighted[b][a] += w

    for concept, keywords in concepts.items():
        ctoks = _concept_tokens(_CONCEPT_SEEDS.get(concept, keywords))
        cslug = concept_slug(concept)
        for name in entity_names:
            if entity_keys[name] in ctoks:
                # belongs-to is the primary edge — weight it above raw
                # co-occurrence counts so it survives neighbour truncation.
                link(entity_slug(name), cslug, _BELONGS_TO_WEIGHT)

    # ---- entity ↔ entity (shared-document co-occurrence) ----
    doc_entities: list[set[str]] = []
    for _path, text in corpus:
        present = {
            tok.lstrip("@")
            for tok in _DECORATOR_RE.findall(text)
            if tok.lstrip("@").lower() not in _NON_DECORATORS
        }
        # keep only discovered entities (normalise to their canonical "@name")
        canon = {name for name in entity_names if name.lstrip("@") in present}
        if len(canon) >= 2:
            doc_entities.append(canon)

    pair_docs: dict[tuple[str, str], int] = defaultdict(int)
    for ents in doc_entities:
        ordered = sorted(ents)
        for i in range(len(ordered)):
            for j in range(i + 1, len(ordered)):
                pair_docs[(ordered[i], ordered[j])] += 1

    for (a, b), n in pair_docs.items():
        if n >= entity_cooccur_min_docs:
            link(entity_slug(a), entity_slug(b), n)

    # ---- truncate to strongest neighbours ----
    result: dict[str, list[str]] = {}
    for node, nbrs in weighted.items():
        top = sorted(nbrs.items(), key=lambda kv: kv[1], reverse=True)[:max_related_per_node]
        result[node] = [slug for slug, _w in top]
    logger.info(
        "build_edges: %d nodes with edges (%d entity-entity pairs >= %d docs)",
        len(result),
        sum(1 for n in pair_docs.values() if n >= entity_cooccur_min_docs),
        entity_cooccur_min_docs,
    )
    return result
