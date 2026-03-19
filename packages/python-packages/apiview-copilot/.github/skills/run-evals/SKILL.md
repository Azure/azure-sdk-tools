---
name: run-evals
description: "Run evaluation tests for prompt quality. Use for: run evals, validate prompt changes, check prompt quality, eval recording, evaluation failure, debug eval, use recording."
---

# Run Evaluations

## When to Use
- After modifying any `.prompty` file to validate quality hasn't regressed
- Debugging failing or partial eval results
- Iterating on prompts with cached recordings to avoid LLM costs

## Running Evals

Always activate the virtualenv first: `.venv\Scripts\activate` (Windows) or `source .venv/bin/activate` (Linux/macOS).

### Via CLI
```bash
# Run all workflows
avc eval run

# Run a specific workflow
avc eval run --test-paths evals/tests/mention_action

# Run a single test file
avc eval run --test-paths evals/tests/filter_existing_comment/discard_azure_sdk_repeat_comment.yaml

# Multiple runs (median result kept)
avc eval run --num-runs 5 --test-paths evals/tests/filter_comment_metadata

# Use recordings (cached LLM responses) — first run saves, subsequent runs reuse
avc eval run --use-recording --test-paths evals/tests/mention_action

# Verbose output (show passing tests too)
avc eval run --style verbose
```

### Via run.py directly
```bash
cd evals
python run.py --test-paths tests/mention_action
```

## Existing Workflows

| Workflow directory | Kind | Target function in `_custom.py` | Prompt tested |
|---|---|---|---|
| `mention_action` | `prompt` | `_mention_action_workflow` | `parse_conversation_action.prompty` |
| `mention_summarize` | `summarize_prompt` | `_mention_summarize_workflow` | `summarize_github_actions.prompty` |
| `thread_resolution_action` | `prompt` | `_thread_resolution_action_workflow` | `parse_thread_resolution_action.prompty` |
| `filter_comment_metadata` | `prompt` | `_filter_comment_metadata` | `filter_comment_with_metadata.prompty` |
| `filter_existing_comment` | `prompt` | `_filter_existing_comment` | `filter_existing_comment.prompty` |
| `deduplicate_parser_issue` | `prompt` | `_deduplicate_parser_issue` | `deduplicate_parser_issue.prompty` |
| `deduplicate_guidelines_issue` | `prompt` | `_deduplicate_guidelines_issue` | `deduplicate_guidelines_issue.prompty` |

### Evaluator kinds
- **`prompt`** — Action-based. Compares expected vs actual action, then similarity-scores the rationale. Wrong action = 0%.
- **`summarize_prompt`** — Summary-based. Uses `SimilarityEvaluator` on full output. Success threshold: score > 70%.

## Recordings

- Stored in `evals/recordings/<workflow_name>/<testcase_id>.json`
- Gitignored — each dev builds their own cache
- If you change a test file, delete its recording or run without `--use-recording`
- `--use-recording` on first run makes LLM calls and saves; subsequent runs reuse cached responses

## Gotchas

- **Use `python cli.py` not `.\avc`**: The `avc.bat` script calls bare `python` which may resolve to the system Python instead of the venv. Use `.venv\Scripts\activate; python cli.py eval run ...` to ensure the venv Python is used.
- **Field name mismatch**: Test YAML fields must exactly match target function parameter names (excluding `testcase` and `response`)
- **Stale recordings**: After changing a prompt, delete recordings or run without `--use-recording` to get fresh results
- **Testcase uniqueness**: The `testcase` field must be unique across all test files in a workflow — it's the cache key
- **Kind validation**: The `kind` in `test-config.yaml` must be registered in `_config_loader.py` (`prompt` or `summarize_prompt`)
