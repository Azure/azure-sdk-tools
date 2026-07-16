# azure-sdk-qa-bot-wiki-index

Builds **knowledge wiki pages** and pushes them into the same Azure AI Search
index that backs the Azure SDK QA bot's knowledge base, so the agent can answer
from *internalized knowledge* instead of re-reading raw documentation chunks.

The wiki layer is fused at the **index** level: generated pages become ordinary
documents in the shared index (same vector + keyword fields), so the existing
retrieval path (hybrid search → RRF → rerank → hierarchy expansion) surfaces and
ranks them alongside raw chunks with no query-time changes.

## Page types

| Page type | Scope | Source | `context_id` | Link |
| --------- | ----- | ------ | ------------ | ---- |
| `summary` | one per source document | full document text | inherited source folder | resolves to the source doc |
| `entity`  | one per recurring symbol (`@added`, `TrackedResource`, …) | excerpts across documents | `wiki_entity` | none |
| `concept` | one per core topic (versioning, LRO, pagination, …) | excerpts across documents | `wiki_concept` | none |

Each page is:

* **synthesised** by a reasoning chat model into dense, declarative expert facts
  (definitions, exact decorator/API names, rules, defaults, gotchas) — no
  navigation phrases, nothing invented;
* **embedded** with the index's own embedding model (`text-embedding-ada-002`)
  so it shares the KB vector space;
* **upserted** with a stable key (`wiki-<type>-<hash>`) so re-runs are idempotent
  and never disturb the indexer-managed raw chunks.

## Index field mapping

Chosen so the existing retrieval code treats wiki pages as first-class chunks:

* `chunk_id` — `wiki-<type>-<hash>` (unique key, no leading underscore).
* `title` — for `summary`, the source's `#`-encoded rel path (so link resolution
  finds the real doc); for `entity`/`concept`, a slug.
* `header_1` — a distinct `"… (knowledge)"` heading so the page is isolated in
  hierarchy expansion and becomes its own reference title.
* `chunk` — the synthesised page text (embedded into `text_vector`).
* `context_id` — drives tenant scoping (inherited folder, or `wiki_entity` /
  `wiki_concept`).
* `chunk_refs` — source rel paths the page was built from.
* `related_slugs` — page-to-page links.
* `page_type` — `summary` | `entity` | `concept`.

All string fields are written as `""` (never null).

## Usage

```bash
pip install -r requirements.txt

# per-document summary cards for one source folder
python -m azure_sdk_qa_bot_wiki_index.main --pages summary --prefix typespec_docs/

# everything, whole corpus
python -m azure_sdk_qa_bot_wiki_index.main --pages summary,entity,concept

# generate + embed but do not push (inspect output)
python -m azure_sdk_qa_bot_wiki_index.main --pages summary --prefix typespec_docs/ --dry-run

# delete every previously-pushed wiki page
python -m azure_sdk_qa_bot_wiki_index.main --purge
```

## Configuration (environment variables)

| Variable | Default | Purpose |
| -------- | ------- | ------- |
| `AI_SEARCH_BASE_URL` | — | Azure AI Search endpoint |
| `AI_SEARCH_INDEX` | — | target index (shared with the KB) |
| `STORAGE_BLOB_ENDPOINT` | — | blob account endpoint |
| `STORAGE_KNOWLEDGE_CONTAINER` | `knowledge` | knowledge container name |
| `AZURE_OPENAI_ENDPOINT` | — | Azure OpenAI endpoint |
| `WIKI_SYNTHESIS_DEPLOYMENT` | `gpt-5.4` | chat deployment for synthesis |
| `WIKI_EMBEDDING_DEPLOYMENT` | `text-embedding-ada-002` | embedding deployment (must match the index) |

Authentication is AAD via `DefaultAzureCredential` (no keys required); an
`AZURE_OPENAI_API_KEY` is used for Azure OpenAI if present.

## Prerequisites

The target index must already carry the additive fields `chunk_refs`,
`related_slugs` (both `Collection(Edm.String)`) and `page_type` (`String`), and
the tenant configuration must register the `wiki_entity` / `wiki_concept`
knowledge sources for entity/concept pages to be in scope.
