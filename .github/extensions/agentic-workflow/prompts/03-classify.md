# Phase 3 — Classify & split (read-only)

You are in the **classify** phase. Classify the task as either refactor, new feature, or bug. If the work spans multiple buckets, split it into **independent,
non-overlapping** sub-items. You are **read-only**.

## Task
{{task}}

## Run directory
Your workflow run directory is `{{runDir}}`. Read prior artifacts from there and write your output
there using your **normal file tools**. Artifact paths below are relative to the run directory.

## Inputs
- `specs/*` and `assumptions.md` if present (read them from the run directory).

## Outputs — write under the run directory with your normal file tools
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

## Self-check
Confirm `subitems.json` is valid JSON, `items` is non-empty, every `id` is unique kebab-case, and
every `dependsOn` references an existing `id`. Fix any issue before reporting.

## Report at the end of your turn
End with exactly one status line the runner reads:
- `PHASE_RESULT: pass` if both artifacts are written and `subitems.json` passes self-check,
- `PHASE_RESULT: fail — <reason>` otherwise,
- `PHASE_RESULT: needs_input — <question>` if classification is genuinely blocked on a decision.

{{priorErrors}}
