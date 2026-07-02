# Dataset Schema and Workflows

Canonical schema, field rules, and step-by-step recipes for the QA bot evaluation
datasets. All commands run from `tools/sdk-ai-bots/azure-sdk-qa-bot-evaluation`.

## Canonical row

One JSON object per line (JSONL). Inputs + expectations only — the answer, context,
and references are produced at run time by the bot and read by the evaluators.

```json
{
  "testcase": "short title",
  "query": "title: ...\n\nquestion: the full question to ask the bot",
  "ground_truth": "expected answer",
  "expected_references": [{ "title": "t", "link": "https://..." }],
  "expected_knowledges": [{ "title": "t", "link": "https://..." }],
  "scenario": "typespec",
  "tenant": null,
  "source": "blob path / manual",
  "reviewed": "pass"
}
```

## Field rules

| Field                 | Required | Notes                                                             |
| --------------------- | -------- | ----------------------------------------------------------------- |
| `testcase`            | yes      | Non-empty string. May repeat across rows; **not** a dedup key.    |
| `query`               | yes      | The question; the **normalized query is the dedup key**.          |
| `ground_truth`        | yes      | Expected answer the evaluators compare against.                   |
| `scenario`            | yes      | Scenario id; should match the `<scenario>` file stem.             |
| `reviewed`            | yes      | `todo` (new, awaiting review), `pass` (accepted), or `abandoned`. |
| `expected_references` | no       | List of `{title, link}`; defaults to `[]`.                        |
| `expected_knowledges` | no       | List of `{title, link}`; defaults to `[]`.                        |
| `tenant`              | no       | `null` unless a scenario needs explicit tenant routing.           |
| `source`              | no       | Provenance (blob filename or `manual`).                           |

`validate_file` enforces the schema and rejects duplicate **normalized queries** within
a file; with `--require-reviewed` it also requires every row to be `reviewed: "pass"`.

`target` is `basic` (PR-gate / curated) or `perf` (large, grown over time). Existing
scenarios: `typespec`, `python`, `apispec`, `onboarding`, `general`, `releasesupport`.
A new scenario is just a new `<scenario>.jsonl` file in the target dir.

**Env:** `dataset.curate` needs `az login` + `STORAGE_BLOB_ACCOUNT` +
`AI_ONLINE_PERFORMANCE_EVALUATION_STORAGE_CONTAINER`; `dataset.upload` needs `az login` +
`AZURE_AI_PROJECT_ENDPOINT`; `validate`/`review` need none. Copy and fill
`env-variables` into `.env` first.

## Manual add

Add specific cases you already have to an existing dataset.

1. Append canonical rows to `evaluation_datasets/<target>/<scenario>.jsonl`, each with
   `"reviewed": "pass"`, a unique `query`, and `"scenario"` matching the file stem.
2. `python -m dataset.validate evaluation_datasets/<target>/<scenario>.jsonl --require-reviewed`
3. `python -m dataset.upload --target <target> --scenario <scenario>`

## Curate from blob

Harvest new cases from Q&A markdown collected in storage.

1. Stage new, deduped candidates (rows start `reviewed: "todo"`): `python -m dataset.curate`
   (or `--source_md_path online-qa-tests` for a local md folder).
2. Review `evaluation_datasets/_staging/<scenario>.jsonl`: fix `ground_truth`/links and set
   `"reviewed": "pass"` on keepers.
3. Promote `pass` rows (leftover `todo` becomes `abandoned`):
   `python -m dataset.review --target <basic|perf>`.
4. `python -m dataset.upload --target <basic|perf>`.

## New dataset

Same as adding to a not-yet-existing file.

1. Create `evaluation_datasets/<target>/<new-scenario>.jsonl` with canonical rows
   (`"scenario": "<new-scenario>"`, `"reviewed": "pass"`) — or stage via `dataset.curate`
   when blob filenames carry the new scenario prefix, then review + promote.
2. Validate with `--require-reviewed`.
3. `python -m dataset.upload --target <target> --scenario <new-scenario>` — creates asset
   `qa-bot-<target>-<new-scenario>` versioned `<new-scenario>-YYYY-MM-DD` and records it in
   `registry.json`.
4. If it should run in CI, add the scenario to `perf-evaluation.yml` /
   `offline-evaluation.yml`.

Commit the per-scenario file, `registry.json`, and the updated `_staging/` files
(staging is tracked so concurrent contributors don't re-curate the same cases).
