---
name: sdk-ai-bot-run-evaluation
license: MIT
metadata:
  version: "1.0.0"
  distribution: local
description: 'Run Azure SDK QA bot evaluations on curated datasets locally, including a single test case. WHEN: "run evaluation", "run eval", "evaluate the bot", "run perf evaluation", "run basic evaluation", "run a single test case", "evaluate one question", "run all scenarios", "score the bot", "run evals locally". DO NOT USE FOR: preparing or uploading datasets, pipeline troubleshooting, knowledge-graph indexing.'
compatibility: "local azure-sdk-tools clone, python 3.12 venv, az login, bot /completion endpoint"
---

# Run QA Bot Evaluation

Run the Azure SDK QA bot evaluation locally with `evals_run.py` in the package
`tools/sdk-ai-bots/azure-sdk-qa-bot-evaluation`. It calls the bot `/completion` endpoint
concurrently to collect answers, then grades them with the Foundry builtin LLM evaluators.

Run all commands from `tools/sdk-ai-bots/azure-sdk-qa-bot-evaluation` with the package
`.venv` active and `az login` done. See [running evaluations](references/running-evaluations.md) for flags, the bot endpoint options, single-case and all-scenario recipes, and how to read results.

## Triggers

USE FOR: run an evaluation on a curated dataset (basic or perf); run a single scenario; run all scenarios; run a single test case; grade against the local or deployed bot; inspect results and the pass/fail gate
WHEN: "run evaluation", "run eval", "evaluate the bot", "run perf evaluation", "run basic evaluation", "run a single test case", "evaluate one question", "run all scenarios", "score the bot", "run evals locally"
DO NOT USE FOR: preparing or uploading datasets, pipeline troubleshooting, knowledge-graph indexing

## Rules

- `--dataset` accepts a local `evaluation_datasets/<target>/<scenario>.jsonl` path **or** a Foundry asset name `qa-bot-<target>-<scenario>[:version]` (the asset name resolves to the local file; the version is informational).
- There is **no single-testcase flag**. To run **one case**, make a one-row JSONL file and pass it as `--dataset` (see the reference).
- Use `--is_ci False` locally (uses `az login`); CI uses pipeline identity.
- The bot endpoint comes from `BOT_SERVICE_ENDPOINT`; if unset it defaults to the **local** `http://localhost:8089` — start the agent `server.py` first for local runs.
- Default evaluators are all seven; pass `--evaluators` to subset.

## Environment

Confirm and remind the user to set these before running (loads `.env`; copy and fill
`tools/sdk-ai-bots/azure-sdk-qa-bot-evaluation/env-variables`):

| Purpose         | Variables                                                                                    |
| --------------- | -------------------------------------------------------------------------------------------- |
| Foundry grading | `AZURE_AI_PROJECT_ENDPOINT`, `AZURE_EVALUATION_MODEL_NAME`, `EVALUATE_THRESHOLD` (default 3) |
| Deployed bot    | `BOT_SERVICE_ENDPOINT` + (`BOT_AGENT_TOKEN_RESOURCE` or `BOT_AGENT_ACCESS_TOKEN`)            |
| Local bot       | none — run agent `server.py` (defaults to `http://localhost:8089`)                           |
| Tenant routing  | `STORAGE_BLOB_ACCOUNT`, `BOT_CONFIG_CONTAINER`, `BOT_CONFIG_CHANNEL_BLOB`                    |
| Auth            | `az login` (with `--is_ci False`)                                                            |

## Common commands

```bash
# One scenario (all evaluators), against the local server, no baseline gate:
python evals_run.py --dataset evaluation_datasets/perf/typespec.jsonl \
  --is_ci False --baseline_check False --cache_result full

# Same via the asset name:
python evals_run.py --dataset "qa-bot-perf-typespec:latest" --is_ci False --baseline_check False

# Subset of evaluators:
python evals_run.py --dataset "qa-bot-basic-python:latest" \
  --evaluators "bot_evals,groundedness" --is_ci False --baseline_check False
```

For a **single test case**, **all scenarios**, the **deployed bot**, and reading
results / the gate, see [running evaluations](references/running-evaluations.md).

## Steps

1. Ensure the required env vars are set; for a local run, start the agent `server.py`.
2. Choose the dataset: a scenario file, an asset name, or a one-row file for a single case.
3. Run `python evals_run.py --dataset <...> --is_ci False` (add `--baseline_check False`
   for ad-hoc runs, `--evaluators` to subset).
4. Open the printed Foundry **Report URL** and review the per-case score table.
5. With `--cache_result full`, inspect `cache/<scenario>-*.json` for per-case and
   failed-case detail.
