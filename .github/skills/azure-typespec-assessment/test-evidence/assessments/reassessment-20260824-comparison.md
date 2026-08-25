# Judgment-only Reassessment Comparison — 2026-08-24

This comparison covers the fresh artifact-backed reassessment of PRs 42435,
42853, 43308, 43745, 44200, 44454, 44742, 44882, 44988, 45348, and 45536.
The previous reports are preserved beside the replacements with a `.backup`
suffix.

The new flow uses bounded `model-input.json`, a judgment-only model response,
retained AutoRest evidence for complete operation materialization, and
deterministic JSON/Markdown/HTML rendering and validation.

| PR | Intents | Unique operations | REST findings | Downstream findings | Compliance | Material difference |
| --- | ---: | ---: | ---: | ---: | --- | --- |
| 42435 | 1 → 1 | 1 → 1 | 0 → 0 | 1 → 1 | passed → passed | Same pageable SDK impact; false parameter candidates are explicitly rejected. |
| 42853 | 1 → 2 | 4 → 11 | 0 → 0 | 1 → 1 | failed (2) → failed (1) | Separates stable-version promotion from Go client placement and enumerates seven inherited stable-version operations. The replaced preview is removed correctly; the client customization file finding remains. |
| 43308 | 1 → 1 | 6 → 6 | 0 → 0 | 1 → 1 | passed → passed | Same six-operation Location-based LRO intent and downstream impact; unrelated GET artifact-reference noise is excluded. |
| 43745 | 1 → 1 | 5 → 5 | 0 → 0 | 1 → 1 | passed → passed | Same high SDK-shape impact. Exact source shows a named string-literal union rather than an open `string` arm. |
| 44200 | 2 → 2 | 7 → 15 | 0 → 0 | 1 → 1 | failed (4) → failed (4) | Retains the four material compliance gaps and JavaScript nesting impact while enumerating the newly added private endpoint/private-link operation surface. |
| 44454 | 1 → 1 | 3 → 3 | 0 → 0 | 0 → 0 | failed (1) → failed (1) | Same customization-file compliance gap; confidence is medium because compact evidence omits decorator arguments and operation mappings. |
| 44742 | 1 → 2 | 1 → 2 | 1 → 3 | 1 → 1 | passed → passed | Splits the combined intent by operation and the prior broad REST break into query-value, response-shape, and header-value removals. Each operation now carries only its matching TypeSpec diff. |
| 44882 | 1 → 1 | 35 → 35 | 0 → 0 | 0 → 0 | failed (1) → failed (1) | Material conclusion unchanged; the Go override is explicitly judged compliant while the retained preview version remains the gap. |
| 44988 | 10 → 11 | 66 → 127 | 0 → 0 | 0 → 0 | failed (2) → failed (4) | Separates the optional `afcManagedSync` parameter from the Kubernetes selector-group resource intent, retains 87 inherited operations as API-version availability rather than behavior changes, and removes the false VMSS preservation intent caused by artifact identity/order noise. Exact added source also proves standard child-resource model gaps for AddressPrefixSet and FirewallPolicyKubeSelectorGroup; the two ConnectionAnalyzer gaps remain. |
| 45348 | 1 → 1 | 2 → 2 | 0 → 0 | 0 → 0 | passed → passed | Material conclusion unchanged. Common-type reference-path noise is normalized, reducing the model input from 738,459 bytes to 8,224 bytes. |
| 45536 | 1 → 1 | 5 → 5 | 0 → 0 | 0 → 0 | passed → not-assessed | REST and SDK conclusions are unchanged. Available official evidence does not define a compliance rule for Java-specific enum member names, so a positive compliance conclusion is no longer asserted. |

## Aggregate differences

- Semantic intents increase from 21 to 24.
- Unique affected operations increase primarily because the new assembler
  expands complete stable/new-version operation lineages from retained
  artifacts instead of relying on the narrower historical report inventory.
  For PR 44988, 87 inherited operations are explicitly labeled as new-version
  availability and do not count as wire-behavior modifications.
- REST findings increase from one to three. All three new findings are in PR
  44742, where query-value, response-shape, and request-header-value removals
  are represented separately.
- Downstream finding count remains materially stable. REST breaks also receive
  a deterministic generated-client impact entry when no separate downstream
  candidate exists.
- Compliance ends at five passed, five failed, and one not-assessed. Findings
  are declaration-scoped and use retained authoritative excerpts; prior
  findings are not copied as conclusions.
- All replacement JSON, Markdown, and HTML reports pass the schema-v2 report
  validator.
