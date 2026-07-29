# Running Evaluations

Flags, recipes, and result handling for `evals_run.py`. Run from
`tools/sdk-ai-bots/azure-sdk-qa-bot-evaluation` with the `.venv` active and `az login` done.

## Flags

| Flag                | Default  | Notes                                                                                                                                                                     |
| ------------------- | -------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `--dataset`         | required | Local `<...>.jsonl` path or `qa-bot-<target>-<scenario>[:version]`.                                                                                                       |
| `--evaluators`      | all      | Comma-separated subset of the evaluators below.                                                                                                                           |
| `--max_concurrency` | 8        | Parallel `/completion` calls.                                                                                                                                             |
| `--run_context`     | `local`  | Context tag appended to the dataset name (e.g. `local` or the pipeline name). The eval name is `<dataset>-<run_context>`, kept stable to avoid flooding the Foundry list. |
| `--baseline_check`  | `True`   | `True` gates against the stored baseline; `False` just reports.                                                                                                           |
| `--is_ci`           | `True`   | Use `False` locally (uses `az login` credential).                                                                                                                         |
| `--cache_result`    | `none`   | `none` \| `score` \| `full` (full writes per-case + failed-case JSON to `cache/`).                                                                                        |

## Evaluators

`similarity`, `response_completeness`, `groundedness`, `relevance`, `coherence`,
`fluency` (builtin LLM graders, 1-5 vs `EVALUATE_THRESHOLD`), and `bot_evals` (local
composite = `similarity * 0.6 + response_completeness * 0.4`). Omitting `--evaluators`
runs all seven.

## Bot endpoint

The collector posts each question to the bot `/completion` endpoint.

- **Local bot:** leave `BOT_SERVICE_ENDPOINT` unset → defaults to
  `http://localhost:8089/completion`. Start the agent first (separate shell, ~60-90s warmup):
  ```bash
  cd ../azure-sdk-qa-bot-agent && python server.py
  ```
- **Deployed bot:** set `BOT_SERVICE_ENDPOINT` and a token via `BOT_AGENT_TOKEN_RESOURCE`
  (fetched with `az account get-access-token`) or `BOT_AGENT_ACCESS_TOKEN`.

## Single test case

There is no `--testcase` flag — build a one-row JSONL file and pass it as `--dataset`.

```bash
# Extract one case by its testcase title from a scenario file:
grep -F '"testcase": "Async headers"' evaluation_datasets/perf/typespec.jsonl > one.jsonl
python evals_run.py --dataset one.jsonl --is_ci False --baseline_check False --cache_result full

# Or hand-write a single canonical row (testcase/query/ground_truth/scenario/reviewed) into one.jsonl.
```

The `scenario` is taken from the dataset file stem (`one` here) unless an asset name is
used; tenant routing falls back to default, which is fine for a quick local check.

## All scenarios

`--dataset` takes one file, so loop over scenarios:

```bash
for f in evaluation_datasets/perf/*.jsonl; do
  python evals_run.py --dataset "$f" \
    --is_ci False --baseline_check False --cache_result full
done
```

Swap `perf` for `basic` to run the PR-gate datasets.

## Results and the gate

- Each run logs a Foundry **`Report URL`** and prints a per-case score table.
- `--cache_result full` writes `cache/<scenario>-result-*.json` (every case) and
  `cache/<scenario>-failed-cases-*.json` (failures only) for analysis. `cache/` is git-ignored.
- **Gate** (`verify_results`): `groundedness` fails if any case fails; other metrics fail
  if the pass rate is below the case count. `suppression.json` can downgrade a FAIL to a
  warning. With `--baseline_check False` the run reports without gating against a baseline.
- A failed `/completion` collection is counted as a synthetic failing row so the gate is
  not blind to transport errors.
