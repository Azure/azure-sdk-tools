"""Wiki-page document assembly + push into the shared Azure AI Search index.

Turns synthesised pages into index documents whose field mapping is chosen so
the existing KB retrieval path handles them unchanged:

* ``chunk_id``   — stable unique key ``wiki-<type>-<hash>`` (no leading underscore).
* ``title``      — for **summary** pages, the source's ``#``-encoded rel path so
  ``KnowledgeSource.get_link`` resolves the real doc URL; for entity/concept
  pages, a slug (no external link).
* ``header_1``   — a distinct heading (e.g. ``"<Doc> (knowledge)"``) that both
  becomes the reference title and isolates the card in ``expand_by_hierarchy``
  (no source chunk shares it), so the card stays a standalone unit.
* ``chunk``      — the synthesised page text (embedded).
* ``context_id`` — inherited source folder (summary) or ``wiki_entity`` /
  ``wiki_concept`` (cross-document pages), driving tenant scoping.
* ``chunk_refs`` — source rel paths this page was built from (deep-read anchor).
* ``related_slugs`` — page-to-page links.
* ``page_type``  — ``summary`` | ``entity`` | ``concept``.
* ``text_vector``— ada-002 embedding (shared KB vector space).

All string fields are set to ``""`` (never null) to satisfy the retrieval
model's validation; every push is idempotent (``mergeOrUpload`` by key).
"""

from __future__ import annotations

import hashlib
import logging
from dataclasses import dataclass, field

logger = logging.getLogger(__name__)

WIKI_ENTITY_CONTEXT = "wiki_entity"
WIKI_CONCEPT_CONTEXT = "wiki_concept"

# All wiki docs carry this marker so they can be bulk-listed / cleaned up.
WIKI_KEY_PREFIX = "wiki-"


def _hash(*parts: str) -> str:
    return hashlib.sha1("\u0000".join(parts).encode("utf-8")).hexdigest()[:20]


@dataclass
class WikiDoc:
    """An index document for one wiki page."""

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


def make_summary_doc(source_folder: str, rel_title: str, doc_title: str, card_text: str) -> WikiDoc:
    """Build a per-document summary knowledge-card doc (inherits source scope)."""
    return WikiDoc(
        chunk_id=f"{WIKI_KEY_PREFIX}summary-{_hash(source_folder, rel_title)}",
        title=rel_title,  # source rel path → link resolves via get_link
        header_1=f"{doc_title} (knowledge)",
        chunk=card_text,
        context_id=source_folder,
        page_type="summary",
        chunk_refs=[rel_title],
    )


def make_entity_doc(entity: str, page_text: str, source_refs: list[str], related: list[str]) -> WikiDoc:
    return WikiDoc(
        chunk_id=f"{WIKI_KEY_PREFIX}entity-{_hash(entity)}",
        title=f"entity:{entity}",
        header_1=f"{entity} (knowledge)",
        chunk=page_text,
        context_id=WIKI_ENTITY_CONTEXT,
        page_type="entity",
        chunk_refs=source_refs,
        related_slugs=related,
    )


def make_concept_doc(concept: str, page_text: str, source_refs: list[str], related: list[str]) -> WikiDoc:
    return WikiDoc(
        chunk_id=f"{WIKI_KEY_PREFIX}concept-{_hash(concept)}",
        title=f"concept:{concept}",
        header_1=f"{concept} (knowledge)",
        chunk=page_text,
        context_id=WIKI_CONCEPT_CONTEXT,
        page_type="concept",
        chunk_refs=source_refs,
        related_slugs=related,
    )


async def push_docs(search_data_client, docs: list[WikiDoc], batch_size: int = 100) -> int:
    """``mergeOrUpload`` wiki docs into the index in batches. Returns success count."""
    ok = 0
    payload = [d.to_index_doc() for d in docs]
    for i in range(0, len(payload), batch_size):
        batch = payload[i : i + batch_size]
        results = await search_data_client.merge_or_upload_documents(documents=batch)
        ok += sum(1 for r in results if r.succeeded)
    logger.info("push_docs: %d/%d wiki docs upserted", ok, len(docs))
    return ok


async def delete_all_wiki_docs(search_data_client) -> int:
    """Delete every previously-pushed wiki doc. Returns count.

    Wiki docs are the only documents with ``page_type`` in
    ``{summary, entity, concept}`` (raw chunks leave it null), so that filterable
    field selects them server-side; the ``wiki-`` key prefix is re-checked
    client-side as a safety net.
    """
    to_delete: list[dict] = []
    results = await search_data_client.search(
        search_text="*",
        filter="search.in(page_type, 'summary,entity,concept', ',')",
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
    logger.info("delete_all_wiki_docs: removed %d wiki docs", deleted)
    return deleted
