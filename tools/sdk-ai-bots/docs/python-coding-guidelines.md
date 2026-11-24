# Python coding guidelines

These guidelines apply to new or modified Python code under `tools/sdk-ai-bots/`,
including tests and scripts, whether written by a person or a coding agent.
They address Azure/azure-sdk-pr#3230. This guidance does not introduce new tools,
dependencies, CI checks, or changes to existing application code.

## Reuse established guidance

- Follow [PEP 8](https://peps.python.org/pep-0008/) for naming, imports, and layout,
  and [PEP 257](https://peps.python.org/pep-0257/) for docstrings.
- Use Microsoft's Engineering Fundamentals Playbook:
  [Python Code Reviews](https://microsoft.github.io/code-with-engineering-playbook/code-reviews/recipes/python/)
  for Python readability and type hints, and
  [Reviewer Guidance](https://microsoft.github.io/code-with-engineering-playbook/code-reviews/process-guidance/reviewer-guidance/)
  for responsibility boundaries, complexity, error handling, and tests.
  These are Microsoft-maintained public references, not a company-wide internal
  Python policy. Their tool-installation examples do not change this project's tooling.

Explicit project configuration and documented compatibility requirements take
precedence over general style recommendations. Existing code is integration
context, not proof of good design: "follow existing patterns" does not require
copying a maintainability problem. Preserve behavior and supported Python versions.
If a conflict cannot be resolved within the task, explain it in the PR rather
than silently changing a contract or expanding into unrelated cleanup.

## Review checklist for chatbot Python

This checklist adapts the review principles above to the request handlers,
agents, retrieval, and persistence components in the
[chatbot architecture](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/README.md#architecture-overview).
It is a project-specific synthesis, not a separate published Python standard.

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
- **Respect asynchronous execution.** Follow Python's guidance on
  [avoiding blocking work in the event loop](https://docs.python.org/3/library/asyncio-dev.html#running-blocking-code);
  use async clients or deliberately offload blocking work. Set appropriate network
  timeouts, respect cancellation, and make resource cleanup ownership explicit.
- **Test observable behavior.** Cover the changed success and failure paths with
  deterministic tests; isolate external services. Comments and docstrings should
  explain contracts or non-obvious decisions, not narrate the code.
  Use the affected component's existing checks and report commands run or checks
  that could not be completed. Passing checks does not replace design review.
