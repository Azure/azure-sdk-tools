# GraphRAG Knowledge-Graph Retrieval — Design

## 1 Background

The Azure SDK QA Bot answers developer questions by grounding a chat agent on a curated
knowledge corpus (TypeSpec docs, ARM/API guidelines, SDK repo docs, samples, resolved
support threads). Until now the only retrieval path was **vector / agentic search over an
Azure AI Search index** (the "KB path"). Vector search excels at **single-concept,
definitional, verbatim-rule** lookups, but it retrieves each chunk independently and has no
notion of how entities relate **across** documents — so it under-serves **relational,
multi-hop, cross-document, and troubleshooting** questions where the answer depends on
connecting facts that live in different chunks.

This design adds a second, complementary retrieval path built on **Microsoft GraphRAG**
(entity-graph traversal) that runs **side-by-side** with the existing KB path. The agent
issues both retrievers in parallel and synthesises one answer over the merged references.

> This document describes the GraphRAG update and how it is used, and explicitly contrasts
> it with the original KB architecture. It complements
> [`agent_framework_and_memory_design.md`](agent_framework_and_memory_design.md).

### 1.1 Goals / non-goals

- **Goal** — add graph-grounded recall for relational/cross-document questions **without
  regressing** the KB path or the concise (150–200 word) answer style.
- **Goal** — keep the graph reference shape **identical** to KB references so the agent and
  UI treat both uniformly.
- **Goal** — tenant-scope graph retrieval exactly like the KB tool (same `KnowledgeSource` /
  `source_filter` semantics).
- **Non-goal** — replacing the KB path. GraphRAG is additive; the two are weighted per
  question type (§5).
- **Non-goal** — running GraphRAG's own answer-generation (global/local *search* LLM step).
  We use only its **context-building** half and let the chat agent compose the answer.

---

## 2 Original architecture (the KB path we contrast with)

### 2.1 Corpus build — `azure-sdk-qa-bot-knowledge-sync` (TypeScript)

`processDailySyncKnowledge()` clones/updates the source repos, converts content to markdown
(`TypeSpecProcessor` for `.tsp`, `SampleProcessor` for samples, both emitting under
`generated/`), uploads changed files to blob storage with `scope` / `service_type` metadata,
and **incrementally** updates the Azure AI Search index — deleting the chunks of changed
files (`SearchService.deleteDocumentChunksByFileName`) and re-indexing them. Repos/paths are
declared in `config/knowledge-config.json` (e.g. `typespec_docs`, `typespec_azure_docs`,
`azure_sdk_for_python_docs`, `azure_api_guidelines`, `azure_resource_manager_rpc`).

### 2.2 Retrieval — `search_knowledge_base` (`tools/knowledge_tools.py`)

- Runs **agentic** (`KnowledgeBaseRetrievalClient.retrieve`, `include_references=True`) and/or
  **vector/semantic** search over `text_vector` on the AI Search index
  (`AI_SEARCH_INDEX` / `AI_SEARCH_KNOWLEDGE_BASE`), selecting `chunk`, `context_id`,
  `header_1/2/3`, `scope`, `service_type`, etc.
- **Tenant scoping** (`_resolve_source_filters`) starts from `context_id eq '<source_name>'`,
  then ANDs the tenant's per-source `source_filter` OData clauses (e.g.
  `search.ismatch('python','title')`) and an optional service-type filter.
- Results are deduped and **expanded by header hierarchy** (`expand_by_hierarchy`) and
  returned as `KnowledgeChunk`/`Reference` items (`source` ← `context_id`, `content` ← `chunk`).

**Characteristics.** Flat chunk recall, per-document, no cross-document linking; incremental
index updates; tenant filter via `context_id` + OData.

---

## 3 GraphRAG update — the graph build (`azure-sdk-qa-bot-knowledge-graph-sync`)

A **new, separate** Python project that indexes the *same* knowledge corpus into a GraphRAG
graph and publishes it as versioned snapshots. It does **not** touch the KB index.

### 3.1 Build flow (`main.py` → `run_indexing.py` → `publish_output.py`)

1. `init_configuration()` → `init_secrets()` → `run_graphrag_pipeline()` → `publish_manifest()`.
2. **Input** — reads docs **directly** from the KB corpus blob container
   (`STORAGE_KNOWLEDGE_CONTAINER`) via GraphRAG's `azure_blob` input — i.e. it consumes the
   *same* markdown the KB path indexes, so the two stay corpus-consistent.
3. **Source attribution** — `SourceAwareMarkItDownFileReader` (overrides GraphRAG's
   `MarkItDownFileReader`) tags every `TextDocument.raw_data` with `source_folder` (the
   `KnowledgeSource` name) and `source_path` (full input path), and sets a unique `title`.
   This lets the bot attribute each graph hit back to a concrete `KnowledgeSource` and resolve
   a **KB-consistent link**, independent of GraphRAG's own `documents.title`.
4. **Indexing** — `build_index(..., method=IndexingMethod.Standard, is_update_run=False)`:
   a **full build only** (README: "Full rebuild each run"), for predictable, reproducible
   snapshots. Azure AI Search is the vector store for entity/community embeddings.
5. **Output** — writes the GraphRAG parquet artefacts to
   `STORAGE_GRAPHRAG_OUTPUT_CONTAINER/snapshots/<UTC-ts>-<short>/`:
   `entities`, `communities`, `community_reports`, `text_units`, `relationships`, `documents`.
6. **Manifest** — `publish_output.py` writes a `latest.json` pointer at the container root:
   `{ prefix, built_at (UTC ISO), build_id, files: [...6 parquet names] }`. This is the
   atomic "which snapshot is current" switch.

### 3.2 Trigger

`sync_knowledge_graph.yml` runs **daily at 03:00 UTC** (`trigger: none`, `pr: none`);
`ci.yml` gates PRs to this project path. Full rebuild → new snapshot dir → new `latest.json`.

---

## 4 GraphRAG update — bot-side retrieval

### 4.1 Where it runs: a warm backend service, not the sandbox

GraphRAG's Local Search has a **~40 s cold start** (loading parquets + building the context
builder). Paying that per chat-agent sandbox is untenable. Instead the **backend** keeps a
warm `KnowledgeGraphService` singleton and exposes it over HTTP:

- **`POST /graph/query`** (`server.py`), request `GraphQueryRequest { query, tenant_id? }`,
  response `GraphSearchResult { references: Reference[], query }` (mirrored in the TypeSpec
  contract `tsp/models.tsp`).
- The chat agent calls it via the **`search_knowledge_graph`** function tool
  (`tools/graph_knowledge_tools.py`), which POSTs `{query, tenant_id}` to `GRAPH_QUERY_URL`
  with a bearer token for `GRAPH_QUERY_AUDIENCE`, and returns `GraphSearchResult` (empty on
  any failure — the tool is best-effort and never breaks a turn).

Per query the warm path is ~1–2 s: one embedding + one AI Search ANN + in-memory DataFrame
joins.

### 4.2 Context-building only (no completion LLM)

`KnowledgeGraphService.search_graph` runs **only the context-building half** of GraphRAG
Local Search: embed the query → entity ANN over the graph's entity embeddings → 1-hop
expansion through `relationships` → resolve back to the **source text units** the matched
entities appear in. **No GraphRAG completion call is made** — `extraction.py` converts the
context records into `Reference` objects (same shape as KB chunks), and the chat agent
synthesises the final answer over those verbatim snippets, exactly as it does for KB chunks.

### 4.3 Retrieval tuning (the "opt2×lref" config)

The default Local Search context spends ~15 % of its token budget assembling **community
reports**, which the refs-only extraction then discards. We reclaim that budget for source
text units and widen recall (all `GRAPH_LS_*` env-overridable, `engine.py` / `settings.yaml`
/ `server.py`):

| Knob | Default | Ours | Why |
|---|---|---|---|
| `community_prop` | 0.15 | **0.0** | community reports aren't emitted as refs → wasted budget |
| `text_unit_prop` | 0.5 | **0.8** | more source text units per query = more citable refs |
| `max_context_tokens` | 12000 | **16000** | larger context window |
| `top_k_entities` / `top_k_relationships` | (5) | **10** | wider 1-hop recall |
| graph snippet cap (`_GRAPH_SNIPPET_MAX_CHARS`) | 1200 | **3000** | more verbatim text shown per cited ref |

A one-time **community-report embedding preload** (paged `search('*')`, with a
`results_per_page` fallback for azure-search-documents version differences) replaces thousands
of serial per-query `search_by_id` round-trips (~520 s → ~116 s on a full graph).

### 4.4 Tenant-scoped filtering (two layers, mirroring the KB tool)

`search_graph` receives the same tenant scoping the KB tool uses, translated in
`filtering.py`:

1. **Source-folder layer** — restrict graph retrieval to entities whose source documents
   belong to the tenant's `KnowledgeSource` set (`allowed_source_folders` from
   `KnowledgeSource.name`), the graph equivalent of the KB tool's `context_id eq '<source>'`.
   Implemented via a per-snapshot reverse index + an ANN-store wrapper with oversampling.
2. **File (`source_path`) layer** — the tenant's per-source `source_filter` OData clauses
   (e.g. `search.ismatch('python','title')`) are parsed to case-insensitive terms
   (`parse_title_filter_terms`) and matched against each document's `source_path`, so a tenant
   that narrows a shared source by title narrows the graph the same way.

### 4.5 Daily snapshot refresh (pulled, not pushed)

The backend runs a poll loop: sleep `GRAPH_RELOAD_POLL_SECONDS` (default `86400`), then
`service.reload_if_changed()` reads `latest.json`, compares `build_id`, and hot-swaps the
snapshot only when it changed (`service.py`, `loading.py`). Reloads are atomic with full
rollback on failure, so a broken snapshot never leaves the service half-loaded.

---

## 5 Hybrid synthesis — how the two paths combine

Both tools are **mandatory** on every domain question and are issued in the **same parallel
batch**. `chat_service._merge_references(vector_refs, graph_refs)` merges the two reference
sets, **deduplicating by `link`** so a KB (vector, primary-source) hit wins over a graph hit
for the same document; refs without a link are always kept.

The agent instruction **weights the two sources by question type** (tuned from case-level
bad-case analysis) rather than concatenating every snippet equally:

- **Definitional / decorator / language-feature questions → KB is the backbone**, graph is
  confirmation-only, with an anti-dilution rule (don't surface adjacent/legacy mechanisms the
  user didn't ask about; prefer the KB answer if graph contradicts it).
- **Process / workflow / permissions / CI / release / cross-team questions → graph is the
  backbone**; KB grounds exact wording/links.

Answer length target stays **150–200 words**.

---

## 6 Key differences from the original architecture

| Dimension | Original KB path | GraphRAG path (this update) |
|---|---|---|
| Retrieval model | Vector / semantic + agentic search over AI Search chunks | Entity-graph traversal: query → entity ANN → 1-hop → source text units |
| Best at | Single-concept, definitional, verbatim rules, code snippets | Relational, multi-hop, cross-document, troubleshooting |
| Corpus build | `azure-sdk-qa-bot-knowledge-sync` (TS), **incremental** index updates | `azure-sdk-qa-bot-knowledge-graph-sync` (Py), **full rebuild** → versioned snapshots |
| Artefact | AI Search index (chunks + `text_vector`) | 6 parquet files per snapshot + `latest.json` manifest in blob |
| Refresh | Incremental per changed file | Daily full rebuild; backend **pulls** new snapshot via `latest.json` poll |
| Where retrieval runs | Inline AI Search client | Warm `KnowledgeGraphService` behind `POST /graph/query` (avoids ~40 s cold start) |
| Answer generation | Agent composes over chunks | Agent composes over graph refs — **no GraphRAG completion LLM call** |
| Tenant scoping | `context_id eq '<source>'` + OData `source_filter` | `allowed_source_folders` (reverse index) + `source_path` term match — same semantics |
| Reference shape | `Reference` (`source`←`context_id`, `content`←`chunk`) | **Same** `Reference` (source_folder attribution, KB-consistent link) |
| Relationship to each other | — | **Additive & complementary**; merged + deduped by `link`, weighted by question type |

**What did *not* change:** the KB path, the AI Search index, the agent framework, the memory
design, the answer style, and the `Reference` contract the UI renders. GraphRAG is a bolt-on
second retriever that reuses the same corpus, the same tenant model, and the same reference
shape.

---

## 7 Evaluation & operational notes

- **Evaluation must disable memory** (`ENABLE_MEMORY=false`): the agent otherwise injects
  historical Q&A from the memory stores, and since the perf datasets are built from prior
  Q&A, that leaks ground-truth and biases results. See the evaluation project README.
- **Latest 223-case perf result (memory off, threshold 4, concise ~155–165 words):** KB-only
  **78.0 %** vs Graph RAG **81.2 %**. Case-level analysis shows Graph's advantage is
  concentrated in its designed strengths (relational / troubleshooting / versioning), with
  small dilutions on definitional / corpus-gap questions — the KB/graph weighting in §5 is what
  keeps those in check. Remaining failures are dominated by **corpus gaps** (short,
  thread-specific facts absent from the indexed corpus); the next lever is curated corpus
  expansion, not further retrieval/prompt tuning.

### 7.1 Config reference (bot side)

| Env | Default | Purpose |
|---|---|---|
| `GRAPH_QUERY_URL` / `GRAPH_QUERY_AUDIENCE` | — | backend `/graph/query` endpoint + token audience |
| `GRAPH_LS_COMMUNITY_PROP` | `0.0` | Local Search community-report token proportion |
| `GRAPH_LS_TEXT_UNIT_PROP` | `0.8` | Local Search text-unit token proportion |
| `GRAPH_LS_MAX_CONTEXT_TOKENS` | `16000` | Local Search context window |
| `GRAPH_RELOAD_POLL_SECONDS` | `86400` | snapshot `latest.json` poll interval |
| `ENABLE_MEMORY` | `true` | set `false` for evaluations (see §7) |
