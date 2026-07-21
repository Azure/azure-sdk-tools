"""Map phase — per-document extraction of entities + concepts (WeKnora-faithful).

Mirrors WeKnora's combined entity/concept extraction (``wiki_ingest.go``
``combinedExtraction``): one LLM call per document returns the salient
**entities** (decorators/APIs/types) and **concepts** (cross-cutting topics) the
document discusses, each with a grounded one-line description and the excerpt the
reduce phase will synthesise from.

The per-document **summary** page is produced separately by :mod:`wiki`.
"""

from __future__ import annotations

import logging
from concurrent.futures import ThreadPoolExecutor
from dataclasses import dataclass, field

from .llm import ChatLLM
from .reader import rel_title

logger = logging.getLogger(__name__)

# WeKnora concurrency for the map phase.
MAX_CONCURRENT_EXTRACTIONS = 4

_EXTRACT_SYS = (
    "You read one Azure SDK / TypeSpec document and extract the reusable "
    "knowledge items it teaches, split into ENTITIES and CONCEPTS.\n"
    "- ENTITY = a concrete named symbol: a decorator (keep the leading @, e.g. "
    "`@added`), an API/operation, or a type/model (e.g. `TrackedResource`).\n"
    "- CONCEPT = a cross-cutting topic/methodology (e.g. `API versioning`, "
    "`long-running operations`, `pagination`).\n"
    'Return ONLY JSON: {"entities": [{"name": "<canonical name>", "type": '
    '"decorator|api|type", "description": "<one dense grounded sentence>"}], '
    '"concepts": [{"name": "<topic>", "description": "<one dense grounded '
    'sentence>"}]}. At most 12 entities and 8 concepts, the most important ones. '
    "Do not invent items absent from the text. No prose outside the JSON."
)


@dataclass
class ExtractedItem:
    """One entity or concept mention from a single document."""

    kind: str  # "entity" | "concept"
    name: str
    type: str = ""
    description: str = ""
    source_ref: str = ""
    excerpt: str = ""


@dataclass
class DocExtraction:
    """All items extracted from one document."""

    source_ref: str
    entities: list[ExtractedItem] = field(default_factory=list)
    concepts: list[ExtractedItem] = field(default_factory=list)


def _norm(s: str) -> str:
    return (s or "").strip()


def extract_doc(llm: ChatLLM, source_ref: str, text: str) -> DocExtraction:
    """Run the combined entity+concept extraction for one document."""
    out = DocExtraction(source_ref=source_ref)
    text = (text or "").strip()
    if not text:
        return out
    user = f"Document: {source_ref}\n\n{text[:9000]}"
    try:
        parsed = llm.complete_json(_EXTRACT_SYS, user, max_tokens=1100)
    except Exception:
        logger.warning("extract_doc failed for %s", source_ref, exc_info=True)
        parsed = None
    if not isinstance(parsed, dict):
        return out
    for e in parsed.get("entities", []) or []:
        if isinstance(e, dict) and _norm(e.get("name", "")):
            out.entities.append(
                ExtractedItem(
                    kind="entity",
                    name=_norm(e["name"]),
                    type=_norm(str(e.get("type", ""))),
                    description=_norm(str(e.get("description", ""))),
                    source_ref=source_ref,
                )
            )
    for c in parsed.get("concepts", []) or []:
        if isinstance(c, dict) and _norm(c.get("name", "")):
            out.concepts.append(
                ExtractedItem(
                    kind="concept",
                    name=_norm(c["name"]),
                    description=_norm(str(c.get("description", ""))),
                    source_ref=source_ref,
                )
            )
    return out


def map_extract(
    corpus: list[tuple[str, str]],
    llm: ChatLLM,
    *,
    max_workers: int = MAX_CONCURRENT_EXTRACTIONS,
) -> list[DocExtraction]:
    """Map phase: extract entities+concepts from every document (parallel)."""

    def one(item: tuple[str, str]) -> DocExtraction:
        source_path, text = item
        return extract_doc(llm, rel_title(source_path), text)

    results: list[DocExtraction] = []
    with ThreadPoolExecutor(max_workers=max_workers) as ex:
        for d in ex.map(one, corpus):
            results.append(d)
    n_ent = sum(len(d.entities) for d in results)
    n_con = sum(len(d.concepts) for d in results)
    logger.info(
        "map_extract: %d docs -> %d entity mentions, %d concept mentions",
        len(results),
        n_ent,
        n_con,
    )
    return results
