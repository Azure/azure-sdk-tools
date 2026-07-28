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

## Freshness and tenant scoping

- **Incremental reconcile.** The build diffs the corpus against the manifest by content hash: only changed/new documents are re-extracted and their summaries regenerated; entity/concept pages are re-synthesised only when a group's source set or content changed; removed documents have their pages soft-deleted (`IsDeleted` blob metadata, matching the KB sync pipeline, so the shared indexer drops them). The first run against an empty manifest is a full build. Documents whose summary generation fails keep an empty hash so the next run retries them, and a run that would drop more than half of the known sources aborts rather than tombstone the wiki over an incomplete upstream corpus.
- **Scheduling.** `azure-sdk-qa-bot-wiki-index/build_wiki.yml` runs the build daily at 04:00 UTC, two hours after the knowledge sync that writes its input. Configuration comes from Azure App Configuration, so the pipeline itself only carries an endpoint and a service connection.
- **Tenant scoping** reuses the KB tool's source scoping. Summary pages inherit their source document's `context_id`; cross-document entity/concept pages carry dedicated `wiki_entity` / `wiki_concept` contexts registered as tenant sources.

## Evaluation

226-case perf set (7 scenarios), memory off, gpt-5.4 grader, same-day runs. A case passes only when all six core metrics score ≥ 4. Same-config reruns move by up to ~4.6 pp, so only larger deltas are treated as signal.

| | TOTAL | apispec | python | authoring | typespec |
| --- | --- | --- | --- | --- | --- |
| KB-only baseline (`main`, memory off) | 67.6 % | 44.0 | 52.2 | 76.9 | 77.4 |
| Wiki two-track | **73.3 %** | 64.0 | 60.9 | 84.6 | 79.8 |

Every scenario is at or above the baseline (**+5.7 pp** overall). Groundedness / relevance / coherence / fluency stay ~100 %; the gain is carried by `similarity` and `response_completeness`.

What each design decision is worth, measured by same-day A/B:

- **Track separation is essential** — retrieving wiki pages in the same ranked pool as source chunks regresses the score, because generic pages displace the specific source doc.
- **Full symbol coverage** (always extracting named decorators/templates, including single-doc symbols, plus their constraints) moved typespec **+4.6 pp** and `response_completeness` **+5 pp** by giving symbol questions a consolidated page to route to.
- **Faithfulness rules** (scope/exception preservation in the build prompts, specific-verdict preference in the answer rules) recovered the cases where a wiki-grounded answer had been *more confident and less correct* than the KB-only one.

The wiki layer is only measurable when it actually returns results. A silent retrieval outage (see below) cost the entire lead for several days while every tool call still appeared to fire, so any evaluation of this feature should first assert that `wiki_search` returns a non-empty result.

## Known limitations

- **Wiki pages have no header hierarchy.** The indexer maps `header_1` from the page title and leaves `header_2` / `header_3` null, unlike raw chunks. `KnowledgeChunk` coerces null headers to `""`, without which every wiki chunk fails validation and `wiki_search` silently returns empty. Any new index-backed field must tolerate an explicit `null`.
- **Cross-document page scoping.** Entity/concept pages carry a shared `wiki_entity` / `wiki_concept` context, so a tenant reading them can see facts synthesised from documents outside its own sources. Acceptable because the corpus is public docs and tenants map to topic channels, not access boundaries; summary pages and raw chunks stay scoped to their source `context_id`.
- **Multi-chunk page ordering.** A synthesised page larger than the indexer chunk size is split into several chunks; wiki reads reassemble by `ordinal_position`, which is not projected onto wiki chunks, so a split page can concatenate out of order. Mitigated by keeping pages within the single-chunk budget; a projected ordinal is the durable fix and needs a reindex.
- **Prompt changes need a manual full rebuild.** Reconcile diffs by source content hash, so editing a build prompt does not invalidate any page. Changing what pages should contain requires clearing the wiki container and the wiki documents in the index, then a full build.
