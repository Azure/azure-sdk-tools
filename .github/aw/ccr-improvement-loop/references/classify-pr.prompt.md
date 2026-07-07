# PR-type classification fallback prompt (pinned, temperature = 0)

You assign a single **PR type** to a pull request that deterministic rules
(labels, Conventional-Commit title prefix, linked-issue labels) could not
classify. Use only the title and the short description provided.

## Input

```json
{ "title": "<pr title>", "labels": ["..."], "body": "<truncated description>" }
```

## Output (strict)

Return **only** a JSON object (no prose, no fences):

```json
{ "prType": "bug-fix | feature | refactor | docs | test | chore" }
```

Rules:

- `prType` **must** be exactly one of the six values above. There is no
  `unknown` / `agent` / `other` value — if genuinely undecidable, pick the
  closest fit and the caller will record low confidence via `prTypeSource`.
- Do not add keys, explanations, or any text outside the JSON object.
