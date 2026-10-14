# Historical API spec review dataset

This category contains 473 reusable questions extracted from the historical API Spec Review channel export. It is separate from `basic` and `perf` and is not part of the scheduled perf matrix. The question and historical ground-truth text are preserved from the reviewed corpus; `case_id` and the original `dedup_key` remain stable across topic classification.

| File | Cases | Classification |
| --- | ---: | --- |
| [auth.jsonl](auth.jsonl) | 29 | Repository access, organization membership, permissions, and CLA |
| [spec-review.jsonl](spec-review.jsonl) | 158 | Specification validation checks, suppressions, and diagnostics |
| [sdk-review.jsonl](sdk-review.jsonl) | 53 | SDK validation, package names, SDK compatibility, and review |
| [review-process.jsonl](review-process.jsonl) | 49 | Reviewers, ownership, sign-off, queues, and merge approvals |
| [versioning.jsonl](versioning.jsonl) | 63 | API lifecycle, compatibility, version changes, and immutability |
| [authoring.jsonl](authoring.jsonl) | 49 | TypeSpec/OpenAPI modeling, templates, and conversion |
| [repository-workflow.jsonl](repository-workflow.jsonl) | 64 | Branches, forks, synchronization, and check reruns |
| [documentation.jsonl](documentation.jsonl) | 8 | Reference publishing, previews, and table-of-contents updates |

Every row sets `scenario` to its topic file stem and `tenant` to `api_spec_review_bot`. Explicit row tenant settings take precedence over generic scenario-to-channel mappings, including topics such as `authoring` that otherwise route elsewhere.

Ground truths use expert replies from someone other than the question author when available, including corrections and additions to bot answers. The 94 bot-fallback cases are not independent expert truth. Historical policies and workarounds must not be interpreted as unconditional current guidance; mixed questions freeze the relevant historical facts and avoid live status lookups.

```powershell
python -m dataset.validate evaluation_datasets\api-spec-review --require-reviewed
python evals_run.py --dataset evaluation_datasets\api-spec-review\auth.jsonl --baseline_check False --is_ci False
```

The existing upload and promotion CLIs still target `basic` and `perf`; do not move this corpus into perf merely to use those workflows. The old perf Foundry asset is retained only as provenance for the completed historical replay.

Reports: [evaluation results](../../results/api-spec-review/evaluation-report.md) and [expert knowledge and KB coverage](../../results/api-spec-review/expert-knowledge-report.md).
