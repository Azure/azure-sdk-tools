---
name: eval-authoring-tool
description: 'Author and validate hermetic single-tool Vally evals under evals/tools. WHEN: "write a tool eval", "add prompt-to-tool coverage", "test MCP tool selection", "add tool catalog eval", "create single-tool scenario", "harden tool-call grader". DO NOT USE FOR: skill routing or capability evals (use eval-authoring-skill), multi-tool, multi-turn, or live scenarios (use eval-authoring-workflow).'
license: MIT
metadata:
  author: Microsoft
  version: "1.1.0"
compatibility: "copilot-chat, @microsoft/vally-cli 0.14.0"
---

# Tool Eval Authoring

Author Vally evals that verify **one MCP tool** is selected correctly for a given prompt, under `evals/tools/`. All the actual guidance — naming convention, placement, per-category requirements, glossary, grading patterns, grader catalog, anti-patterns, and worked examples — lives in one shared place so it never drifts across the three eval-authoring skills: the repository-local eval authoring guide at `.github/skills/eval-authoring/README.md`. This skill exists to route you there with tool-eval context already loaded, not to duplicate it.

## Triggers

USE FOR: write a tool eval, add prompt-to-tool coverage, test MCP tool selection, add tool catalog eval, create single-tool scenario, harden tool-call grader
WHEN: "write a tool eval", "add prompt-to-tool coverage", "test MCP tool selection", "add tool catalog eval", "create single-tool scenario", "harden tool-call grader"
DO NOT USE FOR: skill routing or capability evals (use eval-authoring-skill), multi-tool, multi-turn, or live scenarios (use eval-authoring-workflow)

## Steps

1. Read the eval authoring guide (`.github/skills/eval-authoring/README.md`) Step 0 to find this repo's `vallyRoot`/`evalGlobs` for the tool tier, then the guide's "Tool" column throughout (naming, requirements, worked example).
2. Confirm the scenario expects one primary tool. If success requires orchestration, conversation state, or live services, use `eval-authoring-workflow` instead.
3. Add stimuli to the matching `prompt-to-tool-<area>.eval.yaml`; create a separate file only when it needs fixtures or outcome grading.
4. Use realistic, concrete prompts with multiple natural phrasings and collision cases that disallow the nearest competing tool. Make `tool-calls` the primary signal; avoid the anti-patterns in the guide (vacuous keyword-only grading, missing `scoring.threshold`).
5. Keep the unit tier hermetic (`environment: azsdk-mcp-mock`, `tags.tier: unit`, established area tag). Avoid git worktrees and production writes.
6. Run strict eval-spec lint, then validate locally per the guide's "Running evals locally" section, building the mock MCP first. Do not finish or open a PR until both lint and the focused eval pass; inspect recorded tool names and arguments on failure.

## Rules

- Use exact MCP tool names and assert forbidden alternatives where ambiguity exists.
- Do not force a tool through unnatural prompt instructions unless direct invocation is the contract being tested.
- Keep fixture paths relative to the eval file and fixture data minimal and non-secret.
- Preserve existing namespace coverage and add new stimuli instead of duplicating files.

## References

- Eval authoring guide: `.github/skills/eval-authoring/README.md` — naming, placement, requirements, glossary, graders, anti-patterns, worked examples, local commands. Shared with `eval-authoring-skill`/`eval-authoring-workflow` — update it there, not per-skill.
- Repository-local eval README/configuration discovered in Step 0 (if present)
