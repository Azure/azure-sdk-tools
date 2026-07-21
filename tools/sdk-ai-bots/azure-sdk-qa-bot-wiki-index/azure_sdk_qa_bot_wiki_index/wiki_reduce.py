"""Reduce phase — aggregate extractions into wiki pages (WeKnora-faithful).

Mirrors WeKnora's wiki reduce/finalize (``wiki_ingest.go``):

1. **Aggregate** the per-document :class:`ExtractedItem`s by normalized name into
   entity / concept groups; a group becomes a page only when it recurs across at
   least ``min_docs`` documents (cross-document requirement).
2. **Synthesize** one coherent page per group via an LLM reduce call over all the
   group's grounded descriptions (``deduplicateExtractedBatch`` + page write).
3. **Cross-link** (``injectCrossLinks``): pages that share source documents, plus
   entity→concept membership, become each other's ``out_links``; dead links to
   non-existent pages are dropped.
4. **Index page**: a navigation page listing the generated entity/concept pages.

Name normalization is the dedup mechanism here (``@Added`` == ``@added``);
embedding-based near-duplicate merging is a possible future refinement.
"""

from __future__ import annotations

import logging
import re
from collections import defaultdict

from .llm import ChatLLM
from .pages import (
    CONTEXT_BY_TYPE,
    PAGE_CONCEPT,
    PAGE_ENTITY,
    PAGE_INDEX,
    WikiPage,
    make_slug,
)
from .wiki_extract import DocExtraction, ExtractedItem

logger = logging.getLogger(__name__)

_MAX_PAGE_CHARS = 1800
_MAX_OUT_LINKS = 8
_MAX_REDUCE_INPUT = 9000

_ENTITY_SYS = (
    "You are writing a cross-document ENTITY wiki page for ONE Azure SDK / "
    "TypeSpec symbol. Given grounded descriptions of it collected from multiple "
    "documents, synthesise everything an expert must know: what it is, exact "
    "signature/usage, when to use it, interactions with related symbols, "
    "constraints, and common mistakes. Merge duplicates; keep the most specific "
    "facts. Tight declarative bullets, exact syntax, no navigation phrases. "
    "Max ~220 words."
)
_CONCEPT_SYS = (
    "You are writing a cross-document CONCEPT wiki page for ONE Azure SDK / "
    "TypeSpec topic. Given grounded descriptions collected from multiple "
    "documents, synthesise the core rules, the decorators/APIs involved, the "
    "correct approach, and the pitfalls an expert knows. Merge duplicates; keep "
    "specific, actionable facts. Tight declarative bullets, exact names/syntax, "
    "no navigation phrases. Max ~220 words."
)

_ENTITY_KEY_RE = re.compile(r"\s+")


def _entity_key(name: str) -> str:
    """Normalized grouping key for an entity (case-insensitive, keeps ``@``)."""
    return _ENTITY_KEY_RE.sub(" ", name.strip().lower())


def _concept_key(name: str) -> str:
    """Normalized grouping key for a concept (case/punct-insensitive)."""
    return re.sub(r"[^a-z0-9 ]+", "", name.strip().lower()).strip()


def _canonical_name(items: list[ExtractedItem]) -> str:
    """Pick the most common surface form as the display name."""
    counts: dict[str, int] = defaultdict(int)
    for it in items:
        counts[it.name] += 1
    return max(counts.items(), key=lambda kv: kv[1])[0]


def _group(items: list[ExtractedItem], key_fn) -> dict[str, list[ExtractedItem]]:
    groups: dict[str, list[ExtractedItem]] = defaultdict(list)
    for it in items:
        groups[key_fn(it.name)].append(it)
    return groups


def _reduce_body(llm: ChatLLM, system: str, name: str, items: list[ExtractedItem]) -> str:
    descriptions = []
    seen = set()
    for it in items:
        d = it.description.strip()
        if d and d.lower() not in seen:
            seen.add(d.lower())
            descriptions.append(f"- {d}")
    body = "\n".join(descriptions)
    if not body:
        return ""
    user = f"Name: {name}\n\nDescriptions from documents:\n{body[:_MAX_REDUCE_INPUT]}"
    try:
        out = llm.complete(system, user, max_tokens=520)
        return (out or "")[:_MAX_PAGE_CHARS]
    except Exception:
        logger.warning("reduce failed for %s", name, exc_info=True)
        return ""


def reduce_pages(
    extractions: list[DocExtraction],
    llm: ChatLLM,
    *,
    min_docs: int = 2,
) -> list[WikiPage]:
    """Aggregate + synthesise entity/concept pages, then cross-link + index."""
    all_entities = [e for d in extractions for e in d.entities]
    all_concepts = [c for d in extractions for c in d.concepts]

    entity_groups = _group(all_entities, _entity_key)
    concept_groups = _group(all_concepts, _concept_key)

    pages: list[WikiPage] = []
    pages += _build_group_pages(
        entity_groups, PAGE_ENTITY, _ENTITY_SYS, llm, min_docs
    )
    pages += _build_group_pages(
        concept_groups, PAGE_CONCEPT, _CONCEPT_SYS, llm, min_docs
    )

    _inject_cross_links(pages)
    index = _build_index_page(pages)
    if index is not None:
        pages.append(index)
    logger.info("reduce_pages: %d cross-document pages", len(pages))
    return pages


def _build_group_pages(
    groups: dict[str, list[ExtractedItem]],
    page_type: str,
    system: str,
    llm: ChatLLM,
    min_docs: int,
) -> list[WikiPage]:
    out: list[WikiPage] = []
    context_id = CONTEXT_BY_TYPE[page_type]
    for _key, items in groups.items():
        source_refs = sorted({it.source_ref for it in items if it.source_ref})
        if len(source_refs) < min_docs:
            continue
        name = _canonical_name(items)
        body = _reduce_body(llm, system, name, items)
        if not body:
            continue
        out.append(
            WikiPage(
                slug=make_slug(page_type, name),
                page_type=page_type,
                title=name,
                content=body,
                context_id=context_id,
                source_refs=source_refs,
            )
        )
    logger.info("_build_group_pages(%s): %d pages (min_docs=%d)", page_type, len(out), min_docs)
    return out


def _inject_cross_links(pages: list[WikiPage]) -> None:
    """Link pages that share source documents (WeKnora injectCrossLinks)."""
    by_doc: dict[str, list[str]] = defaultdict(list)
    for p in pages:
        for ref in p.source_refs:
            by_doc[ref].append(p.slug)

    valid = {p.slug for p in pages}
    for p in pages:
        weighted: dict[str, int] = defaultdict(int)
        for ref in p.source_refs:
            for other in by_doc.get(ref, []):
                if other != p.slug and other in valid:
                    weighted[other] += 1
        ranked = sorted(weighted.items(), key=lambda kv: kv[1], reverse=True)
        p.out_links = [slug for slug, _w in ranked[:_MAX_OUT_LINKS]]


def _build_index_page(pages: list[WikiPage]) -> WikiPage | None:
    """A navigation page listing generated entity/concept pages."""
    if not pages:
        return None
    entities = sorted(p.title for p in pages if p.page_type == PAGE_ENTITY)
    concepts = sorted(p.title for p in pages if p.page_type == PAGE_CONCEPT)
    if not entities and not concepts:
        return None
    lines = ["# Knowledge wiki index", ""]
    if concepts:
        lines.append("## Concepts")
        lines += [f"- {c}" for c in concepts]
        lines.append("")
    if entities:
        lines.append("## Entities")
        lines += [f"- {e}" for e in entities]
    content = "\n".join(lines)[:_MAX_PAGE_CHARS]
    return WikiPage(
        slug=f"{PAGE_INDEX}/knowledge-wiki",
        page_type=PAGE_INDEX,
        title="Knowledge wiki index",
        content=content,
        context_id=CONTEXT_BY_TYPE[PAGE_INDEX],
        out_links=[p.slug for p in pages if p.page_type in (PAGE_ENTITY, PAGE_CONCEPT)][:_MAX_OUT_LINKS],
    )
