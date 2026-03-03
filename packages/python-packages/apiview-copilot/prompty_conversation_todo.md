# Prompty Library Removal — Remaining TODO

## Status Summary

The `prompty` and `prompty.azure` library **imports have been fully removed** from all source code.
A custom `.prompty` file parser and Azure AI Foundry executor now lives in `src/_prompt_runner.py`.
The `.prompty` prompt template files under `prompts/` are **kept as-is** (they're just a human-readable format now parsed by our own code).

---

## Bugs / Broken Code (must fix)

### ~~1. `app.py` — broken `run_prompty` import~~ ✅ Fixed
- Replaced `run_prompty` import/call with `run_prompt` from `src._prompt_runner`.

### ~~2. `_prompt_runner.py` — `configuration` / `api_key` parameter is accepted but ignored~~ ✅ Fixed
- Wired `configuration["api_key"]` into the `ChatCompletionsClient` via `AzureKeyCredential` so CI evals authenticate correctly. Falls back to `DefaultAzureCredential` when no key is provided.

---

## Cleanup Tasks (should fix)

### ~~3. Uninstall `prompty` from `.venv`~~ ✅ Fixed
- Ran `pip uninstall prompty -y`, verified imports still work.

### ~~4. Update `.github/copilot-instructions.md`~~ ✅ Fixed
- Removed `run_prompty` reference from `_utils.py` description.
- Updated Prompt Execution section to describe custom `_prompt_runner.py` implementation using Azure AI Foundry `ChatCompletionsClient`.
- Updated Evaluation Tests section to reference `_execute_prompt_template` instead of the prompty library.

### 5. Update `evals/README.md`
- **File:** `evals/README.md` lines 224–230, 236, 260
- **Problem:** Code examples still show `prompty.execute(prompty_path, inputs=prompty_kwargs)` and reference `PromptyEvaluator`/`PromptySummaryEvaluator`.
- **Fix:** Update examples to use `_execute_prompt_template` (the current implementation). The class names (`PromptyEvaluator` etc.) can optionally be renamed too — see item 6.

### 6. Consider renaming evaluator classes in `evals/_custom.py` and `evals/_config_loader.py`
- **Files:** `evals/_custom.py` lines 204, 363; `evals/_config_loader.py` lines 27–28, 153–154
- **Problem:** Class names `PromptyEvaluator` and `PromptySummaryEvaluator` reference the old library name. They don't import or use the prompty library — just named after it.
- **Fix (optional):** Rename to `PromptEvaluator` / `PromptSummaryEvaluator` or similar. Low priority since it's just naming.

### ~~7. Tests — variable naming~~ ✅ Fixed
- Renamed `mock_prompty` → `mock_run_prompt` at all 9 occurrences across 4 test methods in `tests/apiview_test.py`.
- All 38 tests pass.

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
