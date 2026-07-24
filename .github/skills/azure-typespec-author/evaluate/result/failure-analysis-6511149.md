# Benchmark failure analysis — build 6511149 (main) vs 6486659 (branch)

**Pipeline:** `azure-typespec-author-benchmark` (definitionId 8178), org `azure-sdk`, project `internal`
**This run:** buildId **6511149**, branch `main`, 2026-07-02, result `partiallySucceeded`
**Baseline compared:** buildId **6486659**, branch `typespec-authoring-agentic-search-only-clean`

## Headline

| Suite | Branch (6486659) | Main (6511149) |
|---|---|---|
| code-quality | 25/29 | **10/29** |
| skill-invocation | 25/29 | **25/29** |

The code-quality "collapse" is **~90% a grading artifact**, not an agent regression. Proven by
comparing the *same stimuli* across both runs.

## Root cause: `prompt` grader `threshold: 1.0` + non-deterministic LLM judge

The `prompt` grader (LLM judge `claude-opus-4.6`, defined in `suites/forced.eval.yaml`, e.g. lines
60-65) requires a **perfect 1.0** to pass. The judge is stochastic and this run systematically
awarded **0.889 (8/9)** — usually docking the *"output is clear and well-structured"* sub-criterion —
even while labeling the work **"correct"**. `report-eval-results.sh` treats `score >= 1.0` as pass,
so 0.889 -> FAIL.

### Smoking gun — identical stimuli, perfect on branch, 0.889 on main (all judge-labeled "correct")

| Stimulus | Branch prompt | Main prompt |
|---|---|---|
| 001002-version-default-value | 1.000 PASS | 0.889 FAIL |
| 001003-version-required-to-optional | 1.000 PASS | 0.889 FAIL |
| 001004-version-property-decorator | 1.000 PASS | 0.778 FAIL |
| 001006-version-add-preview-after-stable | 1.000 PASS | 0.889 FAIL |
| 001008-version-add-stable-after-stable | 1.000 PASS | 0.889 FAIL |
| 002001-ARM-change-resource-type | 1.000 PASS | 0.889 FAIL |
| 002004-ARM-define-extension-resource-fromProxyResource | 1.000 PASS | 0.889 FAIL |
| 002005-ARM-define-the-resource | 1.000 PASS | 0.889 FAIL |
| 002006-ARM-define-child-resource | 1.000 PASS | 0.889 FAIL |
| 002007-ARM-define-custom-action | 1.000 PASS | 0.889 FAIL |
| 002009-arm-add-patch-operation-to-resource | 1.000 PASS | 0.889 FAIL |
| 002011-arm-add-check-existence-operation | 1.000 PASS | 0.889 FAIL |
| 003001-arm-action-lro | 1.000 PASS | 0.889 FAIL |
| 003002-arm-modify-response | 1.000 PASS | 0.889 FAIL |
| 004001-decorate-mgmt-resource-name-parameter | 1.000 PASS | 0.889 FAIL |

Noise runs both directions (further proof it is not a regression):
- **002010-arm-action-sync-operation**: 0.000 FAIL (branch) -> **1.000 PASS** (main)
- **004003** (code-quality aggregate): 0.923 FAIL (branch) -> **1.000 PASS** (main)

## Genuine, content-based failures (real action items)

- **002008-ARM-add-parameters — 0.333 on BOTH runs.** Repeatable capability gap: agent emits raw
  `@query("$top")/@query("$skip")` instead of
  `ArmListBySubscription<Employee, {...TopQueryParameter; ...SkipQueryParameter}>`.
- **001001-version-spread-property — 1.000 PASS (branch) -> 0.111 FAIL (main).** On main the agent
  added `#suppress "@azure-tools/typespec-azure-resource-manager/arm-resource-invalid-envelope-property"`
  to silence the ARM linter. The rubric explicitly disqualifies *any* suppression, so the judge
  labeled it **"incorrect."** A genuine bad shortcut.
- **005001 (0.667), 001013 code-quality (0.778)** — partial/mixed; borderline, partly judge strictness.

## Skill-invocation: no regression (25/29 both runs)

Same trigger-reliability gap (G1 in `document-gaps.md`); the 4 failures also shuffle run-to-run:
- **004003** (operationId decorator edit) — fails both runs; consistently doesn't trigger the skill.
- **001011** fails both. **001013** passed branch / failed main. **003001** reverse. **005001** improved.

## Recommendations

**Eval side (highest leverage):**
1. Lower the `prompt` grader `threshold` to ~0.8, OR split "task correctness" from "output clarity"
   so a stylistic 8/9 does not fail a functionally-correct case. Without this, pass rates will keep
   swinging 10 <-> 25 on judge noise.
2. Consider making the judge more deterministic (temperature 0, fixed rubric anchors) to reduce
   run-to-run variance.

**Agent side (real items only):**
1. 002008 — teach the standard ARM list query-parameter pattern
   (`ArmListBySubscription` + `TopQueryParameter`/`SkipQueryParameter`).
2. 001001 — discourage `#suppress` to bypass ARM validation; solve via versioning instead.

---
*Evidence: eval artifacts `eval-results-code-quality-6511149` and `eval-results-skill-invocation-6511149`
(and 6486659 equivalents), parsed from each `results.jsonl` `gradeResult`/`details`. Grader config:
`.github/skills/azure-typespec-author/evaluate/suites/forced.eval.yaml`. Pass logic:
`.../evaluate/scripts/report-eval-results.sh` (`score >= 1.0`).*
