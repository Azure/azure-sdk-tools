# Prompty Library Removal — Remaining TODO

## Status Summary

The `prompty` and `prompty.azure` library **imports have been fully removed** from all source code.
A custom `.prompty` file parser and Azure AI Foundry executor now lives in `src/_prompt_runner.py`.
The `.prompty` prompt template files under `prompts/` are **kept as-is** (they're just a human-readable format now parsed by our own code).

---

## Cleanup Tasks (should fix)

### 1. Update `evals/README.md`
- **File:** `evals/README.md` lines 224–230, 236, 260
- **Problem:** Code examples still show `prompty.execute(prompty_path, inputs=prompty_kwargs)` and reference `PromptyEvaluator`/`PromptySummaryEvaluator`.
- **Fix:** Update examples to use `_execute_prompt_template` (the current implementation). The class names (`PromptyEvaluator` etc.) can optionally be renamed too — see item 6.

### 2. Consider renaming evaluator classes in `evals/_custom.py` and `evals/_config_loader.py`
- **Files:** `evals/_custom.py` lines 204, 363; `evals/_config_loader.py` lines 27–28, 153–154
- **Problem:** Class names `PromptyEvaluator` and `PromptySummaryEvaluator` reference the old library name. They don't import or use the prompty library — just named after it.
- **Fix (optional):** Rename to `PromptEvaluator` / `PromptSummaryEvaluator` or similar. Low priority since it's just naming.

---

## Already Done (no action needed)

| Item | Status |
|------|--------|
| Remove `import prompty` / `from prompty` from all `src/` files | ✅ Done |
| Remove `import prompty` / `from prompty` from all `evals/` files | ✅ Done |
| Remove `prompty` / `prompty-azure` from `requirements.txt` | ✅ Done (was never in it, or already removed) |
| Remove `prompty` / `prompty-azure` from `dev_requirements.txt` | ✅ Done |
| Remove `prompty` / `prompty-azure` from `evals/requirements.txt` | ✅ Done |
| Implement custom `.prompty` parser in `_prompt_runner.py` | ✅ Done (`_parse_prompty`, `_render_template`) |
| Implement Azure AI Foundry executor in `_prompt_runner.py` | ✅ Done (`_execute_prompt_template`) |
| All `src/` modules use `run_prompt` from `_prompt_runner` | ✅ Done |
| `evals/_custom.py` uses `_execute_prompt_template` from `_prompt_runner` | ✅ Done |
| Keep `.prompty` template files in `prompts/` directory | ✅ Kept |
| Fix `app.py` broken `run_prompty` import (Bug 1) | ✅ Done — replaced with `run_prompt` from `_prompt_runner` |
| Remove unused `configuration`/`api_key` param (Bug 2) | ✅ Done — wired `api_key` into `AzureKeyCredential` for CI; falls back to `DefaultAzureCredential` |
| Uninstall stale `prompty 0.1.49` from `.venv` (Cleanup 3) | ✅ Done |

---

## Manual Prompt File Testing

Each `.prompty` file must be manually tested end-to-end through the custom `_prompt_runner.py` parser/executor to confirm it works correctly after the library removal. Mark each as verified once tested.

### `prompts/api_review/`

- [x] `context_diff_review.prompty`
- [x] `context_review.prompty`
- [x] `filter_comment_with_metadata.prompty`
- [x] `filter_existing_comment.prompty`
- [x] `filter_generic_comment.prompty`
- [x] `generate_correlation_ids.prompty`
- [x] `generic_diff_review.prompty`
- [x] `generic_review.prompty`
- [x] `guidelines_diff_review.prompty`
- [x] `guidelines_review.prompty`
- [x] `judge_comment_confidence.prompty`
- [x] `merge_comments.prompty`

### `prompts/evals/`

- [x] `eval_judge_prompt.prompty`

### `prompts/mention/`

- [x] `deduplicate_guidelines_issue.prompty`
- [x] `deduplicate_parser_issue.prompty`
- [x] `parse_conversation_action.prompty`
- [x] `parse_conversation_to_github_issue.prompty`
- [x] `parse_conversation_to_memory.prompty`
- [x] `summarize_actions.prompty`
- [x] `summarize_github_actions.prompty`

### `prompts/other/`

- [x] `analyze_comment_themes.prompty` Unsure
- [x] `resolve_package.prompty`
- [x] `summarize_metrics.prompty`

### `prompts/summarize/`

- [x] `summarize_api.prompty`
- [x] `summarize_diff.prompty`

### `prompts/thread_resolution/`

- [x] `parse_thread_resolution_action.prompty`
- [x] `parse_thread_resolution_to_memory.prompty`
- [x] `summarize_actions.prompty`
