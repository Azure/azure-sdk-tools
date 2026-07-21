"""Graph weighting — PMI + Strength, degree, and edge projection (WeKnora-faithful).

Mirrors WeKnora's ``graph.go`` post-extraction math:

* :func:`compute_weights` — every relationship gets a weight combining
  **PMI** (point-wise mutual information over co-occurrence) and the LLM
  **strength**::

      PMI          = max(log2( P(x,y) / (P(x)·P(y)) ), 0)
      weight       = 1 + 9 · (0.6·normPMI + 0.4·normStrength)   → range [1, 10]

  (WeKnora: ``PMIWeight=0.6``, ``StrengthWeight=0.4``, ``WeightScaleFactor=9``.)
* :func:`compute_degrees` — entity degree = in-degree + out-degree; each
  relationship's ``combined_degree`` = source.degree + target.degree.
* :func:`build_entity_edges` — projects the entity/relationship graph into an
  ``entity → [neighbour]`` adjacency (analogous to WeKnora's chunk-to-chunk
  projection) using **1-hop** direct edges plus **2-hop** indirect edges with
  ``IndirectRelationWeightDecay=0.5``, ranked by weight then degree.
"""

from __future__ import annotations

import logging
import math
from collections import defaultdict

from .graph_extract import Entity, Relationship

logger = logging.getLogger(__name__)

# WeKnora constants (graph.go).
PMI_WEIGHT = 0.6
STRENGTH_WEIGHT = 0.4
WEIGHT_SCALE_FACTOR = 9.0
INDIRECT_RELATION_WEIGHT_DECAY = 0.5
MIN_WEIGHT_VALUE = 1.0


def compute_weights(
    entities: dict[str, Entity],
    relationships: dict[tuple[str, str], Relationship],
    total_chunks: int,
) -> None:
    """Set ``relationship.weight`` from PMI + strength (in place, WeKnora formula)."""
    if not relationships or total_chunks <= 0:
        for rel in relationships.values():
            rel.weight = MIN_WEIGHT_VALUE
        return

    n = float(total_chunks)
    pmi_values: dict[tuple[str, str], float] = {}
    max_pmi = 0.0
    max_strength = 0.0

    for key, rel in relationships.items():
        src = entities.get(rel.source)
        tgt = entities.get(rel.target)
        if src is None or tgt is None:
            pmi_values[key] = 0.0
            continue
        p_x = (len(src.chunk_ids) or 1) / n
        p_y = (len(tgt.chunk_ids) or 1) / n
        p_xy = (len(rel.chunk_ids) or 1) / n
        denom = p_x * p_y
        pmi = math.log2(p_xy / denom) if denom > 0 and p_xy > 0 else 0.0
        pmi = max(pmi, 0.0)
        pmi_values[key] = pmi
        max_pmi = max(max_pmi, pmi)
        max_strength = max(max_strength, float(rel.strength))

    for key, rel in relationships.items():
        norm_pmi = (pmi_values[key] / max_pmi) if max_pmi > 0 else 0.0
        norm_strength = (float(rel.strength) / max_strength) if max_strength > 0 else 0.0
        combined = PMI_WEIGHT * norm_pmi + STRENGTH_WEIGHT * norm_strength
        rel.weight = MIN_WEIGHT_VALUE + WEIGHT_SCALE_FACTOR * combined  # [1, 10]
    logger.info("compute_weights: weighted %d relationships (max_pmi=%.3f)", len(relationships), max_pmi)


def compute_degrees(
    entities: dict[str, Entity],
    relationships: dict[tuple[str, str], Relationship],
) -> None:
    """Set entity degree (in+out) and each relationship's combined_degree (in place)."""
    in_deg: dict[str, int] = defaultdict(int)
    out_deg: dict[str, int] = defaultdict(int)
    for rel in relationships.values():
        out_deg[rel.source] += 1
        in_deg[rel.target] += 1
    for title, ent in entities.items():
        ent.degree = in_deg[title] + out_deg[title]
    for rel in relationships.values():
        s = entities.get(rel.source)
        t = entities.get(rel.target)
        rel.combined_degree = (s.degree if s else 0) + (t.degree if t else 0)


def build_entity_edges(
    entities: dict[str, Entity],
    relationships: dict[tuple[str, str], Relationship],
    *,
    max_neighbors: int = 6,
    decay: float = INDIRECT_RELATION_WEIGHT_DECAY,
) -> dict[str, list[str]]:
    """Project relationships into an ``entity → [neighbour title]`` adjacency.

    Combines **1-hop** edges (direct relationships, weight = rel.weight) and
    **2-hop** edges (neighbour-of-neighbour, weight = w1·w2·decay). Each entity's
    neighbour list is ranked by (weight, degree) and truncated to
    ``max_neighbors`` — mirroring WeKnora's ``GetRelationChunks`` /
    ``GetIndirectRelationChunks``.
    """
    # Undirected 1-hop adjacency with weight + degree.
    direct: dict[str, dict[str, tuple[float, int]]] = defaultdict(dict)
    for rel in relationships.values():
        for a, b in ((rel.source, rel.target), (rel.target, rel.source)):
            prev = direct[a].get(b)
            cand = (rel.weight, rel.combined_degree)
            if prev is None or cand[0] > prev[0]:
                direct[a][b] = cand

    # 2-hop indirect edges with decay (skip direct neighbours and self).
    combined: dict[str, dict[str, tuple[float, int]]] = {
        a: dict(nbrs) for a, nbrs in direct.items()
    }
    for a, nbrs in direct.items():
        for b, (w1, _d1) in nbrs.items():
            for c, (w2, d2) in direct.get(b, {}).items():
                if c == a or c in direct.get(a, {}):
                    continue
                iw = w1 * w2 * decay
                prev = combined[a].get(c)
                if prev is None or iw > prev[0]:
                    combined[a][c] = (iw, d2)

    result: dict[str, list[str]] = {}
    for a, nbrs in combined.items():
        ranked = sorted(nbrs.items(), key=lambda kv: (kv[1][0], kv[1][1]), reverse=True)
        result[a] = [b for b, _ in ranked[:max_neighbors]]
    logger.info("build_entity_edges: adjacency for %d entities", len(result))
    return result
