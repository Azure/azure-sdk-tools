# Azure SDK QA Bot Code Search Builder

Builds the QA bot's offline semantic code index from repositories declared by the agent's `TenantConfig`. The builder resolves every configured full Git ref to a commit, maintains a deterministic depth-one checkout and CocoIndex LMDB state per Git URL/ref, chunks and embeds changed files, reconciles generation-visible documents into Azure AI Search, and publishes the tenant catalog last.

## Requirements

- Python 3.12
- Node.js 22 or newer
- Git
- Azure permissions for App Configuration, Blob Storage, Azure AI Search, and the configured Azure OpenAI embedding deployment

Install pinned dependencies:

```bash
pip install -r requirements.txt
npm ci
```

`cocoindex==1.0.20` and `@typespec/compiler==1.13.0` are pinned. Authentication uses `DefaultAzureCredential`; no credentials are stored in this package.

## Commands

```bash
# Create/update azure-sdk-code-v1 and point the azure-sdk-code alias at it.
python -m code_search ensure-index

# Build every repository demanded by TenantConfig and publish one catalog.
python -m code_search build-all

# Remove generations outside the rollback and retry windows.
python -m code_search collect-garbage
```

`build-all` imports `config.tenant_config.get_all_code_repository_configs` from the sibling `azure-sdk-qa-bot-agent` directory. That helper returns repository configuration keyed by tenant ID. The import is isolated in `code_search/tenant_source.py` and fails with an actionable message when the agent helper is unavailable.

## Configuration

Environment variables override Azure App Configuration values loaded from
`AZURE_APPCONFIG_ENDPOINT`.

| Setting | Default | Purpose |
| --- | --- | --- |
| `AI_SEARCH_BASE_URL` | required | Azure AI Search endpoint |
| `CODE_INDEX_SEARCH_ALIAS` | `azure-sdk-code` | Stable query alias |
| `CODE_INDEX_SEARCH_INDEX` | `<alias>-v1` | Versioned physical index |
| `AZURE_OPENAI_ENDPOINT` | required | Azure OpenAI endpoint |
| `CODE_INDEX_EMBEDDING_DEPLOYMENT` | required | Pinned embedding deployment |
| `CODE_INDEX_EMBEDDING_MODEL` | required | Model name used by the Search vectorizer |
| `CODE_INDEX_EMBEDDING_DIMENSIONS` | required | Exact vector dimensions |
| `AZURE_OPENAI_API_VERSION` | `2024-02-01` | Embedding API version |
| `STORAGE_BLOB_ENDPOINT` | `STORAGE_BASE_URL` | Blob account endpoint |
| `STORAGE_CODE_INDEX_CONTAINER` | `code-index` | Catalog, manifest, and state container |
| `CODE_INDEX_WORK_ROOT` | `.code-index` | Local Git/state cache |
| `CODE_INDEX_MAX_FILE_BYTES` | `2097152` | Maximum indexed file size |
| `CODE_INDEX_EMBEDDING_CONCURRENCY` | `2` | Concurrent embedding requests |
| `CODE_INDEX_VALIDATION_TIMEOUT_SECONDS` | `180` | Search publication wait |
| `SEARCH_USER_ASSIGNED_IDENTITY_RESOURCE_ID` | empty | Optional query vectorizer identity |

The Chat Agent also reads `CODE_INDEX_SEARCH_TOPK`, `CODE_INDEX_SEARCH_CANDIDATE_TOPK`, `CODE_INDEX_SEARCH_CONTENT_CHARS`, `CODE_INDEX_SEARCH_TIMEOUT_SECONDS`, and `CODE_INDEX_CATALOG_CACHE_SECONDS` to bound online retrieval.

## Incremental and generation behavior

- `git ls-remote` resolves the exact ref. Workspaces use a deterministic URL/ref hash and only run `git fetch --no-tags --depth=1 origin <ref>` before a detached checkout. Missing caches and force-pushes use the same path; full history, unrelated refs, tags, and submodules are never fetched.
- File discovery uses CocoIndex `localfs.walk_dir(..., recursive=True)` with a `PatternFilePathMatcher` wrapper that also applies `.gitignore`, size, and binary checks.
- TypeSpec files are parsed by a small persistent Node worker pool. Declaration spans carry symbol metadata and exact source lines. A parser diagnostic is logged and recorded in the manifest before explicit `RecursiveSplitter` fallback; worker failures stop the build.
- Index-time embeddings use a memoized CocoIndex function and `AsyncAzureOpenAI` with managed identity. The Search index uses the same deployment and dimensions for query-time vectorization.
- Target tracking stores the content fingerprint, current document ID, and valid-from generation. Unchanged chunks keep their document and generation. Changed or deleted chunks close prior IDs at the prospective generation.
- Every `IndexingResult` is checked. Only per-document transient failures are retried; permanent failures fail the CocoIndex update.
- The LMDB archive and immutable generation manifest are uploaded before an ETag-conditional catalog update. If publication fails, active queries remain pinned to the prior generation. Retrying active generation plus one reuses deterministic document IDs and close operations.
- `collect-garbage` retains the active generation, two previous published generations, and generations younger than seven days. It also reverts unpublished generations older than 24 hours and retains old manifests for 90 days.

The local `.code-index` directory is only a performance cache. The Blob state archive is the durable source used when a local CocoIndex cache is unavailable.

## Tests

```bash
python -m pytest tests -q
python -m pyright --pythonversion 3.12 code_search
node --check code_search/typespec_worker.mjs
```
