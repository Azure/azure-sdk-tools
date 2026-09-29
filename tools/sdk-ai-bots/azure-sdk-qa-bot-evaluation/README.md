# Azure SDK QA Bot Evaluations

Evaluation for the Azure SDK QA bot, built on the **Azure AI Foundry** evaluation
framework (`azure-ai-projects >= 2.1`, the OpenAI-evals surface).

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

We call the bot `/completion` endpoint **concurrently** (`--max_concurrency`, default 8), retrieve each stored Agent response from Foundry by its response ID, then grade the collected answers in one inline evaluation run. The bot's documentation-only `full_context` is sent to the evaluators unchanged; tool calls and their results remain local and are joined into cached results after grading. Reads cases from the local `evaluation_datasets/<target>/<scenario>.jsonl`.

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

Set the bot `/completion` endpoint via `BOT_SERVICE_ENDPOINT` (+ `BOT_AGENT_TOKEN_RESOURCE` or `BOT_AGENT_ACCESS_TOKEN`) for the deployed bot, or run the agent `server.py` locally (defaults to `http://localhost:8089`). `AI_FOUNDRY_AGENT_NAME` selects the Hosted Agent whose stored responses are retrieved and defaults to `azure-sdk-chat-agent`. Tenant routing is resolved from `BOT_CONFIG_CONTAINER` / `BOT_CONFIG_CHANNEL_BLOB`.

Results appear on the Evaluation tab of the Azure AI Foundry portal (each run prints its `report_url`). `--cache_result full` writes per-case JSON + failed-cases JSON under `cache/`.

Each cached case preserves the hosted-agent response ID and ordered tool calls under `execution`. Tool calls contain the tool name, original arguments, and complete output; JSON results from `search_knowledge_base` and `wiki_search` are stored as objects. Tool calls are not sent to Foundry evaluators. The raw `actual.context` used by the groundedness evaluator is retained separately. A normal response ID can retrieve the original stored response on demand; synthetic IDs such as `content-filter` have no stored response.

### Dev-only decision-guidance study

`decision_study.py` reuses this runner and result adapter to compare **baseline,
general, topic, and combined** guidance. It does not change the hosted agent,
tenant routing, production prompts, memory stores, or search indexes.

The first track is **oracle content replay**, not an end-to-end bot benchmark:
the selected model receives frozen baseline instructions, the applicable guide
deltas, and identical supplied evidence. No tools or memory are available.
A common replay instruction replaces the requirement to call tools; this
necessary deviation is saved in the bundle and applied equally to all arms.
Judge expectations and reference answers are never sent to the answering model.

The initial 12 cases in `evaluation_datasets/decision-study/apispec.jsonl` form
six counterfactual pairs (missing versus supplied context, conflicting versus
consistent evidence, and pending versus confirmed resolution). They are
**synthetic diagnostic development cases**, not unseen historical holdouts or
proof of production effectiveness. They remain `reviewed: todo` pending domain
review. Do not promote them through the canonical curator: study-only metadata
is intentionally read directly by this harness and is not preserved by
`CanonicalCase` round-tripping.

Prepare a local bundle (no Azure credentials or cloud calls):

```powershell
python decision_study.py prepare `
  --dataset .\evaluation_datasets\decision-study\apispec.jsonl `
  --baseline <frozen-global-instruction.md> <frozen-api-spec-instruction.md> `
  --baseline-revision <upstream-commit-sha> `
  --general <bot_instructions.txt> `
  --guides <guides.json> `
  --output <new-private-study-directory> `
  --repeats 2
```

`--guides` accepts the extracted decision-guide package (`guides` array with
IDs, title, lesson, inspect/avoid, context dependencies, conditional actions,
owner role and stop condition). Guide IDs are selected explicitly in each case,
so this track measures usefulness **when the right guide is available**, not
guide retrieval accuracy. Source conversations and guide provenance IDs are
not inserted into model prompts. Keep private guide packages and bundles
outside this public repository.

Use a current, reviewed baseline snapshot and retain its upstream commit.
Preparation hashes all input files, freezes prompts/cases/rubrics, and creates
a seeded block-randomized schedule with every arm for each case/repeat.
The evaluation implementation is hashed too; modifying it requires a fresh bundle.
Treatment instructions explicitly supersede conflicting diagnostic rules only,
not safety or current policy. The topic overlay specifically reconciles the
merge-summary rule when supplied evidence proves the summary stale or contradictory.
That precedence change is part of the treatment, not a silent baseline edit.
The default seed dataset produces 96 generation requests plus grading.
Output directories must be new: existing studies are never overwritten.

After selecting authorized dev model deployments, explicitly opt into the
paid generation and grading run:

```powershell
python decision_study.py run `
  --bundle <study-directory>\bundle.json `
  --output <new-private-run-directory> `
  --project-endpoint <authorized-Foundry-project-endpoint> `
  --model <generation-deployment> `
  --judge-model <grading-deployment> `
  --execute
```

The generation deployment must support chat completions and the project must
support OpenAI-evals `score_model` criteria. Pin deployment/model versions and
do not update them during the run. These APIs are covered by offline contract
tests here; service availability still requires a live smoke run in the chosen
project. No resources are provisioned by this command. It uses `az login`
through the existing credential helper and disables SDK request retries.
Unreviewed cases are rejected unless `--allow-unreviewed` explicitly permits an
exploratory development run. This is not permission to label them validated.

The four opt-in graders score 0–2: `next_action`, `context_discipline`,
`evidence_discipline`, and `authority_discipline`. The judge sees the answer,
expected acceptable behavior, frozen evidence, and actual trace (empty in
content replay), not the arm label or treatment instructions. Necessary
clarification can receive full credit; unnecessary questions lose credit.
An evidence/authority score of zero is a hard failure, forcing aggregate
utility to zero instead of being averaged away.

Artifacts include an integrity-checked bundle, randomized schedule and prompt
hashes, an append-only attempted/completed/failed generation journal, model IDs,
usage and latency, raw decision-grader results, and paired win/tie/loss summaries.
Generation failures and missing/invalid grades stay in the denominator with
zero utility and separate infrastructure-failure counts. Any such failures
make the command exit unsuccessfully. Never interpret them as model-quality
judgments. Group bootstrap intervals resample whole scenario groups, not
individual repeats; with six synthetic groups they are exploratory only.
Interrupted/failed runs are preserved and never automatically resumed or retried.

Before claiming practical improvement, add independently selected and reviewed
historical cutoff cases, exclude related incidents and all previously tuned
cases, and remove their answers from Q&A/wiki/episode retrieval. Then measure
the same deltas through isolated, version-pinned hosted agents with native
retrieval and scripted multi-turn clarification. Today's live PR state is not
historical evidence. The replay run does **not** claim those later tracks.

### Local dev-agent comparison (before deployment)

`run-local` calls the actual edited chat agent on `127.0.0.1:8088/responses`,
not the API server's `/completion` (which uses the deployed agent). It records
the inline local tool trace, then grades the collected answer with the same
Foundry decision rubrics. Azure model, tools, search and grading can still incur
cost. Run the agent from
`../azure-sdk-qa-bot-agent` using its README's `requirements-dev.txt`,
Azure login/App Configuration access and `agentdev`/F5 setup first.
Do not deploy or modify shared App Configuration for this study.

Prepare a **new** bundle against the *currently checked-out* chat-agent root
instruction and API Spec Review tenant prompt (not an older snapshot). Use the
`prepare` command above with those two files as `--baseline`. For each arm,
stop the previous dev agent, then in PowerShell:

```powershell
# From azure-sdk-qa-bot-evaluation; use a new file and receipt for every arm.
python decision_study.py variant `
  --bundle <private-study>\bundle.json --arm baseline `
  --root-instruction ..\azure-sdk-qa-bot-agent\agents\chat_agent\instruction.md `
  --tenant-guideline ..\azure-sdk-qa-bot-agent\prompts\tenants\api_spec_review.md `
  --output <private-study>\baseline-variant.json

# In another PowerShell window, from azure-sdk-qa-bot-agent, with its .venv active:
$env:SDK_QA_STUDY_VARIANT_FILE = '<private-study>\baseline-variant.json'
$env:SDK_QA_STUDY_RECEIPT_FILE = '<private-study>\baseline-receipt.json'
agentdev run agents/chat_agent/init.py --port 8088

# Back in the evaluation window after agent startup; explicit paid grading opt-in:
python decision_study.py run-local `
  --bundle <private-study>\bundle.json `
  --variant <private-study>\baseline-variant.json `
  --receipt <private-study>\baseline-receipt.json `
  --output <private-study>\baseline-run `
  --project-endpoint <authorized-Foundry-project-endpoint> `
  --judge-model <grading-deployment> --execute --allow-unreviewed
```

Repeat the same three commands with `general`, `topic`, and `combined`, each
with a fresh variant/receipt/run path and a **fresh agent process**. The agent
checks both frozen prompt hashes at startup and writes a receipt; `run-local`
rejects a different arm or bundle. A pre-existing server on port 8088 must be
stopped first. Do not change local code, model deployment, search indexes, or
tool permissions between arms. Remove the two environment variables when
finished. For reviewed cases omit `--allow-unreviewed`.

```powershell
python decision_study.py summarize-local `
  --bundle <private-study>\bundle.json `
  --runs <private-study>\baseline-run <private-study>\general-run `
         <private-study>\topic-run <private-study>\combined-run `
  --output <private-study>\local-summary.json
```

The variant file contains only general/topic treatment text, **never reference
answers or cases**. Each arm retains the bot's normal tools and tenant skill;
study mode omits both tenant episode and personal memory providers (and skips
memory-store initialization) consistently across arms. It does **not** freeze
live search, wiki, web or other tool results, nor guarantee that unrelated
historical Q&A is absent from their indexes. Case evidence is explicitly
supplied in the user message rather than injected as a tool result. Unlike
fixed-evidence content replay, all relevant topic guides are available to the
tenant skill; this track measures end-to-end guide use, not oracle selection.
Local failures are journaled and retained in paired denominators. The first
12 synthetic cases are exploratory and unreviewed; the mode does not establish
real-world superiority. No stored Foundry response retrieval is used for
local-agent traces. Never commit private variants, bundles, receipts or results.

### Evaluators

Default evaluators are builtin LLM evaluators that read the collected bot answer via
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
python -m pytest unit_tests -q          # includes decision-study isolation and mocked-run checks
python -m dataset.validate evaluation_datasets/decision-study/apispec.jsonl
```

## Pre-commit

```bash
pre-commit install
pre-commit run --all-files
```
