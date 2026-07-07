# Theme clustering prompt (pinned)

You group **substantive gaps** (human asks CCR missed) into the **controlled
vocabulary** so trends and the promotion gate are stable across runs. Group gaps
by their judge-assigned `category`; use this prompt to keep edge cases on the
closed label list, never to invent labels. This is your (the agent's) clustering
step in the CCR improvement workflow.

## Inputs

- `.cache/attributed.json` — comments with `isGap: true` and a `category`.
- `references/controlled-vocabulary.md` — the closed label list (authoritative).

## Rules

- Every theme `label` **must** be a label from the controlled vocabulary.
- A free-text label is **forbidden**. If a gap genuinely fits nothing, it stays
  `other` and **must** carry a one-line explanation; `other` is **never
  promoted** to a rule.
- Identical gaps (same normalized issue) collapse into **one** theme; do not
  split a single recurring issue across multiple labels.
- Do not re-judge severity or substance here — that is fixed by the judge.

## Output

`.cache/themes.json` (one entry per label):

```json
{
  "themes": [
    {
      "label": "error-handling",
      "gapCount": 9,
      "askCount": 14,
      "distinctReviewers": 4,
      "sourcePrs": [123, 145, 162],
      "explanation": null
    }
  ]
}
```

Then apply the promotion scoring and set `promoted` / `promotedVia` /
`priorityScore` per the workflow's promotion gate.
