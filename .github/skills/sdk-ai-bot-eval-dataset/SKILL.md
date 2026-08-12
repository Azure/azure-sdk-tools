---
name: sdk-ai-bot-eval-dataset
license: MIT
metadata:
  version: "1.0.0"
  distribution: local
description: 'Create a new evaluation dataset or add cases to an existing one for the Azure SDK QA bot evaluation. WHEN: "add eval dataset item", "add a test case", "new evaluation dataset", "create dataset", "add question to dataset", "curate eval data", "promote staging cases", "upload dataset asset", "new scenario dataset". DO NOT USE FOR: running evaluations, pipeline troubleshooting, knowledge-graph indexing.'
compatibility: "local azure-sdk-tools clone, python 3.12 venv, az login"
---

# QA Bot Evaluation Dataset

Create a new per-scenario evaluation dataset or add cases to an existing one for the
QA bot evaluation package at `tools/sdk-ai-bots/azure-sdk-qa-bot-evaluation`. Datasets
are per-scenario JSONL files under `evaluation_datasets/<target>/<scenario>.jsonl`
(`target` = `basic` or `perf`) holding **inputs + expectations only**.

Run all commands from `tools/sdk-ai-bots/azure-sdk-qa-bot-evaluation` with the package
`.venv` active and `az login` done. See [schema and workflows](references/dataset-schema-and-workflows.md) for the canonical row format and step-by-step recipes.

## Triggers

USE FOR: create a new evaluation dataset (new scenario file); add cases to an existing per-scenario dataset; curate cases from storage markdown; promote reviewed staging cases; upload a dataset as a Foundry asset
WHEN: "add eval dataset item", "add a test case", "new evaluation dataset", "create dataset", "add question to dataset", "curate eval data", "promote staging cases", "upload dataset asset", "new scenario dataset"
DO NOT USE FOR: running evaluations, pipeline troubleshooting, knowledge-graph indexing

## Rules

- A **dataset** is one file: `evaluation_datasets/<target>/<scenario>.jsonl`. **Creating a new dataset = creating a new `<scenario>.jsonl`** in `basic/` or `perf/`.
- The canonical **dedup key is the normalized `query`** (applied at curation). `testcase` titles may legitimately repeat (e.g. `Untitled`) — never dedup or fail on `testcase`.
- Only `reviewed: "pass"` rows are curated/committed; see the [review status lifecycle](references/dataset-schema-and-workflows.md) for the three states and how leftovers are finalized.
- `evaluation_datasets/_staging/` **is committed** (shared review state) so concurrent contributors don't re-curate the same cases; `basic/`, `perf/` and `registry.json` are committed too.
- Always validate before upload, and after editing any curated file.

## Environment

Before running any command that touches Azure, **ensure the required variables are set**
and remind the user to configure them. Dataset prep loads a local `.env` (copy and fill
in `tools/sdk-ai-bots/azure-sdk-qa-bot-evaluation/env-variables`) and authenticates with
`az login`.

| Command                              | Requires                                                                                 |
| ------------------------------------ | ---------------------------------------------------------------------------------------- |
| `dataset.curate`                     | `az login`, `STORAGE_BLOB_ACCOUNT`, `AI_ONLINE_PERFORMANCE_EVALUATION_STORAGE_CONTAINER` |
| `dataset.upload`                     | `az login`, `AZURE_AI_PROJECT_ENDPOINT`                                                  |
| `dataset.validate`, `dataset.review` | none (local file operations)                                                             |

If a required variable is missing the command fails (`KeyError` / auth error) — set it in
`.env` or the shell and re-run. A purely **manual add** (edit JSONL + validate) needs no
env vars; only `dataset.upload` then requires `AZURE_AI_PROJECT_ENDPOINT` + `az login`.

## Choose a workflow

| Goal                                              | Workflow                                                                        |
| ------------------------------------------------- | ------------------------------------------------------------------------------- |
| Add a few specific cases you already have         | [Manual add](references/dataset-schema-and-workflows.md#manual-add)             |
| Harvest new cases from collected storage markdown | [Curate from blob](references/dataset-schema-and-workflows.md#curate-from-blob) |
| Create a brand-new scenario dataset               | [New dataset](references/dataset-schema-and-workflows.md#new-dataset)           |

## Core commands

```bash
# Validate a file or folder (--require-reviewed gates official datasets on reviewed=="pass")
python -m dataset.validate evaluation_datasets/<target>/<scenario>.jsonl --require-reviewed

# Promote reviewed (pass) staging rows; leftover items are finalized to abandoned
python -m dataset.review --target <basic|perf> [--scenario <scenario>]

# Upload one versioned Foundry asset per scenario; writes registry.json
python -m dataset.upload --target <basic|perf> [--scenario <scenario>]
```

After adding or creating a dataset: **validate → upload → commit** the per-scenario
file, `registry.json`, and updated `_staging/` files.

## Steps

1. Pick a workflow from the table above (manual add, curate from blob, or new dataset).
2. Add or stage canonical rows in `evaluation_datasets/<target>/<scenario>.jsonl`.
3. For staged cases, promote the reviewed ones with `python -m dataset.review`.
4. Validate the file with `python -m dataset.validate ... --require-reviewed`.
5. Publish with `python -m dataset.upload`, then commit the per-scenario file,
   `registry.json`, and updated `_staging/`.
