"""Blob persistence for wiki pages (durable, rebuildable, debuggable).

Each :class:`WikiPage` is written as a markdown blob under its slug in the wiki
container, carrying the fields the dedicated indexer needs as **blob metadata**
(so Azure Search can field-map them without a custom skill):

* ``page_type``    — summary | entity | concept | synthesis | index
* ``context_id``   — source folder (summary) or ``wiki_*`` bucket → tenant scope
* ``title``        — the index title (summary: source rel path so ``get_link``
  resolves the real doc; others: the display title)
* ``content_hash`` — change-detection digest of the rendered body
* ``is_deleted``   — soft-delete tombstone flag (``"true"`` when retracted)

A ``_manifest.json`` at the container root records every page's full provenance
(source_refs, out_links, hashes) for reconcile (:mod:`reconcile`) and for tracing
any answer back to the documents it was synthesised from.

Deletion is **soft**: the blob is kept (metadata ``is_deleted=true``) so the
indexer's ``SoftDeleteColumnDeletionDetectionPolicy`` removes it from the index
while the artifact remains for audit (mirrors WeKnora's page archival).
"""

from __future__ import annotations

import hashlib
import json
import logging
from datetime import datetime, timezone

from .pages import WikiPage

logger = logging.getLogger(__name__)

MANIFEST_BLOB = "_manifest.json"
MANIFEST_VERSION = 1


def _ascii(s: str, limit: int = 1024) -> str:
    """Blob metadata values must be ASCII; strip the rest and cap length."""
    return (s or "").encode("ascii", "ignore").decode("ascii")[:limit]


def _blob_path(slug: str) -> str:
    return f"{slug}.md"


def render_markdown(page: WikiPage, title_by_slug: dict[str, str]) -> str:
    """Markdown body = title + content + a Related section embedding cross-links."""
    parts = [f"# {page.title}", "", page.content.strip()]
    related = [title_by_slug.get(s) for s in page.out_links]
    related = [r for r in related if r]
    if related:
        parts += ["", "## Related", *[f"- {r}" for r in related]]
    return "\n".join(parts).strip() + "\n"


def content_hash(text: str) -> str:
    return hashlib.sha1(text.encode("utf-8")).hexdigest()[:16]


def _index_title(page: WikiPage) -> str:
    """The title the index should carry: summary → source rel path, else display."""
    return page.orig_title if page.page_type == "summary" and page.orig_title else page.title


async def write_pages(container_client, pages: list[WikiPage]) -> dict:
    """Upload every page as a markdown blob (+ metadata) and write the manifest.

    Returns the manifest dict.
    """
    title_by_slug = {p.slug: p.title for p in pages}
    manifest_pages: dict[str, dict] = {}
    now = datetime.now(timezone.utc).isoformat()

    for page in pages:
        body = render_markdown(page, title_by_slug)
        chash = content_hash(body)
        blob_path = _blob_path(page.slug)
        metadata = {
            "page_type": _ascii(page.page_type),
            "context_id": _ascii(page.context_id),
            "title": _ascii(_index_title(page)),
            "content_hash": chash,
            "is_deleted": "false",
        }
        blob = container_client.get_blob_client(blob_path)
        await blob.upload_blob(
            body.encode("utf-8"),
            overwrite=True,
            metadata=metadata,
            content_type="text/markdown; charset=utf-8",
        )
        manifest_pages[page.slug] = {
            "page_type": page.page_type,
            "title": page.title,
            "context_id": page.context_id,
            "orig_title": page.orig_title,
            "source_refs": page.source_refs,
            "out_links": page.out_links,
            "content_hash": chash,
            "blob_path": blob_path,
            "updated_at": now,
        }

    manifest = {"version": MANIFEST_VERSION, "updated_at": now, "pages": manifest_pages}
    await _write_manifest(container_client, manifest)
    logger.info("write_pages: uploaded %d pages + manifest", len(pages))
    return manifest


async def _write_manifest(container_client, manifest: dict) -> None:
    blob = container_client.get_blob_client(MANIFEST_BLOB)
    await blob.upload_blob(
        json.dumps(manifest, ensure_ascii=False, indent=2).encode("utf-8"),
        overwrite=True,
        content_type="application/json",
    )


async def load_manifest(container_client) -> dict:
    """Load the manifest, or an empty one if none exists yet."""
    blob = container_client.get_blob_client(MANIFEST_BLOB)
    try:
        exists = await blob.exists()
    except Exception:
        exists = False
    if not exists:
        return {"version": MANIFEST_VERSION, "updated_at": "", "pages": {}}
    downloader = await blob.download_blob()
    data = await downloader.readall()
    try:
        return json.loads(data.decode("utf-8"))
    except (json.JSONDecodeError, UnicodeDecodeError):
        logger.warning("manifest corrupt; treating as empty")
        return {"version": MANIFEST_VERSION, "updated_at": "", "pages": {}}


async def soft_delete_page(container_client, blob_path: str) -> bool:
    """Tombstone a page: set metadata ``is_deleted=true`` (keeps the blob for audit)."""
    blob = container_client.get_blob_client(blob_path)
    try:
        props = await blob.get_blob_properties()
    except Exception:
        return False
    metadata = dict(props.metadata or {})
    metadata["is_deleted"] = "true"
    await blob.set_blob_metadata(metadata)
    logger.info("soft_delete_page: tombstoned %s", blob_path)
    return True
