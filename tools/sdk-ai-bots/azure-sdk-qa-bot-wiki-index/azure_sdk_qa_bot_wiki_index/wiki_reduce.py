"""Reduce phase — aggregate extractions into wiki pages (WeKnora-faithful).

Broken into reusable building blocks so both the full build and the incremental
reconcile can share them:

* :func:`aggregate_groups` — deterministic: group per-document mentions by
  normalized name into entity/concept :class:`Group`s that recur across at least
  ``min_docs`` documents.
* :func:`synthesize_group` — LLM: synthesise one page body from a group's
  grounded descriptions.
* :func:`inject_cross_links` — deterministic: link pages sharing source docs
  (WeKnora ``injectCrossLinks``); drop dead links.
* :func:`build_index_page` — deterministic navigation page.
* :func:`reduce_pages` — convenience full-build wrapper (aggregate → synthesise
  all → cross-link → index).
"""

from __future__ import annotations

import logging
import re
from collections import defaultdict
from dataclasses import dataclass, field

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
_SYS_BY_TYPE = {PAGE_ENTITY: _ENTITY_SYS, PAGE_CONCEPT: _CONCEPT_SYS}

_ENTITY_KEY_RE = re.compile(r"\s+")


@dataclass
class Group:
    """An aggregated entity/concept group (pre-synthesis)."""

    page_type: str
    name: str
    source_refs: list[str]
    descriptions: list[str] = field(default_factory=list)

    def slug(self) -> str:
        return make_slug(self.page_type, self.name)


def _entity_key(name: str) -> str:
    return _ENTITY_KEY_RE.sub(" ", name.strip().lower())


def _concept_key(name: str) -> str:
    return re.sub(r"[^a-z0-9 ]+", "", name.strip().lower()).strip()


def _canonical_name(items: list[ExtractedItem]) -> str:
    counts: dict[str, int] = defaultdict(int)
    for it in items:
        counts[it.name] += 1
    return max(counts.items(), key=lambda kv: kv[1])[0]


def _dedup_descriptions(items: list[ExtractedItem]) -> list[str]:
    out, seen = [], set()
    for it in items:
        d = it.description.strip()
        if d and d.lower() not in seen:
            seen.add(d.lower())
            out.append(d)
    return out


def aggregate_groups(extractions: list[DocExtraction], *, min_docs: int = 2) -> list[Group]:
    """Deterministically group entity/concept mentions into cross-document groups."""
    groups: list[Group] = []
    for kind, page_type, key_fn in (
        ("entities", PAGE_ENTITY, _entity_key),
        ("concepts", PAGE_CONCEPT, _concept_key),
    ):
        buckets: dict[str, list[ExtractedItem]] = defaultdict(list)
        for d in extractions:
            for it in getattr(d, kind):
                buckets[key_fn(it.name)].append(it)
        for _key, items in buckets.items():
            refs = sorted({it.source_ref for it in items if it.source_ref})
            if len(refs) < min_docs:
                continue
            groups.append(
                Group(
                    page_type=page_type,
                    name=_canonical_name(items),
                    source_refs=refs,
                    descriptions=_dedup_descriptions(items),
                )
            )
    logger.info("aggregate_groups: %d groups (min_docs=%d)", len(groups), min_docs)
    return groups


def synthesize_group(llm: ChatLLM, group: Group) -> str:
    """LLM: synthesise one page body from a group's grounded descriptions."""
    body = "\n".join(f"- {d}" for d in group.descriptions)
    if not body:
        return ""
    user = f"Name: {group.name}\n\nDescriptions from documents:\n{body[:_MAX_REDUCE_INPUT]}"
    try:
        out = llm.complete(_SYS_BY_TYPE[group.page_type], user, max_tokens=520)
        return (out or "")[:_MAX_PAGE_CHARS]
    except Exception:
        logger.warning("synthesize_group failed for %s", group.name, exc_info=True)
        return ""


def group_to_page(group: Group, body: str) -> WikiPage:
    return WikiPage(
        slug=group.slug(),
        page_type=group.page_type,
        title=group.name,
        content=body,
        context_id=CONTEXT_BY_TYPE[group.page_type],
        source_refs=group.source_refs,
    )


def inject_cross_links(pages: list[WikiPage]) -> None:
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


def build_index_page(pages: list[WikiPage]) -> WikiPage | None:
    """A navigation page listing generated entity/concept pages."""
    entities = sorted(p.title for p in pages if p.page_type == PAGE_ENTITY)
    concepts = sorted(p.title for p in pages if p.page_type == PAGE_CONCEPT)
    if not entities and not concepts:
        return None
    lines = ["# Knowledge wiki index", ""]
    if concepts:
        lines += ["## Concepts", *[f"- {c}" for c in concepts], ""]
    if entities:
        lines += ["## Entities", *[f"- {e}" for e in entities]]
    content = "\n".join(lines)[:_MAX_PAGE_CHARS]
    return WikiPage(
        slug=f"{PAGE_INDEX}/knowledge-wiki",
        page_type=PAGE_INDEX,
        title="Knowledge wiki index",
        content=content,
        context_id=CONTEXT_BY_TYPE[PAGE_INDEX],
        out_links=[p.slug for p in pages if p.page_type in (PAGE_ENTITY, PAGE_CONCEPT)][:_MAX_OUT_LINKS],
    )


def reduce_pages(
    extractions: list[DocExtraction],
    llm: ChatLLM,
    *,
    min_docs: int = 2,
) -> list[WikiPage]:
    """Full-build convenience: aggregate → synthesise all → cross-link → index."""
    pages: list[WikiPage] = []
    for group in aggregate_groups(extractions, min_docs=min_docs):
        body = synthesize_group(llm, group)
        if body:
            pages.append(group_to_page(group, body))
    inject_cross_links(pages)
    index = build_index_page(pages)
    if index is not None:
        pages.append(index)
    logger.info("reduce_pages: %d cross-document pages", len(pages))
    return pages
