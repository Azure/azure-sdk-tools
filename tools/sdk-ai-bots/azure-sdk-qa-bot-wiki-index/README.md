# azure-sdk-qa-bot-wiki-index

Builds LLM-derived wiki pages and pushes them into the Azure SDK QA bot's
knowledge base.

Generated pages are written as markdown blobs and projected into the shared
Azure AI Search index with the same vector and keyword fields as raw knowledge
chunks.

## Wiki pipeline

The pipeline creates these generated page types:

| Page type | What it contains | `context_id` |
| --------- | ---------------- | ------------ |
| `summary` | one synthesized knowledge page per source document | inherited source folder |
| `entity` | cross-document page for a recurring symbol | `wiki_entity` |
| `concept` | cross-document page for a recurring topic | `wiki_concept` |

The full build extracts entities and concepts per document, aggregates recurring
items, synthesizes generated pages, adds cross-links between pages with shared
source documents, and writes the manifest.

Every LLM system prompt lives in `azure_sdk_qa_bot_wiki_index/prompts/` as
markdown (`extract.md` for the map phase, `summary.md` for per-document summary
pages, `compile.md` for entity/concept pages) and is loaded by
`llm.load_prompt`, so prompts can be tuned without touching code.

## Usage

```bash
pip install -r requirements.txt

# generate and persist wiki pages for the whole knowledge corpus
python -m azure_sdk_qa_bot_wiki_index.main
```

`build_wiki.yml` runs this daily at 04:00 UTC, two hours after the knowledge
sync that produces its input. Runs are incremental: only documents whose content
hash changed are re-summarised, and only entity/concept groups whose membership
changed are recompiled. Pages whose sources disappear are soft-deleted, and a
run that would remove more than half of the known sources aborts instead, on the
assumption that the upstream corpus is incomplete rather than genuinely emptied.

The build only writes blobs; `azure-sdk-knowledge-wiki-indexer` projects them
into the search index on its own daily schedule.

### Full rebuild

Reconcile diffs by **source** content hash, so editing a prompt in `prompts/`
does not invalidate any existing page. To make prompt changes take effect,
force a full rebuild:

1. Delete every blob in the wiki container (this removes the manifest, so the
   next run treats the corpus as new).
2. Delete the wiki documents from the shared index — everything matching
   `page_type ne null`; raw knowledge chunks have a null `page_type` and must be
   left alone.
3. Run the pipeline, then run the indexer manually rather than waiting for its
   daily schedule.

## Configuration

Settings are read from the environment first and then from Azure App
Configuration (`AZURE_APPCONFIG_ENDPOINT`), which is how the pipeline supplies
them.

| Variable | Default | Purpose |
| -------- | ------- | ------- |
| `AI_SEARCH_BASE_URL` | — | Azure AI Search endpoint |
| `AI_SEARCH_INDEX` | `azure-sdk-knowledge` | target index shared with the KB |
| `STORAGE_BLOB_ENDPOINT` | `STORAGE_BASE_URL` | blob account endpoint |
| `STORAGE_KNOWLEDGE_CONTAINER` | `knowledge` | source knowledge container |
| `STORAGE_WIKI_OUTPUT_CONTAINER` | `wiki` | generated wiki container |
| `AZURE_OPENAI_ENDPOINT` | — | Azure OpenAI endpoint |
| `WIKI_SYNTHESIS_DEPLOYMENT` | `gpt-5.4` | chat deployment |
| `STORAGE_ACCOUNT_RESOURCE_ID` | — | storage account the indexer reads (setup only) |
| `SEARCH_USER_ASSIGNED_IDENTITY_RESOURCE_ID` | — | identity the indexer runs as (setup only) |

Authentication uses `DefaultAzureCredential`; `AZURE_OPENAI_API_KEY` is used for
Azure OpenAI when set. The skillset always embeds with `text-embedding-ada-002`
to match the vectors already in the shared index.

## Index fields

Generated pages use these additive fields in the shared index:

* `chunk_refs_str` — JSON array string of source document refs.
* `page_type` — `summary` | `entity` | `concept`.

A page's `header_1` is its title; `header_2` and `header_3` are null, since
generated pages carry no header hierarchy. Consumers must accept null headers.

Blob metadata values must be ASCII.
