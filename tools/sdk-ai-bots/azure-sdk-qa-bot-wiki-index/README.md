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
| `index` | navigation page for generated entity and concept pages | `wiki_index` |

The full build extracts entities and concepts per document, aggregates recurring
items, synthesizes generated pages, adds cross-links between pages with shared
source documents, and writes the manifest.

## Usage

```bash
pip install -r requirements.txt

# generate and persist wiki pages for one folder
python -m azure_sdk_qa_bot_wiki_index.main --prefix typespec_docs/

# inspect generated pages without persisting
python -m azure_sdk_qa_bot_wiki_index.main --prefix typespec_docs/ --dry-run

# purge generated docs from the index
python -m azure_sdk_qa_bot_wiki_index.main --purge --no-generate

# backfill chunk_refs metadata on existing page blobs
python -m azure_sdk_qa_bot_wiki_index.main --backfill-metadata
```

## Configuration

| Variable | Default | Purpose |
| -------- | ------- | ------- |
| `AI_SEARCH_BASE_URL` | — | Azure AI Search endpoint |
| `AI_SEARCH_INDEX` | — | target index shared with the KB |
| `STORAGE_BLOB_ENDPOINT` | — | blob account endpoint |
| `STORAGE_KNOWLEDGE_CONTAINER` | `knowledge` | source knowledge container |
| `STORAGE_WIKI_OUTPUT_CONTAINER` | `wiki` | generated wiki container |
| `AZURE_OPENAI_ENDPOINT` | — | Azure OpenAI endpoint |
| `WIKI_SYNTHESIS_DEPLOYMENT` | `gpt-5.4` | chat deployment |
| `WIKI_EMBEDDING_DEPLOYMENT` | `text-embedding-ada-002` | embedding deployment |
| `WIKI_EXTRACTION_GRANULARITY` | `standard` | extraction granularity |

Authentication uses `DefaultAzureCredential`; `AZURE_OPENAI_API_KEY` is used for
Azure OpenAI when set.

## Index fields

Generated pages use these additive fields in the shared index:

* `chunk_refs_str` — JSON array string of source document refs.
* `page_type` — `summary` | `entity` | `concept` | `synthesis`.

Blob metadata values must be ASCII.
