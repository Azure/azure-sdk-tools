"""Blob persistence + manifest for wiki pages (durable, rebuildable, debuggable).

Each :class:`WikiPage` is a markdown blob under its slug in the wiki container,
carrying the fields the dedicated indexer needs as **blob metadata** (so Azure
Search can field-map them with no custom skill):

* ``page_type``    — summary | entity | concept | index
* ``context_id``   — source folder (summary) or ``wiki_*`` bucket → tenant scope
* ``title``        — index title (summary: source rel path so ``get_link``
  resolves the real doc; others: display title)
* ``content_hash`` — change-detection digest of the rendered body
* ``is_deleted``   — soft-delete tombstone (``"true"`` when retracted)

``_manifest.json`` (v2) is the reconcile state + provenance record:

    {
      "version": 2,
      "sources": { "<rel>": {"hash", "entities":[...], "concepts":[...]} },
      "pages":   { "<slug>": {page_type,title,context_id,orig_title,source_refs,
                              out_links,content,content_hash,blob_path,updated_at} }
    }

Storing per-document extractions + page bodies lets the incremental reconcile
(:mod:`reconcile`) rebuild without re-running the LLM on unchanged docs/groups.
Deletion is **soft** (metadata ``is_deleted=true``; blob kept for audit).
"""

from __future__ import annotations

import hashlib
import json
import logging
from datetime import datetime, timezone

from .pages import WikiPage

logger = logging.getLogger(__name__)

MANIFEST_BLOB = "_manifest.json"
MANIFEST_VERSION = 2

# Cap on source refs stored in blob metadata (bounds metadata size; backfill
# only needs a few of the most relevant source docs per page anyway).
_MAX_CHUNK_REFS_META = 8


def _ascii(s: str, limit: int = 1024) -> str:
    """Blob metadata values must be ASCII; strip the rest and cap length."""
    return (s or "").encode("ascii", "ignore").decode("ascii")[:limit]


def blob_path(slug: str) -> str:
    return f"{slug}.md"


def content_hash(text: str) -> str:
    """Content-only change-detection digest (schema-independent, so a metadata
    schema change never masquerades as a source/page content change)."""
    return hashlib.sha1(text.encode("utf-8")).hexdigest()[:16]


def render_markdown(page: WikiPage, title_by_slug: dict[str, str]) -> str:
    """Markdown body = title + content + a Related section embedding cross-links."""
    parts = [f"# {page.title}", "", page.content.strip()]
    related = [title_by_slug.get(s) for s in page.out_links]
    related = [r for r in related if r]
    if related:
        parts += ["", "## Related", *[f"- {r}" for r in related]]
    return "\n".join(parts).strip() + "\n"


def index_title(page: WikiPage) -> str:
    """The title the index should carry: summary → source rel path, else display."""
    return page.orig_title if page.page_type == "summary" and page.orig_title else page.title


def now_iso() -> str:
    return datetime.now(timezone.utc).isoformat()


async def upload_page(container_client, page: WikiPage, title_by_slug: dict[str, str]) -> tuple[str, str, str]:
    """Upload one page blob (+ metadata). Returns (blob_path, content_hash, body)."""
    body = render_markdown(page, title_by_slug)
    chash = content_hash(body)
    path = blob_path(page.slug)
    # chunk_refs = the source docs this page routes to, for query-time backfill.
    # Stored as a JSON-array string; the indexer projects it verbatim into the
    # index's ``chunk_refs_str`` (Edm.String) field, which the agent parses.
    refs = [_ascii(r) for r in (page.source_refs or [])[:_MAX_CHUNK_REFS_META] if r]
    metadata = {
        "page_type": _ascii(page.page_type),
        "context_id": _ascii(page.context_id),
        "title": _ascii(index_title(page)),
        "chunk_refs": _ascii(json.dumps(refs), limit=7000),
        "content_hash": chash,
        "is_deleted": "false",
    }
    blob = container_client.get_blob_client(path)
    await blob.upload_blob(
        body.encode("utf-8"),
        overwrite=True,
        metadata=metadata,
        content_type="text/markdown; charset=utf-8",
    )
    return path, chash, body


async def backfill_chunk_refs_metadata(container_client) -> tuple[int, int]:
    """One-time migration: stamp ``chunk_refs`` blob metadata onto existing pages.

    Reads the manifest and, for every live page blob, writes the JSON-array
    ``chunk_refs`` metadata derived from the page's stored ``source_refs`` —
    preserving all other metadata and **without** re-rendering bodies or calling
    the LLM. Lets already-built wikis pick up the query-time backfill field with
    a metadata-only rewrite; a subsequent indexer run projects it into the index.

    Returns ``(updated, skipped)``.
    """
    manifest = await read_manifest(container_client)
    pages = manifest.get("pages", {})
    updated = skipped = 0
    for slug, entry in pages.items():
        if entry.get("is_deleted") == "true":
            skipped += 1
            continue
        path = entry.get("blob_path") or blob_path(slug)
        refs = [_ascii(r) for r in (entry.get("source_refs") or [])[:_MAX_CHUNK_REFS_META] if r]
        blob = container_client.get_blob_client(path)
        try:
            props = await blob.get_blob_properties()
        except Exception:
            skipped += 1
            continue
        metadata = dict(props.metadata or {})
        metadata["chunk_refs"] = _ascii(json.dumps(refs), limit=7000)
        await blob.set_blob_metadata(metadata)
        updated += 1
    logger.info("backfill_chunk_refs_metadata: %d updated, %d skipped", updated, skipped)
    return updated, skipped


async def soft_delete_blob(container_client, path: str) -> bool:
    """Tombstone a page blob: metadata ``is_deleted=true`` (keeps blob for audit)."""
    blob = container_client.get_blob_client(path)
    try:
        props = await blob.get_blob_properties()
    except Exception:
        return False
    metadata = dict(props.metadata or {})
    if metadata.get("is_deleted") == "true":
        return False
    metadata["is_deleted"] = "true"
    await blob.set_blob_metadata(metadata)
    logger.info("soft_delete_blob: tombstoned %s", path)
    return True


async def read_manifest(container_client) -> dict:
    """Load the manifest, or an empty v2 skeleton if none exists yet."""
    blob = container_client.get_blob_client(MANIFEST_BLOB)
    try:
        exists = await blob.exists()
    except Exception:
        exists = False
    if not exists:
        return {"version": MANIFEST_VERSION, "updated_at": "", "sources": {}, "pages": {}}
    downloader = await blob.download_blob()
    data = await downloader.readall()
    try:
        m = json.loads(data.decode("utf-8"))
    except (json.JSONDecodeError, UnicodeDecodeError):
        logger.warning("manifest corrupt; treating as empty")
        return {"version": MANIFEST_VERSION, "updated_at": "", "sources": {}, "pages": {}}
    m.setdefault("sources", {})
    m.setdefault("pages", {})
    return m


async def write_manifest(container_client, manifest: dict) -> None:
    manifest["version"] = MANIFEST_VERSION
    manifest["updated_at"] = now_iso()
    blob = container_client.get_blob_client(MANIFEST_BLOB)
    await blob.upload_blob(
        json.dumps(manifest, ensure_ascii=False, indent=2).encode("utf-8"),
        overwrite=True,
        content_type="application/json",
    )
