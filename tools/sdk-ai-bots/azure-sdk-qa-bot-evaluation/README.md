# Azure SDK QA Bot Evaluations

Evaluation for the Azure SDK QA bot, built on the **Azure AI Foundry** evaluation
framework (`azure-ai-projects >= 2.0`, the OpenAI-evals surface).

We call the bot `/completion` endpoint **concurrently**, collect each answer +
retrieved context + references, then grade them inline with the Foundry builtin LLM
evaluators. Parallelizing the slow answer-generation step is the main speed lever.

There are **two independent parts**:

1. **Dataset preparation** (`dataset/`) — infrequent. Scans storage, screens cases
   into curated per-scenario JSONL, and publishes them as Foundry versioned
   Dataset assets.
2. **Evaluation runs** (`evals_run.py`) — consume the published assets locally or in
   CI/CD.

## Prerequisites

- Python 3.12+
- `az login` (local) against the `azuresdkqabot` subscription
- `pip install -r requirements.txt`
- Environment (or `.env`) — see [`env-variables`](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/azure-sdk-qa-bot-evaluation/env-variables):
  - `AZURE_AI_PROJECT_ENDPOINT`, `AZURE_EVALUATION_MODEL_NAME`, `EVALUATE_THRESHOLD`
  - bot `/completion`: `BOT_SERVICE_ENDPOINT` (+ `BOT_AGENT_TOKEN_RESOURCE` /
    `BOT_AGENT_ACCESS_TOKEN`) for the deployed bot, or run the agent `server.py`
    locally (`http://localhost:8089`); tenant routing from `BOT_CONFIG_CONTAINER` /
    `BOT_CONFIG_CHANNEL_BLOB`
  - dataset prep only: `STORAGE_BLOB_ACCOUNT`,
    `AI_ONLINE_PERFORMANCE_EVALUATION_STORAGE_CONTAINER`

## Canonical dataset schema

One JSON object per line. Curated datasets hold **inputs + expectations only** —
the answer, context and references are produced at run time by the agent and read
by the evaluators.

```json
{
  "testcase": "unique name",
  "query": "question to ask the bot",
  "ground_truth": "expected answer",
  "expected_references": [{"title": "t", "link": "https://..."}],
  "expected_knowledges": [{"title": "t", "link": "https://..."}],
  "scenario": "typespec",
  "tenant": null,
  "source": "blob path / manual",
  "reviewed": "pass"
}
```

The `reviewed` field is one of `"todo"` (newly curated, awaiting review),
`"pass"` (accepted; promoted into official datasets) or `"abandoned"` (reviewed
and dropped, or a leftover `todo` finalized at promote time).

Validate any file/folder:

```bash
python -m dataset.validate evaluation_datasets/basic
python -m dataset.validate evaluation_datasets/basic --require-reviewed
```

## Part 1 — Dataset preparation

```bash
# 1) Scan ALL blob content; stage only NEW, deduped candidates (reviewed="todo").
python -m dataset.curate                       # downloads all blobs first
python -m dataset.curate --source_md_path online-qa-tests   # or use local md

# 2) Human review: edit evaluation_datasets/_staging/<scenario>.jsonl, fix links/answers,
#    set "reviewed": "pass" on keepers.

# 3) Promote "pass" rows into the curated set (append, incremental). Any rows
#    still "todo" are finalized to "abandoned" and kept in staging (deduped).
python -m dataset.review --target basic      # or --target perf

# 4) Upload one versioned Foundry Dataset asset per scenario; writes registry.json.
python -m dataset.upload --target basic      # qa-bot-basic-<scenario>:<scenario>-YYYY-MM-DD
```

`evaluation_datasets/_staging/` is committed (shared review state, so concurrent
contributors don't re-curate the same cases); `evaluation_datasets/basic/`, `evaluation_datasets/perf/` and
`evaluation_datasets/registry.json` are committed.

## Part 2 — Running evaluations

We call the bot `/completion` endpoint **concurrently** (`--max_concurrency`, default
8), collect each answer + context, then grade them inline. Reads cases from the local
`evaluation_datasets/<target>/<scenario>.jsonl`.

```bash
# Concurrent /completion collection + inline grading:
python evals_run.py \
  --dataset "qa-bot-basic-typespec:latest" \
  --evaluators "bot_evals,groundedness" \
  --max_concurrency 8 \
  --baseline_check False --is_ci False

# Or point at a local curated file directly:
python evals_run.py --dataset evaluation_datasets/basic/typespec.jsonl --is_ci False
```

Set the bot `/completion` endpoint via `BOT_SERVICE_ENDPOINT` (+ `BOT_AGENT_TOKEN_RESOURCE`
or `BOT_AGENT_ACCESS_TOKEN`) for the deployed bot, or run the agent `server.py` locally
(defaults to `http://localhost:8089`). Tenant routing is resolved from
`BOT_CONFIG_CONTAINER` / `BOT_CONFIG_CHANNEL_BLOB`.

Results appear on the Evaluation tab of the Azure AI Foundry portal (each run prints
its `report_url`). `--cache_result full` writes per-case JSON + failed-cases JSON
under `cache/`.

### Evaluators

All evaluators are builtin LLM evaluators that read the collected bot answer via
`{{item.response}}`:

| Name | Kind | Reads |
|---|---|---|
| `similarity` | builtin LLM (model + threshold) | answer vs `query` + `ground_truth` |
| `response_completeness` | builtin LLM (model + threshold) | answer vs `ground_truth` |
| `relevance` | builtin LLM (model) | answer vs `query` |
| `coherence` | builtin LLM (model) | answer vs `query` |
| `fluency` | builtin LLM (model) | answer vs `query` |
| `groundedness` | builtin LLM (model) | answer vs retrieved context (`{{item.context}}`) |
| `bot_evals` | local composite | weighted `similarity` + `response_completeness` |

LLM-graded evaluators use the 1-5 `EVALUATE_THRESHOLD`.

`expected_references` / `expected_knowledges` are kept in the schema for future use
but are not scored by an evaluator.

## Pipelines

| Pipeline | Purpose |
|---|---|
| `offline-evaluation.yml` | PR gate; starts agent server.py, evaluates the curated `basic` sets via local /completion. |
| `online-evaluation.yml` | weekly production check; snapshots the last 21 days of storage md (`dataset.online_snapshot`) and evaluates that fresh data against the deployed bot. |
| `perf-evaluation.yml` | weekly/manual, large per-scenario perf datasets. |

The online pipeline evaluates the **freshly collected weekly md** (not the static
`basic` set): `dataset.online_snapshot` downloads recent md and converts it to
per-scenario `online-tests/<scenario>.jsonl`, then `evals_run.py` grades each file:

```bash
python -m dataset.online_snapshot --is_ci False --days_before 21 --dest online-tests
for f in online-tests/*.jsonl; do
  python evals_run.py --dataset "$f" --evaluators bot_evals --baseline_check False --is_ci False
done
```


## Tests

```bash
python unit_tests/test_pure_logic.py     # offline: schema validation + output-items adapter
```

## Pre-commit

```bash
pre-commit install
pre-commit run --all-files
```
