# Azure SDK QA Bot Code Repository Sync

Copies the current source files required by the QA bot from Git repositories declared in the sibling agent's `TenantConfig` into Azure Blob Storage. The daily job keeps only the latest checkout and the current blob contents; it does not preserve Git history, local workspaces, or historical blob generations.

## Behavior

- Deduplicates identical repository URL/ref declarations and rejects conflicting refs or file-selection settings.
- Creates a fresh checkout with `git init`, a forced no-tags depth-one fetch, and a detached checkout of the fetched commit.
- Initializes recursive submodules with shallow recommendations and verifies each checkout against its parent gitlink.
- Applies the configured package prefixes, include patterns, excludes, and file size limit to the top-level repository and independently to every recursive submodule.
- Uploads only regular, non-symlink UTF-8 source files. Submodule file paths are prefixed by the submodule path.
- Overwrites stable blobs named `<owner>/<repo>/<repository-relative-path>`, removes prior manifest-listed files that are no longer selected, and writes `manifest.json` last.
- Removes every checkout workspace after success or failure.

## Run

Requirements are Python 3.12 and Git:

```bash
pip install -r requirements.txt
python -m code_repository_sync
```

Authentication uses `DefaultAzureCredential`. Environment variables override values loaded from the App Configuration endpoint in `AZURE_APPCONFIG_ENDPOINT`.

The scheduled pipeline identity needs `Storage Blob Data Contributor` on the target account. The hosted Agent identity only needs `Storage Blob Data Reader`.

| Setting | Default | Purpose |
| --- | --- | --- |
| `STORAGE_BLOB_ENDPOINT` | `STORAGE_BASE_URL` | Blob account endpoint |
| `STORAGE_CODE_REPOSITORY_CONTAINER` | `code-repositories` | Destination container |
| `CODE_REPOSITORY_PREFIX` | empty | Optional prefix for all blobs |
| `CODE_REPOSITORY_MAX_FILE_BYTES` | `2097152` | Maximum selected file size |
| `CODE_REPOSITORY_SYNC_CONCURRENCY` | `32` | Concurrent blob uploads/deletes |
| `CODE_REPOSITORY_WORK_ROOT` | `.repository-sync-work` | Ephemeral checkout parent |

The scheduled pipeline is `sync_code_repositories.yml`. See the [direct repository file search design](../docs/code_repository_file_search_design.md) for the storage contract and Agent integration.

## Validate

```bash
python -m pyright --pythonversion 3.12 code_repository_sync
python -m compileall -q code_repository_sync
python -m pytest tests -q
```
