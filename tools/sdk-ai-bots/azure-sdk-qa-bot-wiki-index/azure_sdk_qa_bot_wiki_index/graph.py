"""Graph creation pipeline — orchestration (WeKnora-faithful, independent of wiki).

End-to-end graph build, mirroring WeKnora's ``BuildGraph``:

1. :func:`extract_entities` — per-document LLM entity extraction.
2. :func:`extract_relationships` — per-batch LLM relationship extraction (strength).
3. :func:`compute_weights` — PMI + strength → weight in [1, 10].
4. :func:`compute_degrees` — entity in/out degree; relationship combined degree.
5. :func:`build_entity_edges` — 1-hop + 2-hop(decay 0.5) adjacency.
6. Emit ``entity`` pages (with graph-edge ``related_slugs``) and ``relationship``
   pages into the shared index — the two graph artifact types WeKnora indexes
   (``ChunkTypeEntity`` / ``ChunkTypeRelationship``).

This pipeline shares nothing with :mod:`wiki` except the LLM/embedding clients.
"""

from __future__ import annotations

import logging

from .documents import (
    WikiDoc,
    entity_slug,
    make_entity_doc,
    make_relationship_doc,
)
from .graph_extract import extract_entities, extract_relationships
from .graph_weights import build_entity_edges, compute_degrees, compute_weights
from .llm import ChatLLM

logger = logging.getLogger(__name__)


def build_graph(
    corpus: list[tuple[str, str]],
    llm: ChatLLM,
    *,
    max_neighbors: int = 6,
) -> list[WikiDoc]:
    """Run the full graph build and return entity + relationship page docs."""
    entities = extract_entities(corpus, llm)
    if not entities:
        logger.warning("build_graph: no entities extracted")
        return []

    relationships = extract_relationships(corpus, entities, llm)
    compute_weights(entities, relationships, total_chunks=len(corpus))
    compute_degrees(entities, relationships)
    edges = build_entity_edges(entities, relationships, max_neighbors=max_neighbors)

    # Relationship endpoints linked to each entity (added to related_slugs).
    rel_endpoints: dict[str, list[tuple[str, str]]] = {}
    for (src, tgt), rel in relationships.items():
        rel_endpoints.setdefault(src, []).append((src, tgt))
        rel_endpoints.setdefault(tgt, []).append((src, tgt))

    docs: list[WikiDoc] = []

    # Entity pages — related_slugs = neighbour entity slugs (graph adjacency).
    for title, ent in entities.items():
        neighbours = [entity_slug(n) for n in edges.get(title, [])]
        docs.append(
            make_entity_doc(
                title,
                ent.description,
                entity_type=ent.type,
                source_refs=sorted(ent.chunk_ids),
                related=neighbours,
            )
        )

    # Relationship pages — one per extracted relationship.
    for (src, tgt), rel in relationships.items():
        docs.append(
            make_relationship_doc(
                src,
                tgt,
                rel.description,
                strength=rel.strength,
                weight=rel.weight,
                source_refs=sorted(rel.chunk_ids),
            )
        )

    logger.info(
        "build_graph: %d entity + %d relationship pages",
        len(entities),
        len(relationships),
    )
    return docs
