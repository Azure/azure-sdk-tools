"""Graph creation — LLM entity + relationship extraction (WeKnora-faithful).

Mirrors WeKnora's ``graph.go``:

* :func:`extract_entities` — a per-document LLM call identifies entities
  (decorators, APIs, types, concepts) as ``{title, type, description}``. Entities
  are deduplicated by title; ``chunk_ids`` (source docs) and ``frequency``
  accumulate across the corpus. (WeKnora: ``extractEntities``, concurrency 4.)
* :func:`extract_relationships` — documents are processed in batches of 5; each
  batch's entities + text are sent to the LLM to extract directed relationships
  ``{source, target, description, strength(1-10)}``. Duplicate relationships
  (same source#target) merge via a strength weighted-average. (WeKnora:
  ``extractRelationships``, batch size 5, concurrency 4.)

Extraction is intentionally LLM-based (not regex) to match WeKnora exactly. The
downstream weighting (PMI + strength) lives in :mod:`graph_weights`.
"""

from __future__ import annotations

import logging
from concurrent.futures import ThreadPoolExecutor
from dataclasses import dataclass, field

from .llm import ChatLLM
from .reader import rel_title

logger = logging.getLogger(__name__)

# WeKnora constants (graph.go).
MAX_CONCURRENT_ENTITY_EXTRACTIONS = 4
MAX_CONCURRENT_RELATION_EXTRACTIONS = 4
RELATION_BATCH_SIZE = 5
MIN_ENTITIES_FOR_RELATION = 2

_ENTITY_SYS = (
    "You extract the key ENTITIES from one Azure SDK / TypeSpec document. An "
    "entity is a concrete, reusable named thing: a decorator (e.g. `@added`), an "
    "API/operation, a type/model (e.g. `TrackedResource`), or a well-defined "
    "concept (e.g. `API versioning`). Return ONLY a JSON array; each element is "
    '{"title": "<canonical name, keep @ for decorators>", "type": '
    '"decorator|api|type|concept", "description": "<one dense sentence on what '
    'it is and does, grounded in the document>"}. Extract at most 15 of the most '
    "important entities. Do not invent entities not present in the text. No prose "
    "outside the JSON array."
)

_RELATION_SYS = (
    "You extract directed RELATIONSHIPS between the given Azure SDK / TypeSpec "
    "entities, grounded in the provided document excerpts. Return ONLY a JSON "
    'array; each element is {"source": "<entity title>", "target": "<entity '
    'title>", "description": "<how source relates to/affects target>", '
    '"strength": <integer 1-10, importance of the relationship>}. Only use '
    "entity titles from the provided list. Only assert relationships supported by "
    "the text. Prefer specific, load-bearing relationships over generic ones. No "
    "prose outside the JSON array."
)


@dataclass
class Entity:
    title: str
    type: str = ""
    description: str = ""
    chunk_ids: set[str] = field(default_factory=set)
    frequency: int = 0
    degree: int = 0


@dataclass
class Relationship:
    source: str
    target: str
    description: str = ""
    strength: int = 1
    chunk_ids: set[str] = field(default_factory=set)
    weight: float = 0.0
    combined_degree: int = 0


def _norm_title(t: str) -> str:
    return (t or "").strip()


def extract_entities(
    corpus: list[tuple[str, str]],
    llm: ChatLLM,
    *,
    max_workers: int = MAX_CONCURRENT_ENTITY_EXTRACTIONS,
) -> dict[str, Entity]:
    """Per-document LLM entity extraction, merged into a title→Entity map."""

    def one(item: tuple[str, str]) -> tuple[str, list[dict]]:
        source_path, text = item
        rel = rel_title(source_path)
        text = (text or "").strip()
        if not text:
            return rel, []
        user = f"Document: {rel}\n\n{text[:9000]}"
        try:
            parsed = llm.complete_json(_ENTITY_SYS, user, max_tokens=800)
        except Exception:
            logger.warning("extract_entities failed for %s", rel, exc_info=True)
            parsed = None
        if not isinstance(parsed, list):
            return rel, []
        return rel, [e for e in parsed if isinstance(e, dict)]

    merged: dict[str, Entity] = {}
    with ThreadPoolExecutor(max_workers=max_workers) as ex:
        for rel, ents in ex.map(one, corpus):
            for e in ents:
                title = _norm_title(e.get("title", ""))
                if not title:
                    continue
                ent = merged.get(title)
                if ent is None:
                    ent = Entity(title=title, type=str(e.get("type", "")).strip())
                    merged[title] = ent
                if not ent.description:
                    ent.description = str(e.get("description", "")).strip()
                ent.chunk_ids.add(rel)
                ent.frequency += 1
    logger.info(
        "extract_entities: %d unique entities from %d docs", len(merged), len(corpus)
    )
    return merged


def _chunk_slices(seq: list, size: int):
    for i in range(0, len(seq), size):
        yield seq[i : i + size]


def extract_relationships(
    corpus: list[tuple[str, str]],
    entities: dict[str, Entity],
    llm: ChatLLM,
    *,
    batch_size: int = RELATION_BATCH_SIZE,
    max_workers: int = MAX_CONCURRENT_RELATION_EXTRACTIONS,
) -> dict[tuple[str, str], Relationship]:
    """Per-batch LLM relationship extraction, merged by (source, target).

    Each batch of ``batch_size`` documents is paired with the entities that occur
    in those documents; the LLM proposes relationships between them.
    """
    titles = set(entities.keys())

    def entities_in(rels: list[str]) -> list[str]:
        found = []
        for t, ent in entities.items():
            if ent.chunk_ids & set(rels):
                found.append(t)
        return found

    batches: list[tuple[list[str], str, list[str]]] = []
    for batch in _chunk_slices(corpus, batch_size):
        rels = [rel_title(sp) for sp, _ in batch]
        batch_entities = entities_in(rels)
        if len(batch_entities) < MIN_ENTITIES_FOR_RELATION:
            continue
        merged_text = "\n\n---\n\n".join((t or "")[:2500] for _sp, t in batch)
        batches.append((rels, merged_text, batch_entities))

    def one(item: tuple[list[str], str, list[str]]) -> list[tuple[str, str, dict]]:
        rels, text, batch_entities = item
        ent_list = "\n".join(f"- {t}" for t in batch_entities)
        user = (
            f"Entities:\n{ent_list}\n\nDocument excerpts:\n{text[:9000]}"
        )
        try:
            parsed = llm.complete_json(_RELATION_SYS, user, max_tokens=900)
        except Exception:
            logger.warning("extract_relationships batch failed", exc_info=True)
            parsed = None
        out = []
        if isinstance(parsed, list):
            for r in parsed:
                if isinstance(r, dict):
                    out.append((rels, r))  # type: ignore[arg-type]
        return out  # type: ignore[return-value]

    merged: dict[tuple[str, str], Relationship] = {}
    with ThreadPoolExecutor(max_workers=max_workers) as ex:
        for batch_out in ex.map(one, batches):
            for rels, r in batch_out:  # type: ignore[misc]
                src = _norm_title(r.get("source", ""))
                tgt = _norm_title(r.get("target", ""))
                if not src or not tgt or src == tgt:
                    continue
                if src not in titles or tgt not in titles:
                    continue
                strength = _coerce_strength(r.get("strength", 1))
                key = (src, tgt)
                rel = merged.get(key)
                if rel is None:
                    rel = Relationship(
                        source=src,
                        target=tgt,
                        description=str(r.get("description", "")).strip(),
                        strength=strength,
                    )
                    merged[key] = rel
                else:
                    # weighted-average strength across occurrences (WeKnora)
                    n = len(rel.chunk_ids) or 1
                    rel.strength = (rel.strength * n + strength) // (n + 1)
                rel.chunk_ids.update(rels)  # type: ignore[arg-type]
    logger.info("extract_relationships: %d relationships", len(merged))
    return merged


def _coerce_strength(v) -> int:
    try:
        s = int(round(float(v)))
    except (TypeError, ValueError):
        s = 1
    return max(1, min(10, s))
