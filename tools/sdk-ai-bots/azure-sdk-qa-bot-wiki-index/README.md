# azure-sdk-qa-bot-wiki-index

Builds LLM-derived wiki pages and pushes them into the Azure SDK QA bot's knowledge base.

Generated pages are written as markdown blobs and projected into the shared Azure AI Search index with the same vector and keyword fields as raw knowledge chunks.

## Wiki pipeline

The pipeline creates these generated page types:

| Page type | What it contains | `context_id` |
| --------- | ---------------- | ------------ |
| `summary` | one synthesized knowledge page per source document | inherited source folder |
| `entity` | cross-document page for a recurring symbol | `wiki_entity` |
| `concept` | cross-document page for a recurring topic | `wiki_concept` |

The full build extracts entities and concepts per document, aggregates recurring items, synthesizes generated pages, and writes the manifest.

Every LLM system prompt lives in `wiki_index/prompts/` as markdown (`extract.md` for the map phase, `summary.md` for per-document summary pages, `compile.md` for entity/concept pages) and is loaded by `llm.load_prompt`, so prompts can be tuned without touching code.

## Usage

```bash
pip install -r requirements.txt

# generate and persist wiki pages for the whole knowledge corpus
python -m wiki_index.main
```

`build_wiki.yml` runs this daily at 04:00 UTC, two hours after the knowledge sync that produces its input.

### Incremental reconcile

State lives in a manifest blob in the wiki container, holding a content hash plus the extracted entities/concepts per source document, and the content hash, input hash, and source refs per page. Each run:

1. **Diffs sources by content hash.** The corpus read skips blobs the knowledge sync has tombstoned (`IsDeleted` metadata rather than an actual delete), so a retired document arrives as a deletion instead of as live content.
2. **Re-extracts only changed documents**; the rest are read back from the manifest.
3. **Re-summarises only changed documents.**
4. **Re-synthesises an entity/concept page** only when its group gained or lost a source document, or when its members' descriptions changed. One changed document therefore touches only the groups that reference it.
5. **Uploads a page only when its rendered content changed.** Pages whose sources are gone are soft-deleted via `IsDeleted` blob metadata — the same convention the knowledge sync uses, so the shared indexer drops them. Documents whose summary generation failed have their stored hash cleared so the next run retries them.

The manifest also records a generation fingerprint containing the synthesis
model, prompt hashes, minimum-document threshold, manifest version, and build
logic version. Changing any of them automatically invalidates cached
extractions and pages. A transient entity/concept synthesis failure preserves
the prior active page and leaves it marked for retry.

The first run against an empty manifest is a full build.

The build only writes blobs; `azure-sdk-knowledge-wiki-indexer` projects them into the search index on its own daily schedule, so a fresh build is not queryable until the indexer runs.

### Full rebuild

Prompt, model, and build-logic changes invalidate cached generation
automatically. To recreate the physical wiki blobs and search documents from a
clean state:

1. Delete every blob in the wiki container (this removes the manifest, so the next run treats the corpus as new).
2. Delete the wiki documents from the shared index — everything matching `page_type ne null`; raw knowledge chunks have a null `page_type` and must be left alone.
3. Run the pipeline, then run the indexer manually rather than waiting for its daily schedule.

## Tenant scoping

The agent scopes retrieval with an OData filter on `context_id`, using the source names each tenant is configured with. Pages join that scheme through the `context_id` written at build time:

* `summary` pages take the **first path segment of their source blob** (`typespec_docs/x.md` → `typespec_docs`), so they inherit their source document's tenant scope with no configuration.
* `entity` / `concept` pages are synthesized from many documents and cannot inherit one scope, so they carry the fixed `wiki_entity` / `wiki_concept` contexts. These are internal index contexts, not knowledge sources; `enable_wiki_cross_document_pages` controls them per tenant and defaults to enabled.

Because the cross-document contexts are shared, a tenant reading them can see facts synthesized from documents outside its own source list. This is accepted: the corpus is public documentation and tenants map to topic channels, not access boundaries.

`wiki_search` adds the internal entity/concept context filters unless a tenant explicitly disables them. Its source backfill remains scoped to the tenant's ordinary knowledge sources, so the internal Wiki contexts never enter raw retrieval or the model-selectable source list.

## Configuration

Settings are read from the environment first and then from Azure App Configuration (`AZURE_APPCONFIG_ENDPOINT`), which is how the pipeline supplies them.

| Variable | Default | Purpose |
| -------- | ------- | ------- |
| `AI_SEARCH_BASE_URL` | — | Azure AI Search endpoint |
| `AI_SEARCH_INDEX` | `azure-sdk-knowledge` | target index shared with the KB |
| `STORAGE_BLOB_ENDPOINT` | `STORAGE_BASE_URL` | blob account endpoint |
| `STORAGE_KNOWLEDGE_CONTAINER` | `knowledge` | source knowledge container |
| `STORAGE_WIKI_OUTPUT_CONTAINER` | `wiki` | generated wiki container |
| `AZURE_OPENAI_ENDPOINT` | — | Azure OpenAI endpoint |
| `WIKI_SYNTHESIS_DEPLOYMENT` | `gpt-5.6-sol` | chat deployment |
| `STORAGE_ACCOUNT_RESOURCE_ID` | — | storage account the indexer reads (setup only) |
| `SEARCH_USER_ASSIGNED_IDENTITY_RESOURCE_ID` | — | identity the indexer runs as (setup only) |

Authentication uses `DefaultAzureCredential`; `AZURE_OPENAI_API_KEY` is used for Azure OpenAI when set. The skillset always embeds with `text-embedding-ada-002` to match the vectors already in the shared index.

## Index fields

Generated pages use these additive fields in the shared index:

* `chunk_refs_str` — JSON array string of source document refs.
* `page_type` — `summary` | `entity` | `concept`.

A page's `header_1` is its title; `header_2` and `header_3` are null, since generated pages carry no header hierarchy. Consumers must accept null headers.

Blob metadata values must be ASCII.
