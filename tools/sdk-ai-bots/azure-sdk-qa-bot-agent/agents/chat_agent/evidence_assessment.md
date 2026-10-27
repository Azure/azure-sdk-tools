# Evidence assessment

Rubric version: evidence-v4.

Use the normal answering and retrieval workflow, then assess the complete answer in the same final generation.
Follow the configured response schema, keeping the usual answer formatting and references inside `answer`.
Give each aspect one short, single-line sentence as its `reason`, identifying the decisive evidence or remaining gap rather than a reasoning trace.

## Assessment rules

- Assess the evidence gathered for this answer; do not retrieve additional evidence solely for assessment.
- Assess important claims (those that can change the diagnosis, recommendation, next step or expected result) for Support, Fit, Source quality and Consistency, and the full request for Coverage; apply Fail before Limited before Pass without averaging claims.
- Use `not_applicable` only for an irrelevant aspect and explain why; missing evidence is not irrelevance. Use `status: abstained` with `aspects: null` only when assessment cannot be performed, not merely because evidence is weak.

## Aspect criteria

| Aspect | Pass | Limited | Fail |
| --- | --- | --- | --- |
| **Support** | Evidence establishes all important claims, directly or by sound deduction with supported premises; live state or completed actions require recent observations of the correct target. | The main conclusion and all necessary premises are supported; disclosed, evidence-bounded uncertainty is non-decisive. | An important claim lacks support, contradicts evidence or assumes an unverified prerequisite that could change the main recommendation. Caveats do not repair it. Unverified claims of live state or completion Fail; documentation alone cannot verify them. |
| **Fit** | The main answer and recommendations fit the user's situation at the requested specificity; supplementary guidance stays in scope. | The main recommendation applies; a named applicability condition materially limits only a secondary part. | The main guidance targets the wrong situation, or an unknown condition prevents judging whether the main recommendation applies; name that condition, not merely an inaccessible source. |
| **Source quality** | Sources are attributable, trustworthy and authoritative for each important claim type. Missing factual support alone is not an authority failure. | Minimum authority is met, but a disclosed, non-decisive provenance or authority limitation remains. | An important claim relies on an unreliable or unattributable source, or lacks required authority; secondary guidance cannot substitute for an authorized policy decision or approval. |
| **Consistency** | No relevant disagreement remains after evidence-backed reconciliation of scope, time and authority. Required/approved labels and different-commit reviews are not inherently conflicting. | A disclosed, separable, non-decisive disagreement remains; all supported interpretations preserve the main diagnosis, recommendation and next step. | Unresolved disagreement could change an important claim or the main diagnosis, recommendation or next step; choosing a source alone does not resolve it. |
| **Coverage** | All main and explicitly required outcomes are resolved; optional omissions or relevant extras do not lower the rating. | The main outcome is resolved; a material secondary requested part remains unresolved and needs follow-up. | The main requested outcome remains unresolved or an essential requested part is missing; useful background or clarification alone does not resolve the original task. |
