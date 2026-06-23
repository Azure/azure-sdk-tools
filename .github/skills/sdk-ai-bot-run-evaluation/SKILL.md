---
name: sdk-ai-bot-run-evaluation
license: MIT
metadata:
  author: Microsoft
  version: "2.0.0"
  distribution: shared
description: "Run the Azure SDK QA bot evaluation on the Azure AI Foundry evals framework (azure-ai-projects>=2.0), and prepare evaluation datasets as Foundry versioned assets. USE FOR: \"run bot evaluation\", \"foundry evaluation\", \"evals_run\", \"prepare eval dataset\", \"curate dataset\", \"upload dataset asset\", \"perf evaluation\". DO NOT USE FOR: starting the bot for chat, deploying the bot agent, knowledge-graph indexing, continuous/online production evaluation rules. INVOKES: dataset.curate, dataset.review, dataset.upload, dataset.validate, evals_run.py."
compatibility: "copilot-cli, Azure CLI (`az login` to azuresdkqabot subscription), Python 3.12+, local clone of tools/sdk-ai-bots"
---

# Run SDK QA Bot Evaluation (Azure AI Foundry)

Evaluation grades the bot answer with builtin Foundry LLM evaluators. We call the bot
`/completion` endpoint **concurrently** (`--max_concurrency`), collect answers +
retrieved context, then grade them inline (evaluators read `{{item.response}}` /
`{{item.context}}`). Parallelizing the slow answer-generation step is the main speed
lever.

There are two independent parts: **dataset preparation** (infrequent) and
**evaluation runs**.

## When to use

- Run the bot evaluation locally against a curated dataset.
- Reproduce an offline/online/perf evaluation pipeline locally.
- Prepare/refresh evaluation datasets (curate → review → upload).

## Prerequisites

1. `az login` against the `azuresdkqabot` subscription.
2. From `tools/sdk-ai-bots/azure-sdk-qa-bot-evaluation`: `pip install -r requirements.txt`.
3. A `.env` with the vars from `env-variables` — at minimum `AZURE_AI_PROJECT_ENDPOINT`,
   `AZURE_EVALUATION_MODEL_NAME`, `EVALUATE_THRESHOLD`, plus the bot `/completion`
   config: `BOT_SERVICE_ENDPOINT` (+ `BOT_AGENT_TOKEN_RESOURCE`/`BOT_AGENT_ACCESS_TOKEN`)
   or a local `server.py`, and `BOT_CONFIG_CONTAINER`/`BOT_CONFIG_CHANNEL_BLOB` for
   tenant routing. Dataset prep also needs `STORAGE_BLOB_ACCOUNT` and
   `AI_ONLINE_PERFORMANCE_EVALUATION_STORAGE_CONTAINER`.

## Canonical dataset schema

JSONL, one case per line, **inputs + expectations only**:
`testcase, query, ground_truth, expected_references[], expected_knowledges[],
scenario, tenant, source, reviewed`. Validate with
`python -m dataset.validate <path-or-folder> [--require-reviewed]`.

## Part 1 — Dataset preparation (infrequent)

1. **Curate** — scan ALL blob content, stage only new deduped candidates:
   ```
   python -m dataset.curate                      # downloads all blobs first
   python -m dataset.curate --source_md_path online-qa-tests   # or local md
   ```
   New candidates land in `datasets/_staging/<scenario>.jsonl` with `reviewed=false`.
2. **Review (human)** — edit staged files, fix ground_truth/links, set
   `"reviewed": true` on keepers.
3. **Promote** — append reviewed rows into the curated set:
   ```
   python -m dataset.review --target basic      # or --target perf
   ```
4. **Upload** — one versioned Foundry asset per scenario; writes `registry.json`:
   ```
   python -m dataset.upload --target basic
   ```

`datasets/_staging/` is git-ignored; `datasets/{basic,perf}/` + `registry.json`
are committed.

## Part 2 — Run an evaluation

```
# Concurrent /completion collection + inline grading
python evals_run.py \
  --dataset "qa-bot-basic-typespec:latest" \
  --evaluators "bot_evals,groundedness" \
  --max_concurrency 8 \
  --baseline_check False --is_ci False
```

Or point at a local curated file directly: `--dataset datasets/basic/typespec.jsonl`.

Set the bot `/completion` endpoint via `BOT_SERVICE_ENDPOINT`
(+ `BOT_AGENT_TOKEN_RESOURCE`/`BOT_AGENT_ACCESS_TOKEN`) for the deployed bot, or run
the agent `server.py` locally (`http://localhost:8089`). Tenant routing comes from
`BOT_CONFIG_CONTAINER`/`BOT_CONFIG_CHANNEL_BLOB`.

Each run prints a per-case table and its Foundry `report_url`. `--cache_result full`
writes per-case + failed-case JSON under `cache/`. The run exits non-zero on failure
(pass-with-warning for suppressed cases via `suppression.json`).

## Key parameters

| Flag | When to use |
|---|---|
| `--dataset` | `qa-bot-<target>-<scenario>[:version]` or a `<path>.jsonl`. |
| `--max_concurrency` | parallel `/completion` calls (default 8). |
| `--evaluators` | subset of `similarity,response_completeness,relevance,coherence,fluency,groundedness,bot_evals`. |
| `--baseline_check` | `True` compares to `results/<scenario>-test.json` baselines (CI gate). |
| `--cache_result` | `full` writes per-case + failed-case JSON; `score` writes a flat table. |
| `--is_ci` | `False` locally (uses `az login`); `True` in pipelines. |

## Evaluators

- builtin LLM (model + threshold): `similarity`, `response_completeness`.
- builtin LLM (model): `relevance`, `coherence`, `fluency`, `groundedness`.
- local composite: `bot_evals` (weighted similarity + response_completeness).

## Troubleshooting

- **`Dataset file not found`** — pass a valid `qa-bot-<target>-<scenario>` name (maps to
  `datasets/<target>/<scenario>.jsonl`) or a local `--dataset <path>.jsonl`.
- **`Missing required environment variable`** — ensure `.env` has
  `AZURE_AI_PROJECT_ENDPOINT`, `AZURE_EVALUATION_MODEL_NAME`, `EVALUATE_THRESHOLD`.
- **All cases counted as failures** — the bot `/completion` endpoint was unreachable;
  check `BOT_SERVICE_ENDPOINT`/local `server.py` and the bearer token.
- **Curate stages 0 new cases** — all cases already curated/staged (incremental dedup).
- **Blob download returns 0 files** — check `STORAGE_BLOB_ACCOUNT` /
  `AI_ONLINE_PERFORMANCE_EVALUATION_STORAGE_CONTAINER` and `az login` subscription.

## References

- Eval entry: `tools/sdk-ai-bots/azure-sdk-qa-bot-evaluation/evals_run.py`
- Dataset prep: `tools/sdk-ai-bots/azure-sdk-qa-bot-evaluation/dataset/`
- Pipelines: `tools/sdk-ai-bots/{offline,online,perf}-evaluation.yml` (dataset preparation is local-only)
