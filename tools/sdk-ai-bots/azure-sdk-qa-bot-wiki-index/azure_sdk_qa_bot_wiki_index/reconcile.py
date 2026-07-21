"""Incremental update/delete reconcile (WeKnora-faithful).

Given the current source corpus and the prior ``_manifest.json``, this computes a
minimal set of changes and applies them — mirroring WeKnora's wiki re-ingest /
finalize (retract stale doc contributions, re-aggregate affected entity/concept
pages, re-inject cross-links, archive orphaned pages):

1. **Diff sources** by content hash → ``changed_or_new`` / ``deleted`` docs.
2. **Extractions**: re-run the map (LLM) only on changed/new docs; reuse stored
   extractions for unchanged docs; drop deleted docs.
3. **Summary pages**: regenerate (LLM) only for changed/new docs; reuse bodies
   for unchanged docs; soft-delete summaries of deleted docs.
4. **Entity/concept pages**: aggregate all current extractions into groups;
   synthesise (LLM) only groups whose source set changed vs the manifest; reuse
   unchanged bodies; soft-delete groups that fell below ``min_docs`` / vanished.
5. **Cross-links + index**: recompute deterministically over the full page set.
6. **Write**: upload only pages whose rendered content hash changed; soft-delete
   removed pages; write the new manifest.

The first run (empty manifest) naturally degenerates to a full build.
"""

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
    build_index_page,
    group_to_page,
    inject_cross_links,
    synthesize_group,
)

logger = logging.getLogger(__name__)

MAX_EXTRACT_WORKERS = 4
MAX_SUMMARY_WORKERS = 16
MAX_REDUCE_WORKERS = 8


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
            {"name": e.name, "type": e.type, "description": e.description} for e in ext.entities
        ],
        "concepts": [{"name": c.name, "description": c.description} for c in ext.concepts],
    }


def _extraction_from_json(source_ref: str, data: dict) -> DocExtraction:
    ext = DocExtraction(source_ref=source_ref)
    for e in data.get("entities", []) or []:
        ext.entities.append(
            ExtractedItem("entity", e.get("name", ""), e.get("type", ""), e.get("description", ""), source_ref)
        )
    for c in data.get("concepts", []) or []:
        ext.concepts.append(
            ExtractedItem("concept", c.get("name", ""), "", c.get("description", ""), source_ref)
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
        out_links=list(entry.get("out_links", [])),
        orig_title=entry.get("orig_title", ""),
    )


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

    # --- 1. diff sources by content hash ---
    current: dict[str, tuple[str, str]] = {}  # rel -> (text, hash)
    for source_path, text in corpus:
        rel = rel_title(source_path)
        current[rel] = (text, content_hash(text or ""))
    changed = {rel for rel, (_t, h) in current.items() if prior_sources.get(rel, {}).get("hash") != h}
    deleted = {rel for rel in prior_sources if rel not in current}
    stats = ReconcileStats(changed_docs=len(changed), deleted_docs=len(deleted))
    logger.info("reconcile: %d changed/new, %d deleted, %d unchanged",
                len(changed), len(deleted), len(current) - len(changed))

    # source folder (context_id) per rel, computed once
    folder_by_rel = {rel_title(sp): source_folder(sp) for sp, _t in corpus}

    # --- 2. extractions: re-extract changed docs, reuse the rest ---
    new_sources: dict[str, dict] = {}
    to_extract = [(rel, current[rel][0]) for rel in changed]

    def _extract(item: tuple[str, str]) -> tuple[str, DocExtraction]:
        rel, text = item
        return rel, extract_doc(llm, rel, text)

    fresh: dict[str, DocExtraction] = {}
    if to_extract:
        with ThreadPoolExecutor(max_workers=MAX_EXTRACT_WORKERS) as ex:
            for rel, extn in ex.map(_extract, to_extract):
                fresh[rel] = extn

    extractions: list[DocExtraction] = []
    for rel, (_text, h) in current.items():
        if rel in fresh:
            extn = fresh[rel]
        else:
            extn = _extraction_from_json(rel, prior_sources.get(rel, {}))
        extractions.append(extn)
        new_sources[rel] = {"hash": h, **_extraction_to_json(extn)}

    # --- 3. summary pages ---
    summary_pages: dict[str, WikiPage] = {}

    def _summary(item: tuple[str, str]) -> WikiPage | None:
        rel, text = item
        folder = folder_by_rel.get(rel, "")
        title = _doc_title(rel)
        body = synthesize_summary(llm, title, text)
        if not body:
            return None
        return WikiPage(
            slug=make_slug(PAGE_SUMMARY, rel),
            page_type=PAGE_SUMMARY,
            title=f"{title} (knowledge)",
            content=body,
            context_id=folder,
            source_refs=[rel],
            orig_title=rel,
        )

    changed_summary_items = [(rel, current[rel][0]) for rel in changed]
    if changed_summary_items:
        with ThreadPoolExecutor(max_workers=MAX_SUMMARY_WORKERS) as ex:
            for p in ex.map(_summary, changed_summary_items):
                if p is not None:
                    summary_pages[p.slug] = p
                    stats.summaries_regenerated += 1
    # reuse unchanged summaries from the manifest
    for rel in current:
        slug = make_slug(PAGE_SUMMARY, rel)
        if slug not in summary_pages and slug in prior_pages:
            summary_pages[slug] = _page_from_manifest(prior_pages[slug])

    # --- 4. entity/concept pages: synth only changed groups ---
    groups = aggregate_groups(extractions, min_docs=min_docs)
    ec_pages: dict[str, WikiPage] = {}
    to_synth: list[Group] = []
    for g in groups:
        slug = g.slug()
        prior = prior_pages.get(slug)
        if prior and set(prior.get("source_refs", [])) == set(g.source_refs) and prior.get("content"):
            ec_pages[slug] = _page_from_manifest(prior)
        else:
            to_synth.append(g)
    if to_synth:
        with ThreadPoolExecutor(max_workers=MAX_REDUCE_WORKERS) as ex:
            for g, body in ex.map(lambda gr: (gr, synthesize_group(llm, gr)), to_synth):
                if body:
                    ec_pages[g.slug()] = group_to_page(g, body)
                    stats.groups_synthesized += 1

    # --- 5. cross-links + index over the full current page set ---
    all_pages = list(summary_pages.values()) + list(ec_pages.values())
    inject_cross_links(all_pages)
    index = build_index_page(all_pages)
    if index is not None:
        all_pages.append(index)
    stats.total_pages = len(all_pages)

    # --- 6. apply: upload changed, soft-delete removed, write manifest ---
    from .storage import now_iso
    ts = now_iso()
    title_by_slug = {p.slug: p.title for p in all_pages}
    current_slugs = {p.slug for p in all_pages}
    new_pages_manifest: dict[str, dict] = {}

    for page in all_pages:
        rendered = render_markdown(page, title_by_slug)
        chash = content_hash(rendered)
        prior = prior_pages.get(page.slug)
        path = blob_path(page.slug)
        if not prior or prior.get("content_hash") != chash or prior.get("is_deleted") == "true":
            path, chash, _ = await upload_page(container_client, page, title_by_slug)
            stats.pages_written += 1
        new_pages_manifest[page.slug] = {
            "page_type": page.page_type,
            "title": page.title,
            "context_id": page.context_id,
            "orig_title": page.orig_title,
            "source_refs": page.source_refs,
            "out_links": page.out_links,
            "content": page.content,
            "content_hash": chash,
            "blob_path": path,
            "is_deleted": "false",
            "updated_at": ts,
        }

    # soft-delete pages that no longer exist
    for slug, entry in prior_pages.items():
        if slug not in current_slugs and entry.get("is_deleted") != "true":
            if await soft_delete_blob(container_client, entry.get("blob_path", blob_path(slug))):
                stats.pages_deleted += 1
            entry["is_deleted"] = "true"
            entry.pop("slug", None)
            new_pages_manifest[slug] = entry

    manifest = {"sources": new_sources, "pages": new_pages_manifest}
    await write_manifest(container_client, manifest)
    logger.info(
        "reconcile done: %d written, %d soft-deleted, %d summaries, %d groups synth, %d total pages",
        stats.pages_written, stats.pages_deleted, stats.summaries_regenerated,
        stats.groups_synthesized, stats.total_pages,
    )
    return stats
