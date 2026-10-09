# Wiki Knowledge-Layer Retrieval — Design

## Background

The QA bot grounds its answers on a curated corpus (TypeSpec docs, ARM/API guidelines, SDK repo docs, samples, resolved threads). Retrieval uses two complementary views of that corpus: authoritative source chunks for exact evidence and a **wiki knowledge layer** for consolidated cross-document context.

The wiki is an offline, LLM-generated set of pages that distils the corpus into per-document **summaries**, per-symbol **entity** pages, and per-topic **concept** pages. It is additive and rebuildable; source chunks remain the authoritative grounding.

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

**Wiki pages and source chunks never fuse into one ranked list.** Keeping the tracks separate prevents broad generated pages from displacing exact source evidence; the answer combines evidence from both tracks.

- **`search_knowledge_base`** — source chunks only (`page_type` null).
- **`wiki_search`** — wiki pages only, **self-contained**: for the top pages it returns bounded synthesized content **plus** query-ranked source chunks from the documents recorded in `chunk_refs`.

Both tracks run the same retrieval pipeline (`SearchClient.fused_search`) and differ only by page-type filter: dense + BM25 (+ agentic in `deep` mode) run in parallel for every query, all query/retriever rankings are fused together with RRF, then the caller dedupes and caps. Retrieval uses a wider candidate pool than the final answer budget.

For most questions the agent issues `search_knowledge_base` + `wiki_search` in one parallel batch and answers on the next turn.

Wiki results use separate content budgets for synthesized pages and routed source chunks. The exact matched source passage is placed first before truncation. This keeps every ranked reference available while bounding the hosted-agent tool payload.

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

Summary pages therefore inherit their source document's tenant scope automatically and need no configuration. Cross-document entity/concept pages cannot — they are synthesised from many documents — so they carry two internal contexts. These contexts are not registered knowledge sources; `wiki_search` adds their filters by default, and a tenant can opt out with `enable_wiki_cross_document_pages=False`.

The consequence is the cross-document scoping limitation below: a tenant with cross-document Wiki pages enabled can see facts synthesised from documents outside its own source list.

## Update model

`reconcile()` is the single entry point. State lives in one manifest blob in the wiki container:

```
{"sources": {source_path: {hash, entities, concepts}},
 "pages":   {slug: {content_hash, input_hash, source_refs, is_deleted, ...}}}
```

Five phases:

1. **Diff sources by content hash and generation identity.** The corpus reader skips blobs the KB sync has tombstoned (`IsDeleted` metadata) and empty/whitespace-only documents, so retired or unusable documents do not enter generation. `changed` = hash differs from the manifest; `deleted` = in the manifest but absent from the corpus. The generation identity includes the synthesis model, prompt hashes, minimum-document threshold, manifest version, and build-logic version; changing any of them invalidates cached generation.
2. **Extraction.** Only changed documents are re-extracted; every other document's entities/concepts are deserialised from the manifest.
3. **Summary pages.** Only changed documents are re-summarised; the rest are reused from the manifest.
4. **Entity/concept pages.** A group's page is reused only when it already has content **and** its `source_refs` set is unchanged **and** its `input_hash` (a digest of the group name plus all member descriptions) is unchanged. So a single changed document re-synthesises only the groups that reference it.
5. **Apply.** A page is uploaded only when its rendered content hash changed. Pages that no longer exist are **soft-deleted** via `IsDeleted` blob metadata — the same convention the KB sync pipeline uses, so the shared indexer drops them — and their manifest entry is reduced to a tombstone. Documents whose extraction or summary generation failed have their stored hash cleared, so the next run retries them. A transient entity/concept synthesis failure preserves the prior active page and leaves its input hash empty for retry.

The first run against an empty manifest is a full build. A run over an unchanged corpus writes nothing and makes no LLM calls.

Operational requirements:

- **The build only writes blobs.** `azure-sdk-knowledge-wiki-indexer` projects them into the shared index on its own daily schedule, so a fresh build is not queryable until the indexer runs. Trigger it explicitly when the new pages are needed immediately.
- **A clean physical rebuild is still operationally distinct.** Prompt/model/build changes invalidate cached generation automatically, but recreating the blob and search state still requires clearing the wiki container, deleting only index documents matching `page_type ne null`, rebuilding, then running the dedicated indexer.

## Scheduling

`azure-sdk-qa-bot-wiki-index/build_wiki.yml` runs the build daily at 04:00 UTC, two hours after the knowledge sync that writes its input. Configuration comes from Azure App Configuration, so the pipeline itself only carries an endpoint and a service connection.

## Evaluation

The retained benchmark is the best complete A/B on the 225-case perf set (seven scenarios), with memory disabled and GPT-5.4 grading. A case passes only when all six core metrics score at least 4. The baseline filters wiki pages out of retrieval and measures the source knowledge base alone.

| | TOTAL | typespec | apispec | python | authoring | general | onboarding + release support |
| --- | --- | --- | --- | --- | --- | --- | --- |
| N | 225 | 125 | 25 | 24 | 26 | 19 | 6 |
| KB-only baseline | 69.3 % | 76.8 | 60.0 | 50.0 | 76.9 | 52.6 | 50.0 |
| Wiki two-track | **75.6 %** | **81.6** | **68.0** | **62.5** | **80.8** | **63.2** | 50.0 |

Wiki retrieval improves the pass rate from 156/225 to 170/225: **+6.2 percentage points**, with 27 fixed cases, 14 regressions, and a net gain of 13. Similarity improves from 79.1 % to 84.0 % and response completeness from 70.2 % to 76.9 %, while groundedness, relevance, coherence, and fluency remain approximately 100 %. Median answer length changes from 165 to 173 words.

The result supports the final retrieval invariants: keep synthesized and authoritative evidence on separate ranking tracks, retrieve Wiki content at chunk level before page expansion, preserve scope and exceptions in generated pages, and route selected pages back to query-ranked source chunks.

## Known limitations

- **Wiki pages have no header hierarchy.** The indexer maps `header_1` from the page title and leaves `header_2` / `header_3` null, unlike raw chunks. `KnowledgeChunk` normalizes null headers to `""`; any new index-backed field must also tolerate an explicit `null`.
- **Cross-document page scoping.** Entity/concept pages carry a shared `wiki_entity` / `wiki_concept` context, so a tenant reading them can see facts synthesised from documents outside its own sources. Acceptable because the corpus is public docs and tenants map to topic channels, not access boundaries; summary pages and raw chunks stay scoped to their source `context_id`.
- **Multi-chunk page ordering.** A synthesised page larger than the indexer chunk size is split into several chunks. Wiki reads reassemble by `ordinal_position`, which is not projected onto wiki chunks, so a split page can concatenate out of order. Projecting the ordinal and reindexing is required for deterministic assembly.
- **Document-level provenance.** `chunk_refs` identifies source documents rather than exact supporting spans. Query-time backfill reranks chunks within those referenced documents, but generated claims are not yet bound to stable source chunk IDs.
