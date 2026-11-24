---
applyTo: "tools/sdk-ai-bots/**/*.py"
---

# Chatbot Python changes

Read and apply the shared [Python coding guidelines](../../tools/sdk-ai-bots/docs/python-coding-guidelines.md)
before generating or modifying Python code in this subtree. The same standards
apply to human-written and agent-generated code.

- Interpret the repository's "follow existing patterns" instruction as preserving
  integration and compatibility, not copying maintainability problems.
- Keep responsibilities and dependencies explicit, type stable interfaces, and
  distinguish failures from valid empty results.
- Avoid blocking I/O in async paths and define resource ownership and timeouts.
- Keep changes focused. Explain unresolved conflicts with legacy code instead of
  silently changing behavior or performing unrelated refactoring.
- Use the affected component's existing checks and add focused behavioral tests.
  Run the shared Ruff checks documented in the guideline. Add new Python modules
  and their tests to its incremental enforcement scope.
  Report the commands run and any checks that could not be completed.
