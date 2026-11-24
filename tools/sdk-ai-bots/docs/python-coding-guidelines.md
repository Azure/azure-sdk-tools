# Python coding guidelines

These guidelines apply to new or modified Python code under `tools/sdk-ai-bots/`,
including tests and scripts, whether written by a person or a coding agent.
They address Azure/azure-sdk-pr#3230. The standard applies throughout this subtree;
automated enforcement starts with the explicitly listed clean scope below.

## Reuse established guidance

- Follow [PEP 8](https://peps.python.org/pep-0008/) for naming, imports, and layout,
  and [PEP 257](https://peps.python.org/pep-0257/) for docstrings.
- Use the Google Python Style Guide's guidance on
  [exceptions](https://google.github.io/styleguide/pyguide.html#24-exceptions),
  [mutable global state](https://google.github.io/styleguide/pyguide.html#25-mutable-global-state),
  [resource management](https://google.github.io/styleguide/pyguide.html#311-files-sockets-and-similar-stateful-resources),
  and [function length](https://google.github.io/styleguide/pyguide.html#318-function-length).
  This does not adopt every Google-specific convention or tool.
- Apply [Azure SDK Python implementation guidelines](https://azure.github.io/azure-sdk/python_implementation.html)
  when implementing SDK components; do not impose SDK-specific requirements on
  chatbot application code.

Explicit project configuration and documented compatibility requirements take
precedence over general style recommendations. Existing code is integration
context, not proof of good design: "follow existing patterns" does not require
copying a maintainability problem. Preserve behavior and supported Python versions.
If a conflict cannot be resolved within the task, explain it in the PR rather
than silently changing a contract or expanding into unrelated cleanup.

## Chatbot-specific review checklist

- **Keep responsibilities clear.** Request handlers should delegate orchestration,
  retrieval, and persistence rather than implement everything themselves. Extract
  cohesive behavior, not a new framework or a class for every function.
- **Make dependencies visible.** Pass clients and configuration explicitly where
  practical. Avoid hidden mutable globals and import-time network calls. Reuse
  existing lifecycle-managed clients; do not create one per helper call.
- **Type stable boundaries.** Annotate new or changed interfaces and use typed
  models for stable structured data. Keep dictionaries for genuinely dynamic data;
  do not add `Any`, casts, or suppressions merely to silence a type checker.
- **Distinguish failure from an empty result.** Catch specific exceptions only
  where recovery, useful context, or boundary translation is possible. Do not hide
  errors behind empty answers. Broad catches at framework boundaries need an
  explicit reporting and failure policy. Do not log secrets or raw user content.
- **Respect asynchronous execution.** Avoid blocking I/O in async request paths;
  use async clients or deliberately offload blocking work. Set appropriate network
  timeouts, respect cancellation, and make resource cleanup ownership explicit.
- **Test observable behavior.** Cover the changed success and failure paths with
  deterministic tests; isolate external services. Comments and docstrings should
  explain contracts or non-obvious decisions, not narrate the code.

### Example: an empty result is not a parsing failure

This illustrative boundary accepts a JSON array of source strings; it does not
define the chatbot's actual wire format.

Avoid masking malformed input as a successful response with no sources:

```python
import json


def decode_sources(payload: str) -> list[str]:
    try:
        return json.loads(payload)
    except Exception:
        return []
```

Instead, validate the boundary and let the caller apply its error policy:

```python
import json


def decode_sources(payload: str) -> list[str]:
    sources = json.loads(payload)
    if not isinstance(sources, list) or not all(
        isinstance(source, str) for source in sources
    ):
        raise ValueError("Expected a JSON array of source strings")
    return sources
```

Tests for the preferred version should distinguish `[]`, valid sources, malformed
JSON (`json.JSONDecodeError`), and an unexpected shape (`ValueError`).

## Coding-agent entry points

The repository-wide Copilot instructions and the Python-scoped instruction file
both reference this document for Copilot IDE and cloud-agent changes.
`tools/sdk-ai-bots/AGENTS.md` provides the same entry point for coding agents that
read `AGENTS.md`. Keep these entry points thin; change rules here, not in multiple
copies. They are separate from the deployed chatbot's runtime prompts.

Instructions communicate expectations; they cannot guarantee model compliance.
Use automated checks and the review checklist together.

## Local checks and CI

From `tools/sdk-ai-bots/`, use an activated virtual environment and run:

```text
python -m pip install -r requirements-checks.txt
python -m ruff check .
python -m ruff format --check .
python -m pytest azure-sdk-qa-bot-agent/tests/test_text_util.py
```

The shared manifest pins Ruff so local and CI checks use the same release.
Ruff targets Python 3.10, the agent's documented minimum, and uses an 88-character
formatting target. The selected rules check ambiguous syntax, undefined/unused
names, and broad exception handling. To apply formatting, run `python -m ruff format .`;
review the resulting diff before committing.

For changes that affect other agent modules, install the agent's
`requirements-dev.txt` and run the relevant tests and the existing Pyright check.
The [agent CI pipeline](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/azure-sdk-qa-bot-agent/pipelines/server-ci.yml)
contains the full Pyright and pytest commands, including integration-test exclusions.
The evaluation framework retains its own existing type-check configuration.
Do not introduce a competing type checker.

CI runs the same Ruff commands above alongside the agent's existing Pyright and
pytest steps. Changes to the shared guideline, agent instructions, tool manifest,
and Ruff configuration also trigger that pipeline.

## Incremental enforcement and remaining debt

The `include` list in
[ruff.toml](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/ruff.toml)
is the source of truth for the currently enforced files. It initially includes
`utils/text_util.py` and its test module in the agent.

- New Python modules and their tests must be added to the clean scope.
- For existing modules outside the scope, apply the review checklist to changes
  and run relevant existing tests. Add a module and its tests to the scope when
  they can pass the checks without unrelated cleanup.
- Remaining adoption work covers the other Python code in the agent, evaluation
  framework, and tools in this subtree. Exclusion does not certify compliance.
  Record discovered formatting, lint, or design debt in bounded follow-up issues
  or PRs instead of suppressing it or requiring a repository-wide rewrite.
- Do not add blanket ignores or raise complexity thresholds to make CI green.
  Explain any necessary narrow exception in the configuration or code review.

Passing checks does not replace review of responsibility boundaries, readability,
or error policies. PRs should identify the applicable checklist items, behavior
tests, commands run, and any checks that could not be completed.

### Real-code example

The agent's
[text preprocessing utility](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/azure-sdk-qa-bot-agent/utils/text_util.py)
demonstrates two checklist items: it no longer hides unexpected URL-decoder
failures, and it logs completion without recording the user's message.
Its focused tests cover decoding edge cases, error propagation, and log privacy
alongside the existing HTML, link, keyword, and branch-prefix behavior.
