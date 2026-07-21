# azure-sdk-qa-bot-wiki-index

Builds LLM-derived **knowledge layers** and pushes them into the same Azure AI
Search index that backs the Azure SDK QA bot's knowledge base, so the agent can
answer from *internalized knowledge* instead of re-reading raw documentation
chunks.

Everything fuses at the **index** level: generated pages become ordinary
documents in the shared index (same vector + keyword fields), so the existing
retrieval path (hybrid search → RRF → rerank → hierarchy expansion) surfaces and
ranks them alongside raw chunks with no query-time changes.

## Two independent pipelines

Mirroring WeKnora's separate `WikiEnabled` / `GraphEnabled` toggles, wiki and
graph creation are **fully decoupled** — different inputs, different artifacts,
run independently:

| Pipeline | What it does | Artifacts (`page_type`) | `context_id` |
| -------- | ------------ | ----------------------- | ------------ |
| **wiki** | per-document LLM synthesis of one dense knowledge page per source doc | `wiki` | inherited source folder → resolves to the source doc |
| **graph** | LLM entity extraction + LLM relationship extraction (strength), PMI+strength weights, degree, 1-hop/2-hop edges | `entity`, `relationship` | `wiki_entity`, `wiki_relationship` |

### Wiki pipeline (`wiki.py`)
One synthesised page per document — dense, declarative expert facts (definitions,
exact decorator/API names, rules, defaults, gotchas), no navigation phrases,
nothing invented. Equivalent to WeKnora's `ChunkTypeWikiPage`.

### Graph pipeline (`graph.py`, faithful to WeKnora `graph.go`)
1. **Entity extraction** (`graph_extract.extract_entities`) — per-document LLM
   call → `{title, type, description}`; deduped by title, `chunk_ids`/frequency
   accumulate. (Concurrency 4.)
2. **Relationship extraction** (`graph_extract.extract_relationships`) — docs in
   batches of 5 → LLM `{source, target, description, strength 1-10}`; duplicates
   merge via strength weighted-average. (Concurrency 4.)
3. **Weights** (`graph_weights.compute_weights`) —
   `PMI = max(log2(P(x,y)/(P(x)·P(y))), 0)`;
   `weight = 1 + 9·(0.6·normPMI + 0.4·normStrength)` → range `[1, 10]`.
4. **Degrees** (`graph_weights.compute_degrees`) — entity in+out degree;
   relationship combined degree.
5. **Edges** (`graph_weights.build_entity_edges`) — 1-hop direct + 2-hop indirect
   (decay `0.5`), ranked by weight then degree → each entity's `related_slugs`.
6. Emits `entity` and `relationship` pages (WeKnora `ChunkTypeEntity` /
   `ChunkTypeRelationship`).

Extraction is intentionally **LLM-based** (not regex) to match WeKnora exactly.

## Usage

```bash
pip install -r requirements.txt

# wiki layer only, one folder
python -m azure_sdk_qa_bot_wiki_index.main --build wiki  --prefix typespec_docs/

# graph layer only
python -m azure_sdk_qa_bot_wiki_index.main --build graph --prefix typespec_docs/

# both layers, whole corpus
python -m azure_sdk_qa_bot_wiki_index.main --build all

# generate + embed but do not push (inspect output)
python -m azure_sdk_qa_bot_wiki_index.main --build graph --prefix typespec_docs/ --dry-run

# purge just the graph layer, leaving wiki intact
python -m azure_sdk_qa_bot_wiki_index.main --build graph --purge --no-generate
```

## Configuration (environment variables)

| Variable | Default | Purpose |
| -------- | ------- | ------- |
| `AI_SEARCH_BASE_URL` | — | Azure AI Search endpoint |
| `AI_SEARCH_INDEX` | — | target index (shared with the KB) |
| `STORAGE_BLOB_ENDPOINT` | — | blob account endpoint |
| `STORAGE_KNOWLEDGE_CONTAINER` | `knowledge` | knowledge container name |
| `AZURE_OPENAI_ENDPOINT` | — | Azure OpenAI endpoint |
| `WIKI_SYNTHESIS_DEPLOYMENT` | `gpt-5.4` | chat deployment (synthesis + extraction) |
| `WIKI_EMBEDDING_DEPLOYMENT` | `text-embedding-ada-002` | embedding deployment (must match the index) |

Authentication is AAD via `DefaultAzureCredential`; an `AZURE_OPENAI_API_KEY` is
used for Azure OpenAI if present.

## Index field mapping

Chosen so the existing retrieval code treats generated pages as first-class chunks:

* `chunk_id` — `wiki-<type>-<hash>` (unique key, no leading underscore).
* `title` — for `wiki`, the source's `#`-encoded rel path (link resolution finds
  the real doc); for `entity`/`relationship`, a slug.
* `header_1` — a distinct `"… (knowledge/entity/relationship)"` heading so the
  page is isolated in hierarchy expansion and becomes its own reference title.
* `chunk` — the page text (embedded into `text_vector`).
* `context_id` — drives tenant scoping.
* `chunk_refs` — source rel paths the page was built from.
* `related_slugs` — graph edges (neighbour entity slugs / endpoint slugs).
* `page_type` — `wiki` | `entity` | `relationship`.

All string fields are written as `""` (never null).

## Prerequisites

The target index must carry the additive fields `chunk_refs`, `related_slugs`
(both `Collection(Edm.String)`) and `page_type` (`String`), and the tenant
configuration must register the `wiki_entity` / `wiki_relationship` knowledge
sources for graph pages to be in scope. (Wiki pages need no config — they inherit
their source's `context_id`.)
