"""Map phase for per-document entity and concept extraction."""

from __future__ import annotations

import logging
from concurrent.futures import ThreadPoolExecutor
from dataclasses import dataclass, field

from .llm import ChatLLM

logger = logging.getLogger(__name__)

# Map-phase extraction concurrency.
MAX_CONCURRENT_EXTRACTIONS = 4

# Maximum document characters sent to the extraction LLM call.
_MAX_EXTRACT_CHARS = 12000

GRAN_FOCUSED = "focused"
GRAN_STANDARD = "standard"
GRAN_EXHAUSTIVE = "exhaustive"
_VALID_GRANULARITIES = (GRAN_FOCUSED, GRAN_STANDARD, GRAN_EXHAUSTIVE)

# Granularity guidance used by the extraction prompt.
_GRAN_GUIDANCE = {
    GRAN_FOCUSED: (
        "FOCUSED mode - aggressive pruning. Extract ONLY the document's primary "
        "subjects (what the document is fundamentally about). At most 3-7 items "
        "TOTAL across entities and concepts combined. Ignore everything mentioned "
        "only in passing."
    ),
    GRAN_STANDARD: (
        "STANDARD mode - balanced (default). Extract the document's main subjects "
        "PLUS entities/concepts that are SUBSTANTIVELY discussed - meaning they "
        "have a dedicated paragraph, multiple bullet points, or at least 2-3 "
        "sentences of context.\n"
        "EXCLUDE:\n"
        "- Items mentioned only in a comma-separated list of technologies without "
        "further explanation.\n"
        "- Items whose entire contribution to the document would fit in a single "
        "short sentence.\n"
        "Aim for a tight, curated index. When in doubt about a marginal item, "
        "prefer to EXCLUDE it."
    ),
    GRAN_EXHAUSTIVE: (
        "EXHAUSTIVE mode - maximum recall. Extract every named entity and every "
        "recognizable concept, including decorators, APIs, types, tools, "
        "standards, and methodologies mentioned even once by name."
    ),
}


def normalize_granularity(g: str | None) -> str:
    g = (g or "").strip().lower()
    return g if g in _VALID_GRANULARITIES else GRAN_STANDARD


def _extract_sys(granularity: str) -> str:
    return (
        "You are a knowledge-extraction system reading one Azure SDK / TypeSpec "
        "document. List the significant ENTITIES and key CONCEPTS it teaches.\n"
        "- ENTITY = a concrete named symbol: a decorator (keep the leading @, e.g. "
        "`@added`), an API/operation, or a type/model (e.g. `TrackedResource`).\n"
        "- CONCEPT = a cross-cutting topic or methodology (e.g. `API versioning`, "
        "`long-running operations`, `pagination`).\n\n"
        f"### Extraction scope (granularity)\n{_GRAN_GUIDANCE[granularity]}\n\n"
        "For each item give: a canonical `name`; `aliases` (other names/spellings "
        "used for the SAME thing in this document, so duplicates can be merged - "
        "include abbreviations, the with/without `@`, singular/plural); one "
        "grounded `description` of 15-40 words; and `details` (1-3 sentences, "
        "under 300 chars, a fallback paraphrase).\n"
        'Return ONLY JSON: {"entities":[{"name":"","type":"decorator|api|type",'
        '"aliases":[""],"description":"","details":""}],"concepts":[{"name":"",'
        '"aliases":[""],"description":"","details":""}]}. '
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
    aliases: list[str] = field(default_factory=list)
    details: str = ""


@dataclass
class DocExtraction:
    """All items extracted from one document."""

    source_ref: str
    entities: list[ExtractedItem] = field(default_factory=list)
    concepts: list[ExtractedItem] = field(default_factory=list)
    failed: bool = False


def _norm(s: object) -> str:
    return str(s or "").strip()


def _alias_list(raw: object) -> list[str]:
    if not isinstance(raw, list):
        return []
    out: list[str] = []
    for a in raw:
        a = _norm(a)
        if a and a.lower() not in {x.lower() for x in out}:
            out.append(a)
    return out


def extract_doc(
    llm: ChatLLM, source_ref: str, text: str, *, granularity: str = GRAN_STANDARD
) -> DocExtraction:
    """Run the combined entity+concept extraction for one document."""
    out = DocExtraction(source_ref=source_ref)
    text = (text or "").strip()
    if not text:
        return out
    user = f"Document: {source_ref}\n\n{text[:_MAX_EXTRACT_CHARS]}"
    try:
        parsed = llm.complete_json(_extract_sys(granularity), user, max_tokens=1200)
    except Exception:
        logger.warning("extract_doc failed for %s", source_ref, exc_info=True)
        out.failed = True
        return out
    if not isinstance(parsed, dict):
        return out
    for e in parsed.get("entities", []) or []:
        if isinstance(e, dict) and _norm(e.get("name")):
            out.entities.append(
                ExtractedItem(
                    kind="entity",
                    name=_norm(e.get("name")),
                    type=_norm(e.get("type")),
                    description=_norm(e.get("description")),
                    source_ref=source_ref,
                    aliases=_alias_list(e.get("aliases")),
                    details=_norm(e.get("details")),
                )
            )
    for c in parsed.get("concepts", []) or []:
        if isinstance(c, dict) and _norm(c.get("name")):
            out.concepts.append(
                ExtractedItem(
                    kind="concept",
                    name=_norm(c.get("name")),
                    description=_norm(c.get("description")),
                    source_ref=source_ref,
                    aliases=_alias_list(c.get("aliases")),
                    details=_norm(c.get("details")),
                )
            )
    return out


def map_extract(
    corpus: list[tuple[str, str]],
    llm: ChatLLM,
    *,
    granularity: str = GRAN_STANDARD,
    max_workers: int = MAX_CONCURRENT_EXTRACTIONS,
) -> list[DocExtraction]:
    """Map phase: extract entities+concepts from every document (parallel)."""
    granularity = normalize_granularity(granularity)

    def one(item: tuple[str, str]) -> DocExtraction:
        source_path, text = item
        return extract_doc(llm, source_path, text, granularity=granularity)

    results: list[DocExtraction] = []
    with ThreadPoolExecutor(max_workers=max_workers) as ex:
        for d in ex.map(one, corpus):
            results.append(d)
    n_ent = sum(len(d.entities) for d in results)
    n_con = sum(len(d.concepts) for d in results)
    logger.info(
        "map_extract[%s]: %d docs -> %d entity mentions, %d concept mentions",
        granularity, len(results), n_ent, n_con,
    )
    return results
