---
name: skill-eval-authoring
description: |
  Author and harden Vally eval suites for Azure SDK skills (.github/skills/*/evals/).
  WHEN: "write an eval", "add a capability test", "skill is missing tests",
  "fix vacuous output-contains", "review my eval.yaml", "apply the four-layer pattern",
  "add trigger and anti-trigger tests", "lint skill evals".
  DO NOT USE FOR: running the Vally CLI directly (use the Vally primer), debugging
  failing test trajectories, MCP tool implementation, SKILL.md authoring itself.
  INVOKES: file_search, read_file, create_file, replace_string_in_file, run_in_terminal
  (for `vally lint` and `vally eval --tag area=<skill>`).
---

# Skill: Eval authoring for `.github/skills`

## What this skill is for

Skill evals in this repo are not pass/fail vibes &mdash; they are the **contract** a
skill makes with the agent runtime. This skill teaches the agent (and you) how to
write evals that actually pin that contract down, so a silent regression cannot
sneak through CI.

Use this skill when:

- You created a new skill under `.github/skills/<name>/` and the `evals/` folder
  is empty or stubby.
- An existing capability test is graded only by `output-contains "<single word>"`
  and you want to harden it.
- A reviewer asked you to add trigger / anti-trigger coverage.
- You need to lint the whole `.github/skills` tree before opening a PR.

## The four-layer pattern (memorize this)

Every capability stimulus should assert as many of these layers as apply. **At
minimum**, layer 1 + layer 2.

| Layer | Grader | Asserts |
|-------|--------|---------|
| 1. Routing | `skill-invocation` | The right skill was loaded for this prompt |
| 2. Tool-use | `tool-calls` | The right MCP tool was invoked (and wrong ones were not) |
| 3. Output shape | `output-matches` or `output-contains` | The reply structurally addresses the request |
| 4. Judgment | `prompt` (LLM) | Anything the first three cannot express; use sparingly |

Negative stimuli flip layers 1 and 2 to `disallowed:`.

## Folder layout to produce

```
.github/skills/<skill-name>/
├── SKILL.md
├── references/                       # optional, for long docs the skill points to
└── evals/
    ├── eval.yaml                     # capability tests
    └── trigger.eval.yaml             # routing tests (positive + anti-trigger)
```

## Hard rules (the linter will enforce these)

1. Every `eval.yaml` and `trigger.eval.yaml` MUST set `scoring.threshold`
   (recommended: `0.8`). Vally treats missing threshold as 0 &mdash; everything
   passes.
2. No capability stimulus may have only `output-contains` as its grader, unless
   the substring is &ge; 20 chars AND not a prefix of the prompt itself.
3. Every capability stimulus MUST have at least one `tool-calls` OR
   `skill-invocation` grader.
4. Tool names in `tool-calls.required` / `disallowed` should be the **bare**
   form (`azsdk_create_release_plan`), not the MCP-prefixed form, for
   portability across environments. (Both match, the bare form is just clearer.)
5. Negative stimuli MUST assert tool absence with `tool-calls.disallowed`, not
   only `output-not-contains`. Output silence is cheap; tool absence is the
   actual contract.

## Canonical exemplar

When in doubt, copy the shape of
[.github/skills/azsdk-common-prepare-release-plan/evals/eval.yaml](../azsdk-common-prepare-release-plan/evals/eval.yaml).
It is intentionally the gold-standard reference for this skill.

## Workflow the agent should follow

1. **Read the target skill's `SKILL.md`.** Pull the `INVOKES:` tool list and
   the `WHEN:` / `DO NOT USE FOR:` phrases.
2. **Inventory the evals folder.** Note any existing stimuli; new work should be
   additive unless explicitly asked to rewrite.
3. **Generate one capability stimulus per `INVOKES:` tool.** Use the four-layer
   pattern. Pick prompts that are concrete (real-ish URLs, version numbers,
   project paths) so the model has to call the tool, not just paraphrase.
4. **Generate trigger / anti-trigger entries** from the `WHEN:` and
   `DO NOT USE FOR:` phrases. Each phrase becomes one stimulus with a single
   `skill-invocation` grader.
5. **Set `scoring.threshold: 0.8`** and add tags:
   `area: <skill-name>`, `type: ci-gate`, `tier: integration` (or `unit` if the
   tests are prompt-only refusals).
6. **Run the lint script and a focused eval:**
   ```powershell
   cd .github/skills
   vally lint .
   vally eval --tag area=<skill-name> --output-dir ./results
   ```
7. **Open a PR** with: the new YAML files, a one-paragraph summary of what is
   covered (per tool) and what is not yet covered (with a follow-up note).

## Anti-patterns to call out in review

| Smell | Why it's bad | Fix |
|-------|--------------|-----|
| `output-contains: "release plan"` as the only grader | Passes on refusals, paraphrases, wrong-tool calls | Add `tool-calls.required` + `skill-invocation.required` |
| Stimulus prompt and substring are the same phrase | Model parrots the prompt &rarr; trivially passes | Use `output-matches` with a structural regex (e.g. `"(created|link|id).*(work item|plan)"`) |
| Negative test uses only `output-not-contains` | Model can call a forbidden tool then politely not mention it | Add `tool-calls.disallowed` |
| No `trigger.eval.yaml` at all | Routing regressions undetected | Add minimum 3 trigger + 3 anti-trigger stimuli |
| `scoring.weights` set but no `threshold` | Weights are parsed but not applied; threshold defaults to 0 | Set `threshold: 0.8`; drop weights unless you've verified the runtime applies them |
| Tool name with a typo (`azsdk_creat_release_plan`) | Regex match silently fails &rarr; test "passes" tool-disallowed checks | Cross-check against the actual `toolName` strings in a recent `results.jsonl` |

## References

- `references/four-layer-pattern.md` &mdash; deep dive with copy-paste templates.
- `references/grader-catalog.md` &mdash; what each grader does, when to reach for it.
- `references/anti-patterns.md` &mdash; the full list of smells and fixes.
- Notebook design doc:
  [Skill evals &mdash; status, redesign, adoption](file:///C:/Users/gaoh/notebooks/azsdk-evals/design-vally-eval-framework.html).
- Vally Skills &amp; Tests primer:
  [primer-vally-skills-tests.html](file:///C:/Users/gaoh/notebooks/azsdk-evals/primer-vally-skills-tests.html).
