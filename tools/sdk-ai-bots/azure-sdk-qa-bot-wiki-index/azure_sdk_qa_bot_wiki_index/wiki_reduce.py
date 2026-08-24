"""Reduce phase for aggregating extractions into generated wiki pages."""

from __future__ import annotations

import logging
import re
from collections import defaultdict
from dataclasses import dataclass, field
from difflib import SequenceMatcher

from openai import OpenAIError

from .llm import ChatLLM, load_prompt
from .pages import (
    CONTEXT_BY_TYPE,
    PAGE_CONCEPT,
    PAGE_ENTITY,
    WikiPage,
    make_slug,
)
from .wiki_extract import DocExtraction, ExtractedItem

logger = logging.getLogger(__name__)

_MAX_PAGE_CHARS = 3500
_MAX_REDUCE_INPUT = 12000
# Fuzzy-merge threshold for concept canonical keys; entity/decorator symbols are exact-only.
_CONCEPT_FUZZY_RATIO = 0.9

_ENTITY_STRIP_AT = re.compile(r"^@+")
_NONALNUM = re.compile(r"[^a-z0-9]+")
_WS = re.compile(r"\s+")


def _is_symbol_name(name: str) -> bool:
    """True for decorators / framework templates (kept even from a single doc)."""
    n = (name or "").strip()
    return n.startswith("@") or "legacy." in n.lower()


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
            # Require token overlap before the ratio check.
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
            name = _canonical_name(cluster)
            # Decorators/framework templates are high-signal even from one doc.
            min_needed = 1 if (page_type == PAGE_ENTITY and _is_symbol_name(name)) else min_docs
            if len(refs) < min_needed:
                continue
            aliases: list[str] = []
            for it in cluster:
                for a in (it.name, *it.aliases):
                    if a and a not in aliases:
                        aliases.append(a)
            groups.append(
                Group(
                    page_type=page_type,
                    name=name,
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
    system = load_prompt("compile").format(kind=kind, name=group.name)
    user = f"Name: {group.name}\n\nGrounded facts from documents:\n{body[:_MAX_REDUCE_INPUT]}"
    try:
        out = llm.complete(system, user, max_tokens=1000)
        return (out or "")[:_MAX_PAGE_CHARS]
    except OpenAIError:
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
