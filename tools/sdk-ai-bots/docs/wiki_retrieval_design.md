# Wiki Knowledge-Layer Retrieval — Design

## Background

The QA bot grounds its answers on a curated corpus (TypeSpec docs, ARM/API guidelines, SDK repo docs, samples, resolved threads). The original path is vector/agentic search over an Azure AI Search index of document chunks ("KB path"): strong for single-concept, verbatim-rule lookups, but with no consolidated cross-document view of a symbol or topic.

This design adds a **wiki knowledge layer**: an offline, LLM-generated set of pages that distil the corpus into per-document **summaries**, per-symbol **entity** pages, and per-topic **concept** pages. It is additive and rebuildable; source chunks remain the authoritative grounding.

## Architecture

The wiki is a second layer over the **same** corpus, distinguished from raw chunks by a `page_type` field on the shared index.

```mermaid
flowchart LR
    corpus[(Knowledge corpus<br/>markdown in blob)]
    idx[(Shared AI Search index<br/>chunks + wiki pages<br/>tagged by page_type)]
    subgraph BUILD[Wiki build - offline]
        map[Map: per-doc extract<br/>entities + concepts]
        red[Reduce: aggregate + dedup<br/>+ synthesize pages]
        blob[(Wiki pages<br/>blobs + manifest)]
    end
    corpus -->|incremental index| idx
    corpus --> map --> red --> blob
    blob -->|dedicated indexer| idx
    subgraph AGENT[Chat agent]
        kbtool[search_knowledge_base<br/>source chunks only]
        wikitool[wiki_search<br/>pages + routed sources]
        ans([Grounded answer])
    end
    idx --> kbtool --> ans
    idx --> wikitool --> ans
```

- **Build (offline).** A map-reduce over the markdown corpus produces three page types: `summary` (one per document, from its full text), `entity` (one per recurring named symbol), and `concept` (one per cross-cutting topic). Entity/concept pages aggregate mentions across documents with alias/near-duplicate merging and record the source docs they were built from (`chunk_refs`) for query-time routing. Named decorators and framework templates (`@`-prefixed names, `Azure.ResourceManager.Legacy.*`) are always extracted — even from a single document — and their constraints and anti-patterns captured, so symbol-specific questions route to a consolidated page. Pages are written as markdown blobs plus a reconcile manifest. All three LLM prompts live as markdown under `prompts/`.
- **Indexer (blobs → index).** A dedicated indexer projects the wiki blobs into the **shared** KB index, so one index serves both layers. Raw chunks leave `page_type` null; wiki pages set it. `chunk_refs` are carried as a JSON-array string (index projections cannot populate a collection from a scalar) and parsed back at query time. Soft-deletes propagate to the index. `setup_indexer.py` (re)creates the datasource / skillset / indexer.

## Two-track retrieval

The load-bearing decision: **wiki pages and source chunks never fuse into one ranked list** — fusing lets generic wiki pages displace specific source docs and regresses the score. They are retrieved on separate tracks and combined only in the answer.

- **`search_knowledge_base`** — source chunks only (`page_type` null).
- **`wiki_search`** — wiki pages only, **self-contained**: for the top pages it returns their full content **plus** the source chunks each was built from (routed via `chunk_refs`). The next-ranked pages that did not make the cut are appended as a titles-only "Related wiki pages" reference, so the agent can see the neighbourhood it just missed and name an adjacent variant without a second search.
- **`grep_chunks`** (literal symbol/error-string match), **`wiki_read_page`**, **`wiki_read_source_doc`** — optional targeted drills.

Both tracks run the same retrieval pipeline (`SearchClient.fused_search`) and differ only by page-type filter: per query, dense + BM25 (+ agentic in `deep` mode) run in parallel and are fused with RRF, then the caller dedupes and caps.

For most questions the agent issues `search_knowledge_base` + `wiki_search` in one parallel batch and answers on the next turn.

## Faithfulness of generated pages

Summarisation naturally drops qualifiers, and a wiki page that states a conditional fact as an unconditional rule is worse than no page at all: at query time a confident general principle can override the case-specific answer the user needs. The build prompts therefore require each fact to keep the scope its source gives it — conditions, exceptions, and tolerated deviations are recorded next to the rule they qualify, and a verdict given for one situation never becomes general guidance. The answer rules mirror this from the other side: a source that answers the exact situation wins over a source stating a broad rule.

## Tenant scoping

There is no access-control layer: tenant scoping is **retrieval-scope selection**, expressed as an OData filter on the index's `context_id` field. Three layers:

1. **A global source registry** (`config/tenant_config.py`). Every knowledge source is registered once as a `KnowledgeSource`, whose `name` is also the `context_id` value carried by its documents in the index. The source's description is surfaced in the retrieval tools' parameter docs, so the model picks sources itself.
2. **Tenants reference sources by name.** A `TenantConfig` holds an ordered source list, and may override an individual source's filter via `source_filter`.
3. **Query time expands them into one filter** (`_resolve_source_filters`). Each source becomes `context_id eq '<name>'`, optionally `and`-ed with the tenant's override and a service-type clause; the per-source clauses are then OR-ed into the single `filter` passed to Azure AI Search.

Wiki pages join this scheme through the `context_id` written at build time:

| Page type | `context_id` | Source |
| --------- | ------------ | ------ |
| `summary` | the source document's first path segment | `reader.source_folder` — blob `typespec_docs/x.md` → `typespec_docs` |
| `entity` | `wiki_entity` | fixed, `wiki_reduce.CONTEXT_BY_TYPE` |
| `concept` | `wiki_concept` | fixed |

Summary pages therefore inherit their source document's tenant scope automatically and need no configuration. Cross-document entity/concept pages cannot — they are synthesised from many documents — so they carry two dedicated contexts that a tenant opts into by listing `SRC_WIKI_ENTITY` / `SRC_WIKI_CONCEPT` among its sources.

The consequence is the cross-document scoping limitation below: a tenant reading `wiki_entity` can see facts synthesised from documents outside its own source list.

## Update model

`reconcile()` is the single entry point. State lives in one manifest blob in the wiki container:

```
{"sources": {source_path: {hash, entities, concepts}},
 "pages":   {slug: {content_hash, input_hash, source_refs, is_deleted, ...}}}
```

Five phases:

1. **Diff sources by content hash.** The corpus reader skips blobs the KB sync has tombstoned (`IsDeleted` metadata), so a retired document reaches the diff as a deletion rather than as live content. `changed` = hash differs from the manifest; `deleted` = in the manifest but absent from the corpus.
2. **Extraction.** Only changed documents are re-extracted; every other document's entities/concepts are deserialised from the manifest.
3. **Summary pages.** Only changed documents are re-summarised; the rest are reused from the manifest.
4. **Entity/concept pages.** A group's page is reused only when it already has content **and** its `source_refs` set is unchanged **and** its `input_hash` (a digest of the group name plus all member descriptions) is unchanged. So a single changed document re-synthesises only the groups that reference it.
5. **Apply.** A page is uploaded only when its rendered content hash changed. Pages that no longer exist are **soft-deleted** via `IsDeleted` blob metadata — the same convention the KB sync pipeline uses, so the shared indexer drops them — and their manifest entry is reduced to a tombstone. Documents whose summary generation failed have their stored hash cleared, so the next run retries them.

The first run against an empty manifest is a full build. A run over an unchanged corpus writes nothing and makes no LLM calls: measured end to end, ~14 minutes for a 72-document delta over 1029 sources, ~1 second of reconcile for no delta, against ~80 minutes for a full rebuild.

Two properties of this design are easy to trip over:

- **The build only writes blobs.** `azure-sdk-knowledge-wiki-indexer` projects them into the shared index on its own daily schedule, so a fresh build is not queryable until the indexer runs. Trigger it explicitly when the new pages are needed immediately.
- **Prompt edits invalidate nothing.** The diff is over *source document* content, so changing a build prompt leaves every page cache-hit and unchanged. Making prompt changes take effect requires the full-rebuild procedure in the package README: clear the wiki container (including the manifest), delete the index documents matching `page_type ne null`, rebuild, then run the indexer.

## Scheduling

`azure-sdk-qa-bot-wiki-index/build_wiki.yml` runs the build daily at 04:00 UTC, two hours after the knowledge sync that writes its input. Configuration comes from Azure App Configuration, so the pipeline itself only carries an endpoint and a service connection.

## Evaluation

227-case perf set (7 scenarios), memory off, gpt-5.4 grader, same-day back-to-back runs. A case passes only when all six core metrics score ≥ 4. The baseline is `main` with memory disabled and wiki pages filtered out of retrieval, so it measures the knowledge base alone.

| | TOTAL | typespec | apispec | python | authoring | general |
| --- | --- | --- | --- | --- | --- | --- |
| N | 227 | 125 | 26 | 24 | 26 | 20 |
| KB-only baseline (`main`, memory off) | 69.2 % | 76.8 | 57.7 | 50.0 | 76.9 | 55.0 |
| Wiki two-track | **74.9 %** | **81.6** | **65.4** | **62.5** | **80.8** | **60.0** |

Every scenario is at or above the baseline (**+5.7 pp** overall, net **+12** cases: 27 fixed, 15 regressed). Groundedness / relevance / coherence / fluency stay ~100 %, and median answer length is flat (165 → 173 words), so the gain is not bought with longer or less grounded answers — it is carried by `similarity` (79.3 → 83.7 %) and `response_completeness` (70.0 → 76.2 %).

Reading the numbers: same-config reruns churn ~16 % of cases and move the total by up to ±5 pp, so a single run cannot resolve a smaller delta. `typespec` (N = 125) is the only single scenario large enough to trust on its own; `onboarding` and `releasesupport` (N = 3) swing 33 pp on one case and are reported for completeness only.

What each design decision is worth, measured by same-day A/B:

- **Track separation is essential** — retrieving wiki pages in the same ranked pool as source chunks regresses the score, because generic pages displace the specific source doc.
- **Full symbol coverage** (always extracting named decorators/templates, including single-doc symbols, plus their constraints) moved typespec **+4.6 pp** and `response_completeness` **+5 pp** by giving symbol questions a consolidated page to route to.
- **Faithfulness rules** (scope/exception preservation in the build prompts, specific-verdict preference in the answer rules) recovered the cases where a wiki-grounded answer had been *more confident and less correct* than the KB-only one.
- **Chunk granularity beats page integrity** — raising the indexer split budget so every page indexes as one chunk cost typespec **−5.6 pp**. Retrieval matches on sections and expands to the page afterwards, so one vector per page retrieves measurably worse than one per section.

The wiki layer is only measurable when it actually returns results. A silent retrieval outage (see below) cost the entire lead for several days while every tool call still appeared to fire, so any evaluation of this feature should first assert that `wiki_search` returns a non-empty result.

Roughly 60 % of the remaining failures ask for knowledge that is absent from the indexed corpus, so they are not reachable by retrieval or prompt changes; closing them is a corpus-curation problem.

## Known limitations

- **Wiki pages have no header hierarchy.** The indexer maps `header_1` from the page title and leaves `header_2` / `header_3` null, unlike raw chunks. `KnowledgeChunk` coerces null headers to `""`, without which every wiki chunk fails validation and `wiki_search` silently returns empty. Any new index-backed field must tolerate an explicit `null`.
- **Cross-document page scoping.** Entity/concept pages carry a shared `wiki_entity` / `wiki_concept` context, so a tenant reading them can see facts synthesised from documents outside its own sources. Acceptable because the corpus is public docs and tenants map to topic channels, not access boundaries; summary pages and raw chunks stay scoped to their source `context_id`.
- **Multi-chunk page ordering.** A synthesised page larger than the indexer chunk size is split into several chunks; wiki reads reassemble by `ordinal_position`, which is not projected onto wiki chunks, so a split page can concatenate out of order. Raising the split budget so pages stay single-chunk removes the disorder but costs more than it saves — one vector per whole page retrieves measurably worse than one per section, and reassembly runs on the retrieved hit anyway. A projected ordinal is the durable fix and needs a reindex.
- **Prompt changes need a manual full rebuild.** Reconcile diffs by source content hash, so editing a build prompt does not invalidate any page. Changing what pages should contain requires clearing the wiki container and the wiki documents in the index, then a full build.
