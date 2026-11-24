# Python coding guidelines

These guidelines apply to new or modified Python code under `tools/sdk-ai-bots/`,
including tests and scripts, whether written by a person or a coding agent.
They are a pilot for Azure/azure-sdk-pr#3230.

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

## Validation and adoption

Use the affected component's documented development and CI commands. The
[agent CI pipeline](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/azure-sdk-qa-bot-agent/pipelines/server-ci.yml) already runs
Pyright and pytest; use those checks rather than adding a competing type checker.
Passing checks does not replace review of responsibilities and error handling.

This documentation-only pilot adds no dependencies, formatter, lint rules, CI
gates, or runtime changes. Apply the checklist during review of subsequent Python
changes. Record existing debt separately instead of requiring a repository-wide
rewrite. After reviewing the pilot, select formatter/linter configuration and
an incremental enforcement scope in a separate change.
