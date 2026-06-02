# Four-layer pattern — deep dive

Every capability stimulus in a skill eval has a *contract* it's verifying. The
four layers below are the dimensions of that contract. Each layer answers one
question; together they pin down what "the skill works" actually means.

## Layer 1 — Routing (`skill-invocation`)

> Did the right skill get loaded for this prompt?

```yaml
- type: skill-invocation
  config:
    required: ["azsdk-common-prepare-release-plan"]
```

Use `disallowed:` for negative tests where the skill must NOT activate.

## Layer 2 — Tool-use (`tool-calls`)

> Did the agent call the right MCP tool with the right args?

```yaml
- type: tool-calls
  config:
    required:
      - name: azsdk_create_release_plan
    disallowed:
      - name: azsdk_release_sdk
```

`name` is compiled as a regex. The bare form (`azsdk_create_release_plan`) is
preferred because it's portable across MCP server prefixes. You can also
constrain `command` and `path` argument values with their own regex.

## Layer 3 — Output shape (`output-matches`)

> Did the agent's reply structurally address the request?

```yaml
- type: output-matches
  config:
    pattern: "(release plan|work item).*(created|link|id)"
```

Prefer `output-matches` over `output-contains` for capability tests. A regex
that requires two related concepts in proximity is much harder to game than a
single keyword.

## Layer 4 — Judgment (`prompt`)

> Anything the first three cannot express. Use sparingly.

```yaml
- type: prompt
  config:
    prompt: |
      Did the assistant confirm creation AND surface the link/id back to
      the user? Score 1 for yes, 0 for no.
```

LLM judges are expensive and non-deterministic. Reach for them only when a
regex would be brittle or a tool-call check is insufficient (e.g., verifying
the agent *explained* a failure correctly to the user).

## Composition cheat-sheet

| Stimulus kind | Required layers |
|---------------|-----------------|
| Capability — happy path | 1 + 2, optional 3 |
| Capability — multi-tool flow | 1 + 2 with multiple required entries |
| Negative — wrong topic | 1 disallowed + 2 disallowed |
| Trigger | 1 required only |
| Anti-trigger | 1 disallowed only |
| Performance | 2 + `metric-threshold` |
