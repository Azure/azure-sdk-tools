"""In-memory wiki page model + slug helpers (shared by map/reduce phases).

A :class:`WikiPage` is the pipeline's intermediate artifact — produced by the
reduce phase, then either persisted to blob (storage path) or converted to an
index document (push path). Slugs mirror WeKnora's ``<type>/<name>`` scheme so
cross-links and dedup can address pages by a stable id.
"""

from __future__ import annotations

import hashlib
import re
from dataclasses import dataclass, field

# WeKnora wiki page types.
PAGE_SUMMARY = "summary"
PAGE_ENTITY = "entity"
PAGE_CONCEPT = "concept"
PAGE_SYNTHESIS = "synthesis"
PAGE_INDEX = "index"

# context_id buckets for cross-document pages (summary inherits its source folder).
CONTEXT_BY_TYPE = {
    PAGE_ENTITY: "wiki_entity",
    PAGE_CONCEPT: "wiki_concept",
    PAGE_SYNTHESIS: "wiki_synthesis",
    PAGE_INDEX: "wiki_index",
}

_SLUG_RE = re.compile(r"[^a-z0-9]+")


def slugify(name: str) -> str:
    """Lower-case, hyphen-separated slug fragment; keeps a hash tail for uniqueness."""
    base = _SLUG_RE.sub("-", name.strip().lower()).strip("-")
    if not base:
        base = "x"
    # short hash of the original preserves distinctness for names that collapse
    tail = hashlib.sha1(name.strip().encode("utf-8")).hexdigest()[:8]
    return f"{base[:60]}-{tail}"


def make_slug(page_type: str, name: str) -> str:
    return f"{page_type}/{slugify(name)}"


@dataclass
class WikiPage:
    """One synthesised wiki page (pre-persistence)."""

    slug: str
    page_type: str
    title: str
    content: str
    context_id: str
    source_refs: list[str] = field(default_factory=list)
    out_links: list[str] = field(default_factory=list)
    orig_title: str = ""  # summary pages: the source rel path (drives get_link)

    def key_hash(self) -> str:
        """Stable content-address of the source set (for change detection)."""
        joined = "\u0000".join(sorted(self.source_refs))
        return hashlib.sha1(joined.encode("utf-8")).hexdigest()[:16]
