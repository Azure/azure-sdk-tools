"""Incrementally rebuild wiki blobs and manifest entries from source changes."""

from __future__ import annotations

import logging
from concurrent.futures import ThreadPoolExecutor
from dataclasses import dataclass

from .llm import ChatLLM
from .pages import PAGE_SUMMARY, WikiPage, make_slug
from .reader import rel_title, source_folder
from .storage import (
    blob_path,
    content_hash,
    now_iso,
    read_manifest,
    render_markdown,
    soft_delete_blob,
    upload_page,
    write_manifest,
)
from .wiki import _doc_title, synthesize_summary
from .wiki_extract import DocExtraction, ExtractedItem, extract_doc
from .wiki_reduce import (
    Group,
    aggregate_groups,
    group_to_page,
    synthesize_group,
)

logger = logging.getLogger(__name__)

MAX_EXTRACT_WORKERS = 4
MAX_SUMMARY_WORKERS = 16
MAX_REDUCE_WORKERS = 8

# Abort rather than tombstone the corpus when this fraction of known sources
# disappears in one run: the wiki reads the container the KB sync writes, so a
# truncated or misconfigured upstream sync would otherwise delete the wiki and
# force a full (expensive) LLM rebuild on the next run.
MAX_DELETE_RATIO = 0.5


class CorpusShrankError(RuntimeError):
    """The source corpus lost too large a fraction of its documents."""


@dataclass
class ReconcileStats:
    changed_docs: int = 0
    deleted_docs: int = 0
    summaries_regenerated: int = 0
    groups_synthesized: int = 0
    pages_written: int = 0
    pages_deleted: int = 0
    total_pages: int = 0


# --------------------------------------------------------------------------- #
# manifest (de)serialisation for extractions
# --------------------------------------------------------------------------- #
def _extraction_to_json(ext: DocExtraction) -> dict:
    return {
        "entities": [
            {"name": e.name, "type": e.type, "description": e.description,
             "aliases": e.aliases, "details": e.details}
            for e in ext.entities
        ],
        "concepts": [
            {"name": c.name, "description": c.description,
             "aliases": c.aliases, "details": c.details}
            for c in ext.concepts
        ],
    }


def _extraction_from_json(source_ref: str, data: dict) -> DocExtraction:
    ext = DocExtraction(source_ref=source_ref)
    for e in data.get("entities", []) or []:
        ext.entities.append(
            ExtractedItem(
                kind="entity", name=e.get("name", ""), type=e.get("type", ""),
                description=e.get("description", ""), source_ref=source_ref,
                aliases=list(e.get("aliases", []) or []), details=e.get("details", ""),
            )
        )
    for c in data.get("concepts", []) or []:
        ext.concepts.append(
            ExtractedItem(
                kind="concept", name=c.get("name", ""), description=c.get("description", ""),
                source_ref=source_ref, aliases=list(c.get("aliases", []) or []),
                details=c.get("details", ""),
            )
        )
    return ext


def _page_from_manifest(entry: dict) -> WikiPage:
    return WikiPage(
        slug=entry["slug"],
        page_type=entry["page_type"],
        title=entry["title"],
        content=entry.get("content", ""),
        context_id=entry["context_id"],
        source_refs=list(entry.get("source_refs", [])),
        orig_title=entry.get("orig_title", ""),
    )


def _group_digest(g: Group) -> str:
    """Content fingerprint of a group's synthesis inputs (name + descriptions)."""
    return content_hash("\u0000".join([g.name, *sorted(g.descriptions)]))


async def reconcile(
    container_client,
    corpus: list[tuple[str, str]],
    llm: ChatLLM,
    *,
    min_docs: int = 2,
) -> ReconcileStats:
    """Run the incremental reconcile and apply blob + manifest changes."""
    manifest = await read_manifest(container_client)
    prior_sources: dict[str, dict] = manifest.get("sources", {})
    prior_pages: dict[str, dict] = {slug: {**e, "slug": slug} for slug, e in manifest.get("pages", {}).items()}

    # --- 1. diff sources by content hash (identity = full source_path) ---
    current: dict[str, tuple[str, str]] = {}
    for source_path, text in corpus:
        current[source_path] = (text, content_hash(text or ""))
    changed = {sp for sp, (_t, h) in current.items() if prior_sources.get(sp, {}).get("hash") != h}
    deleted = {sp for sp in prior_sources if sp not in current}
    if prior_sources and len(deleted) > len(prior_sources) * MAX_DELETE_RATIO:
        raise CorpusShrankError(
            f"{len(deleted)} of {len(prior_sources)} known sources are missing "
            f"(limit {MAX_DELETE_RATIO:.0%}); refusing to tombstone the wiki. "
            "Check the knowledge container and re-run."
        )
    stats = ReconcileStats(changed_docs=len(changed), deleted_docs=len(deleted))
    logger.info("reconcile: %d changed/new, %d deleted, %d unchanged",
                len(changed), len(deleted), len(current) - len(changed))

    failed_docs: set[str] = set()

    # --- 2. extractions: re-extract changed docs, reuse the rest ---
    new_sources: dict[str, dict] = {}
    to_extract = [(sp, current[sp][0]) for sp in changed]

    def _extract(item: tuple[str, str]) -> tuple[str, DocExtraction]:
        sp, text = item
        return sp, extract_doc(llm, sp, text)

    fresh: dict[str, DocExtraction] = {}
    if to_extract:
        with ThreadPoolExecutor(max_workers=MAX_EXTRACT_WORKERS) as ex:
            for sp, extn in ex.map(_extract, to_extract):
                fresh[sp] = extn

    extractions: list[DocExtraction] = []
    for sp, (_text, h) in current.items():
        if sp in fresh and not fresh[sp].failed:
            extn = fresh[sp]
            stored_hash = h
        elif sp in fresh:
            failed_docs.add(sp)
            extn = _extraction_from_json(sp, prior_sources.get(sp, {}))
            stored_hash = h
        else:
            extn = _extraction_from_json(sp, prior_sources.get(sp, {}))
            stored_hash = h
        extractions.append(extn)
        new_sources[sp] = {"hash": stored_hash, **_extraction_to_json(extn)}

    # --- 3. summary pages ---
    summary_pages: dict[str, WikiPage] = {}

    def _summary(item: tuple[str, str]) -> tuple[str, WikiPage | None, bool]:
        sp, text = item
        folder = source_folder(sp)
        rel = rel_title(sp)
        title = _doc_title(rel)
        try:
            body = synthesize_summary(llm, title, text)
        except Exception:
            logger.warning("summary failed for %s", sp, exc_info=True)
            return sp, None, True
        if not body:
            # An empty body is a generation failure, not a valid empty summary:
            # report it so the source hash stays unadvanced and the next run retries.
            logger.warning("summary empty for %s", sp)
            return sp, None, True
        return sp, WikiPage(
            slug=make_slug(PAGE_SUMMARY, sp),
            page_type=PAGE_SUMMARY,
            title=f"{title} (knowledge)",
            content=body,
            context_id=folder,
            source_refs=[sp],
            orig_title=rel,
        ), False

    changed_summary_items = [(sp, current[sp][0]) for sp in changed]
    if changed_summary_items:
        with ThreadPoolExecutor(max_workers=MAX_SUMMARY_WORKERS) as ex:
            for sp, p, failed in ex.map(_summary, changed_summary_items):
                if failed:
                    failed_docs.add(sp)
                if p is not None:
                    summary_pages[p.slug] = p
                    stats.summaries_regenerated += 1
    # reuse unchanged summaries from the manifest
    for sp in current:
        slug = make_slug(PAGE_SUMMARY, sp)
        if slug not in summary_pages and slug in prior_pages:
            summary_pages[slug] = _page_from_manifest(prior_pages[slug])

    # --- 4. entity/concept pages: synth only changed groups ---
    groups = aggregate_groups(extractions, min_docs=min_docs)
    digest_by_slug = {g.slug(): _group_digest(g) for g in groups}
    ec_pages: dict[str, WikiPage] = {}
    to_synth: list[Group] = []
    for g in groups:
        slug = g.slug()
        prior = prior_pages.get(slug)
        if (prior and prior.get("content")
                and set(prior.get("source_refs", [])) == set(g.source_refs)
                and prior.get("input_hash") == digest_by_slug[slug]):
            ec_pages[slug] = _page_from_manifest(prior)
        else:
            to_synth.append(g)
    if to_synth:
        with ThreadPoolExecutor(max_workers=MAX_REDUCE_WORKERS) as ex:
            for g, body in ex.map(lambda gr: (gr, synthesize_group(llm, gr)), to_synth):
                if body:
                    ec_pages[g.slug()] = group_to_page(g, body)
                    stats.groups_synthesized += 1

    # Collect the full current page set before applying the diff.
    all_pages = list(summary_pages.values()) + list(ec_pages.values())
    stats.total_pages = len(all_pages)

    # --- 5. apply: upload changed, soft-delete removed, write manifest ---
    ts = now_iso()
    current_slugs = {p.slug for p in all_pages}
    new_pages_manifest: dict[str, dict] = {}

    for page in all_pages:
        rendered = render_markdown(page)
        chash = content_hash(rendered)
        prior = prior_pages.get(page.slug)
        path = blob_path(page.slug)
        if not prior or prior.get("content_hash") != chash or prior.get("is_deleted") == "true":
            path, chash = await upload_page(container_client, page)
            stats.pages_written += 1
        new_pages_manifest[page.slug] = {
            "page_type": page.page_type,
            "title": page.title,
            "context_id": page.context_id,
            "orig_title": page.orig_title,
            "source_refs": page.source_refs,
            "content": page.content,
            "content_hash": chash,
            "blob_path": path,
            "input_hash": digest_by_slug.get(page.slug, ""),
            "is_deleted": "false",
            "updated_at": ts,
        }

    # soft-delete pages that no longer exist (matching the KB sync pipeline)
    for slug, entry in prior_pages.items():
        if slug in current_slugs or entry.get("is_deleted") == "true":
            continue
        if await soft_delete_blob(container_client, entry.get("blob_path", blob_path(slug))):
            stats.pages_deleted += 1
        entry.pop("slug", None)
        # Drop the body: a tombstone only needs enough to detect resurrection,
        # and retaining it would grow the manifest without bound.
        entry.pop("content", None)
        entry.pop("out_links", None)
        entry["is_deleted"] = "true"
        entry["updated_at"] = ts
        new_pages_manifest[slug] = entry

    # keep failed docs unadvanced so the next run retries them
    for sp in failed_docs:
        if sp in new_sources:
            new_sources[sp]["hash"] = ""

    manifest = {"sources": new_sources, "pages": new_pages_manifest}
    await write_manifest(container_client, manifest)
    logger.info(
        "reconcile done: %d written, %d soft-deleted, %d summaries, %d groups synth, %d total pages",
        stats.pages_written, stats.pages_deleted, stats.summaries_regenerated,
        stats.groups_synthesized, stats.total_pages,
    )
    return stats
