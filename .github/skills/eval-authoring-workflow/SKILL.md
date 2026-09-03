---
name: eval-authoring-workflow
description: 'Author and validate multi-tool, multi-turn, mock, and live Vally scenarios under evals/workflows. WHEN: "write a workflow eval", "add end-to-end eval", "test tool sequence", "create multi-turn eval", "add mock workflow scenario", "add live eval". DO NOT USE FOR: per-skill routing/capability evals (use eval-authoring-skill), isolated prompt-to-tool tests (use eval-authoring-tool).'
license: MIT
metadata:
  author: Microsoft
  version: "1.1.0"
compatibility: "copilot-chat, @microsoft/vally-cli 0.14.0"
---

# Workflow Eval Authoring

Author Vally evals that verify **multi-skill, multi-tool, or multi-turn** orchestration, state, and outcomes across an entire agent conversation, under `evals/workflows/`. All the actual guidance — naming convention, placement, per-category requirements, glossary, grading patterns, grader catalog, anti-patterns, and worked examples — lives in one shared place so it never drifts across the three eval-authoring skills: the repository-local eval authoring guide at `.github/skills/eval-authoring/README.md`. This skill exists to route you there with workflow-eval context already loaded, not to duplicate it.

## Triggers

USE FOR: write a workflow eval, add end-to-end eval, test tool sequence, create multi-turn eval, add mock workflow scenario, add live eval
WHEN: "write a workflow eval", "add end-to-end eval", "test tool sequence", "create multi-turn eval", "add mock workflow scenario", "add live eval"
DO NOT USE FOR: per-skill routing/capability evals (use eval-authoring-skill), isolated prompt-to-tool tests (use eval-authoring-tool)

## Steps

1. Read the eval authoring guide (`.github/skills/eval-authoring/README.md`) Step 0 to find this repo's `vallyRoot`/`evalGlobs` for the workflow tier, then the guide's "Workflow" column throughout (naming, requirements, worked example).
2. Use `evals/workflows/mock/` by default. Choose `live/` only when the behavior cannot be represented by the mock MCP; document writes, authentication, cleanup, and nightly-only execution.
3. Model one user goal per stimulus. Use `turns` only when conversation state matters; otherwise keep a single prompt. Mount every candidate skill explicitly, provide minimal file/git fixtures, and bound turns, tokens, workers, and timeout.
4. Combine process and outcome graders per the guide's four-layer pattern and grader catalog: required/disallowed skills and tools, ordering where essential, files/commands, response quality. Avoid overfitting to incidental call sequences and the guide's anti-patterns (e.g. boundary anti-triggers with no competing skill mounted).
5. Scope graders to `turn` only when that turn owns the assertion unambiguously; otherwise grade the full conversation.
6. Materialize generated fixture sources, run strict eval-spec lint, then validate locally per the guide's "Running evals locally" section. Build the matching MCP and prime git fixtures when required. Do not finish or open a PR until both lint and the focused eval pass; inspect every turn and tool call on failure.

## Rules

- Keep mock workflows hermetic and repeatable; use fake IDs and canned responses.
- Never run a live write scenario without the documented test area and safety environment.
- Preserve relative path invariants for `environment.skills`, `files`, and `git.source`; every `environment.files` entry needs both `src` and `dest`.
- Weight by grader type, include every used type (use `0` only intentionally), and normalize positive weights to `1.0`.

## References

- Eval authoring guide: `.github/skills/eval-authoring/README.md` — naming, placement, requirements, glossary, graders, anti-patterns, worked examples, local commands. Shared with `eval-authoring-skill`/`eval-authoring-tool` — update it there, not per-skill.
- Repository-local eval README/configuration discovered in Step 0 (if present)
