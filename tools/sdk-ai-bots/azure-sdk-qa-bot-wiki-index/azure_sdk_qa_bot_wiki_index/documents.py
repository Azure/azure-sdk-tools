"""Index-document assembly + push into the shared Azure AI Search index.

Both pipelines emit :class:`WikiDoc` objects whose field mapping lets the
existing KB retrieval path handle them unchanged:

* ``chunk_id``   — stable unique key ``wiki-<type>-<hash>`` (no leading underscore).
* ``title``      — for **wiki** pages, the source's ``#``-encoded rel path so
  ``KnowledgeSource.get_link`` resolves the real doc URL; for **entity** /
  **relationship** pages, a slug (no external link).
* ``header_1``   — a distinct heading so the page is isolated in
  ``expand_by_hierarchy`` and becomes its own reference title.
* ``chunk``      — the page text (embedded).
* ``context_id`` — ``<source folder>`` (wiki), ``wiki_entity`` or
  ``wiki_relationship`` (graph) — drives tenant scoping.
* ``chunk_refs`` — source rel paths this page was built from.
* ``related_slugs`` — graph edges (neighbour entity slugs / endpoint slugs).
* ``page_type``  — ``wiki`` | ``entity`` | ``relationship``.
* ``text_vector``— ada-002 embedding (shared KB vector space).

All string fields are ``""`` (never null); every push is idempotent
(``mergeOrUpload`` by key).
"""

from __future__ import annotations

import hashlib
import logging
from dataclasses import dataclass, field

logger = logging.getLogger(__name__)

WIKI_ENTITY_CONTEXT = "wiki_entity"
WIKI_RELATIONSHIP_CONTEXT = "wiki_relationship"

# All generated docs carry this key marker so they can be bulk-listed / purged.
WIKI_KEY_PREFIX = "wiki-"

# page_type values this project writes (used by the purge filter). ``summary`` /
# ``concept`` are legacy values retained only so old docs are still purgeable.
PAGE_TYPES = ("wiki", "entity", "relationship", "summary", "concept")
WIKI_PAGE_TYPES = ("wiki", "summary")
GRAPH_PAGE_TYPES = ("entity", "relationship", "concept")


def _hash(*parts: str) -> str:
    return hashlib.sha1("\u0000".join(parts).encode("utf-8")).hexdigest()[:20]


def entity_slug(name: str) -> str:
    return f"entity:{name}"


def relationship_slug(source: str, target: str) -> str:
    return f"rel:{source}->{target}"


@dataclass
class WikiDoc:
    """An index document for one generated page."""

    chunk_id: str
    title: str
    header_1: str
    chunk: str
    context_id: str
    page_type: str
    chunk_refs: list[str] = field(default_factory=list)
    related_slugs: list[str] = field(default_factory=list)
    scope: str = "branded"
    service_type: str = ""
    text_vector: list[float] | None = None

    def to_index_doc(self) -> dict:
        """Serialise to the index schema (all strings non-null)."""
        doc = {
            "chunk_id": self.chunk_id,
            "parent_id": "",
            "title": self.title or "",
            "header_1": self.header_1 or "",
            "header_2": "",
            "header_3": "",
            "chunk": self.chunk or "",
            "context_id": self.context_id or "",
            "scope": self.scope or "",
            "service_type": self.service_type or "",
            "ordinal_position": 0,
            "chunk_refs": self.chunk_refs or [],
            "related_slugs": self.related_slugs or [],
            "page_type": self.page_type or "",
        }
        if self.text_vector is not None:
            doc["text_vector"] = self.text_vector
        return doc


def make_wiki_doc(source_folder: str, rel_title: str, doc_title: str, page_text: str) -> WikiDoc:
    """Build a per-document wiki page (inherits source scope for tenant filtering)."""
    return WikiDoc(
        chunk_id=f"{WIKI_KEY_PREFIX}wiki-{_hash(source_folder, rel_title)}",
        title=rel_title,  # source rel path → link resolves via get_link
        header_1=f"{doc_title} (knowledge)",
        chunk=page_text,
        context_id=source_folder,
        page_type="wiki",
        chunk_refs=[rel_title],
    )


def make_entity_doc(
    name: str,
    description: str,
    *,
    entity_type: str = "",
    source_refs: list[str] | None = None,
    related: list[str] | None = None,
) -> WikiDoc:
    """Build a graph ENTITY page (WeKnora ``ChunkTypeEntity``)."""
    heading = f"{name} (entity)" if not entity_type else f"{name} ({entity_type})"
    return WikiDoc(
        chunk_id=f"{WIKI_KEY_PREFIX}entity-{_hash(name)}",
        title=entity_slug(name),
        header_1=heading,
        chunk=description or name,
        context_id=WIKI_ENTITY_CONTEXT,
        page_type="entity",
        chunk_refs=sorted(source_refs or []),
        related_slugs=related or [],
    )


def make_relationship_doc(
    source: str,
    target: str,
    description: str,
    *,
    strength: int = 1,
    weight: float = 0.0,
    source_refs: list[str] | None = None,
) -> WikiDoc:
    """Build a graph RELATIONSHIP page (WeKnora ``ChunkTypeRelationship``)."""
    body = (description or "").strip()
    body = f"{source} \u2192 {target}: {body}" if body else f"{source} \u2192 {target}"
    if strength:
        body += f"\n\n(strength {strength}/10, weight {weight:.2f})"
    return WikiDoc(
        chunk_id=f"{WIKI_KEY_PREFIX}rel-{_hash(source, target)}",
        title=relationship_slug(source, target),
        header_1=f"{source} \u2194 {target} (relationship)",
        chunk=body,
        context_id=WIKI_RELATIONSHIP_CONTEXT,
        page_type="relationship",
        chunk_refs=sorted(source_refs or []),
        related_slugs=[entity_slug(source), entity_slug(target)],
    )


async def push_docs(search_data_client, docs: list[WikiDoc], batch_size: int = 100) -> int:
    """``mergeOrUpload`` docs into the index in batches. Returns success count."""
    ok = 0
    payload = [d.to_index_doc() for d in docs]
    for i in range(0, len(payload), batch_size):
        batch = payload[i : i + batch_size]
        results = await search_data_client.merge_or_upload_documents(documents=batch)
        ok += sum(1 for r in results if r.succeeded)
    logger.info("push_docs: %d/%d docs upserted", ok, len(docs))
    return ok


async def delete_docs_by_page_types(search_data_client, page_types: tuple[str, ...]) -> int:
    """Delete previously-pushed docs whose ``page_type`` is in *page_types*.

    Only docs with the ``wiki-`` key prefix are removed (safety net); raw KB
    chunks leave ``page_type`` null and are never matched.
    """
    quoted = ",".join(page_types)
    to_delete: list[dict] = []
    results = await search_data_client.search(
        search_text="*",
        filter=f"search.in(page_type, '{quoted}', ',')",
        select=["chunk_id"],
        top=100000,
    )
    async for doc in results:
        cid = doc.get("chunk_id", "")
        if cid.startswith(WIKI_KEY_PREFIX):
            to_delete.append({"chunk_id": cid})
    if not to_delete:
        return 0
    deleted = 0
    for i in range(0, len(to_delete), 100):
        batch = to_delete[i : i + 100]
        r = await search_data_client.delete_documents(documents=batch)
        deleted += sum(1 for x in r if x.succeeded)
    logger.info("delete_docs_by_page_types(%s): removed %d docs", page_types, deleted)
    return deleted
