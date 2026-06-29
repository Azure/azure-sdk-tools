# Phase 3 — Classify & split (read-only)

You are in the **classify** phase. Classify the task as either refactor, new feature, or bug. If the work spans multiple buckets, split it into **independent,
non-overlapping** sub-items. You are **read-only**.

## Task
{{task}}

## Inputs
- `specs/*` and `assumptions.md` if present. {{researchNote}}

## Outputs — via the `write_artifact` tool
1. `classification.md` — human-readable: the overall classification
   (`feature` / `bug` / `refactor` / `mixed`) and the reasoning for the split.
2. `subitems.json` — machine-readable, matching this shape exactly:
   ```json
   {
     "task": "string — original task description",
     "classification": "feature | bug | refactor | mixed",
     "items": [
       {
         "id": "kebab-case-id",
         "type": "feature | bug | refactor",
         "title": "short title",
         "description": "what this sub-item covers",
         "rationale": "why it is a separate item",
         "dependsOn": ["other-item-id"],
         "expectedFilesOrAreas": ["src/api/**"],
         "acceptanceCriteria": ["..."],
         "nonGoals": ["..."],
         "overlapRisk": "low | medium | high"
       }
     ]
   }
   ```

## Constraints
- Aim for sub-items that are **independent and non-overlapping**; populate `dependsOn` and
  `overlapRisk` honestly so later phases can order/parallelize safely.
- `id` values are unique, kebab-case. `items` must be non-empty.
- Read-only; no source edits, no shell.
- End your turn once both artifacts are written.
