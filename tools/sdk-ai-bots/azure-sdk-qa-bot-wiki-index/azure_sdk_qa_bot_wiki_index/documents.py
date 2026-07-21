"""Index-document assembly + push/purge helpers for the shared Azure AI Search index.

Wiki pages are emitted as :class:`WikiDoc` objects whose field mapping lets the
existing KB retrieval path handle them unchanged:

* ``chunk_id``   — stable unique key ``wiki-<type>-<hash>`` (no leading underscore).
* ``title``      — for **summary** pages, the source's ``#``-encoded rel path so
  ``KnowledgeSource.get_link`` resolves the real doc URL; for cross-document
  pages, a slug (no external link).
* ``header_1``   — a distinct heading so the page is isolated in
  ``expand_by_hierarchy`` and becomes its own reference title.
* ``chunk``      — the page text (embedded).
* ``context_id`` — ``<source folder>`` (summary) or ``wiki_entity`` /
  ``wiki_concept`` / ``wiki_synthesis`` (cross-document) — drives tenant scoping.
* ``chunk_refs`` — source rel paths this page was built from.
* ``page_type``  — ``summary`` | ``entity`` | ``concept`` | ``synthesis``.
* ``text_vector``— ada-002 embedding (shared KB vector space).

All string fields are ``""`` (never null); every push is idempotent
(``mergeOrUpload`` by key). This module is used by the push-based path; the
storage/indexer path writes markdown blobs instead (see the build pipeline).
"""

from __future__ import annotations

import hashlib
import logging
from dataclasses import dataclass, field

logger = logging.getLogger(__name__)

# All generated docs carry this key marker so they can be bulk-listed / purged.
WIKI_KEY_PREFIX = "wiki-"

# Every page_type this project has ever written — used by the purge filter so a
# cleanup removes all generated docs regardless of which scheme produced them.
PAGE_TYPES = ("wiki", "summary", "entity", "concept", "synthesis", "relationship")


def _hash(*parts: str) -> str:
    return hashlib.sha1("\u0000".join(parts).encode("utf-8")).hexdigest()[:20]


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
            "page_type": self.page_type or "",
        }
        if self.text_vector is not None:
            doc["text_vector"] = self.text_vector
        return doc


def make_summary_doc(source_folder: str, rel_title: str, doc_title: str, page_text: str) -> WikiDoc:
    """Build a per-document summary wiki page (inherits source scope)."""
    return WikiDoc(
        chunk_id=f"{WIKI_KEY_PREFIX}summary-{_hash(source_folder, rel_title)}",
        title=rel_title,  # source rel path → link resolves via get_link
        header_1=f"{doc_title} (knowledge)",
        chunk=page_text,
        context_id=source_folder,
        page_type="summary",
        chunk_refs=[rel_title],
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


async def delete_generated_docs(search_data_client, page_types: tuple[str, ...] = PAGE_TYPES) -> int:
    """Delete previously-generated docs whose ``page_type`` is in *page_types*.

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
    logger.info("delete_generated_docs(%s): removed %d docs", page_types, deleted)
    return deleted
