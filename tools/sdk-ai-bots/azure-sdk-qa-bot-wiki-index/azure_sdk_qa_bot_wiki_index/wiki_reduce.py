"""Reduce phase — aggregate extractions into wiki pages (WeKnora-faithful).

Building blocks shared by the full build and the incremental reconcile:

* :func:`aggregate_groups` — deterministic **alias-aware** merge: entity/concept
  mentions are unioned across documents by canonical name **and aliases** (a
  lightweight stand-in for WeKnora's trigram + LLM dedup), plus a conservative
  fuzzy pass for concepts, so synonyms collapse into one page instead of
  fragmenting. Only groups recurring across ``min_docs`` documents are kept.
* :func:`synthesize_group` — LLM: **compile** (not rewrite) one page body from a
  group's grounded, near-verbatim descriptions (WeKnora ``WikiPageModifyPrompt``
  "you are a COMPILER, not a writer").
* :func:`inject_cross_links` — deterministic: link pages sharing source docs.
* :func:`build_index_page` — deterministic navigation page.
* :func:`reduce_pages` — convenience full-build wrapper.
"""

from __future__ import annotations

import logging
import re
from collections import defaultdict
from dataclasses import dataclass, field
from difflib import SequenceMatcher

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
# Conservative fuzzy-merge threshold for concept canonical keys (entities/
# decorators are never fuzzy-merged — exact symbols must stay distinct).
_CONCEPT_FUZZY_RATIO = 0.9

# WeKnora "COMPILER, not a writer" page-body synthesis (prompts_wiki.go
# WikiPageModifyPrompt): reuse the source's own wording; do not rephrase,
# expand, over-structure, or add rhetorical filler. This keeps aggregated pages
# grounded excerpts rather than keyword-dense generic summaries.
_COMPILE_SYS = (
    "You are a COMPILER, not a writer. You are given grounded facts about ONE "
    "Azure SDK / TypeSpec {kind} ('{name}'), collected verbatim from multiple "
    "documents. Compile them into one wiki page.\n"
    "Rules:\n"
    "- Stay close to the source wording. Reuse the source's own sentences; you "
    "MAY lightly reorder, deduplicate, and join related sentences, but do NOT "
    "rephrase for style, do NOT expand short statements into longer ones, and do "
    "NOT invent transitional sentences.\n"
    "- Do NOT over-structure. Only introduce a heading if the facts clearly form "
    "distinct groups; prefer a flat list of tight declarative bullets.\n"
    "- Do NOT add rhetorical filler (phrases like 'designed to', 'aims to', "
    "'is a powerful') unless literally present in the source.\n"
    "- Keep exact names, signatures, decorators (with @), and syntax verbatim.\n"
    "- Drop duplicates; keep the most specific facts. Max ~200 words."
)

_ENTITY_STRIP_AT = re.compile(r"^@+")
_NONALNUM = re.compile(r"[^a-z0-9]+")
_WS = re.compile(r"\s+")


@dataclass
class Group:
    """An aggregated entity/concept group (pre-synthesis)."""

    page_type: str
    name: str
    source_refs: list[str]
    descriptions: list[str] = field(default_factory=list)
    aliases: list[str] = field(default_factory=list)

    def slug(self) -> str:
        return make_slug(self.page_type, self.name)


def _entity_key(name: str) -> str:
    """Normalized merge key for an entity surface form (@-insensitive)."""
    n = _ENTITY_STRIP_AT.sub("", name.strip().lower())
    return _WS.sub(" ", n).strip()


def _concept_key(name: str) -> str:
    """Normalized merge key for a concept surface form."""
    return _NONALNUM.sub(" ", name.strip().lower()).strip()


class _DSU:
    """Tiny union-find over integer indices."""

    def __init__(self, n: int):
        self.parent = list(range(n))

    def find(self, x: int) -> int:
        while self.parent[x] != x:
            self.parent[x] = self.parent[self.parent[x]]
            x = self.parent[x]
        return x

    def union(self, a: int, b: int) -> None:
        ra, rb = self.find(a), self.find(b)
        if ra != rb:
            self.parent[max(ra, rb)] = min(ra, rb)


def _canonical_name(items: list[ExtractedItem]) -> str:
    counts: dict[str, int] = defaultdict(int)
    for it in items:
        counts[it.name] += 1
    # most frequent; tie-break to the shortest (usually the canonical form)
    return max(counts.items(), key=lambda kv: (kv[1], -len(kv[0])))[0]


def _grounded_texts(items: list[ExtractedItem]) -> list[str]:
    """Dedup'd grounded facts (description, then details) for compilation."""
    out, seen = [], set()
    for it in items:
        for t in (it.description, it.details):
            t = (t or "").strip()
            k = t.lower()
            if t and k not in seen:
                seen.add(k)
                out.append(t)
    return out


def _merge_by_alias(items: list[ExtractedItem], key_fn) -> list[list[ExtractedItem]]:
    """Union items sharing any surface form (canonical name or an alias)."""
    dsu = _DSU(len(items))
    surface_rep: dict[str, int] = {}
    for i, it in enumerate(items):
        surfaces = [it.name, *it.aliases]
        for surf in surfaces:
            k = key_fn(surf)
            if not k:
                continue
            if k in surface_rep:
                dsu.union(i, surface_rep[k])
            else:
                surface_rep[k] = i
    groups: dict[int, list[ExtractedItem]] = defaultdict(list)
    for i, it in enumerate(items):
        groups[dsu.find(i)].append(it)
    return list(groups.values())


def _fuzzy_merge_concepts(groups: list[Group]) -> list[Group]:
    """Conservatively merge concept groups with near-identical canonical keys."""
    if len(groups) < 2:
        return groups
    keys = [_concept_key(g.name) for g in groups]
    dsu = _DSU(len(groups))
    for i in range(len(groups)):
        ti = set(keys[i].split())
        for j in range(i + 1, len(groups)):
            tj = set(keys[j].split())
            if not ti or not tj:
                continue
            # require token overlap before the (cheap) ratio check to stay safe
            if not (ti & tj):
                continue
            if SequenceMatcher(None, keys[i], keys[j]).ratio() >= _CONCEPT_FUZZY_RATIO:
                dsu.union(i, j)
    merged: dict[int, Group] = {}
    for i, g in enumerate(groups):
        r = dsu.find(i)
        if r not in merged:
            merged[r] = Group(g.page_type, g.name, list(g.source_refs),
                              list(g.descriptions), list(g.aliases))
        else:
            m = merged[r]
            m.source_refs = sorted(set(m.source_refs) | set(g.source_refs))
            for d in g.descriptions:
                if d not in m.descriptions:
                    m.descriptions.append(d)
            for a in g.aliases:
                if a not in m.aliases:
                    m.aliases.append(a)
    return list(merged.values())


def aggregate_groups(
    extractions: list[DocExtraction], *, min_docs: int = 2, fuzzy: bool = True
) -> list[Group]:
    """Alias-aware group entity/concept mentions into cross-document groups."""
    groups: list[Group] = []
    for kind, page_type, key_fn in (
        ("entities", PAGE_ENTITY, _entity_key),
        ("concepts", PAGE_CONCEPT, _concept_key),
    ):
        items: list[ExtractedItem] = []
        for d in extractions:
            items.extend(getattr(d, kind))
        for cluster in _merge_by_alias(items, key_fn):
            refs = sorted({it.source_ref for it in cluster if it.source_ref})
            if len(refs) < min_docs:
                continue
            aliases: list[str] = []
            for it in cluster:
                for a in (it.name, *it.aliases):
                    if a and a not in aliases:
                        aliases.append(a)
            groups.append(
                Group(
                    page_type=page_type,
                    name=_canonical_name(cluster),
                    source_refs=refs,
                    descriptions=_grounded_texts(cluster),
                    aliases=aliases,
                )
            )
    if fuzzy:
        concepts = _fuzzy_merge_concepts([g for g in groups if g.page_type == PAGE_CONCEPT])
        groups = [g for g in groups if g.page_type != PAGE_CONCEPT] + concepts
    logger.info("aggregate_groups: %d groups (min_docs=%d, fuzzy=%s)", len(groups), min_docs, fuzzy)
    return groups


def synthesize_group(llm: ChatLLM, group: Group) -> str:
    """LLM: COMPILE one page body from a group's grounded descriptions."""
    body = "\n".join(f"- {d}" for d in group.descriptions)
    if not body:
        return ""
    kind = "concept" if group.page_type == PAGE_CONCEPT else "entity/symbol"
    system = _COMPILE_SYS.format(kind=kind, name=group.name)
    user = f"Name: {group.name}\n\nGrounded facts from documents:\n{body[:_MAX_REDUCE_INPUT]}"
    try:
        out = llm.complete(system, user, max_tokens=520)
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
    """Full-build convenience: aggregate -> compile all -> cross-link -> index."""
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
