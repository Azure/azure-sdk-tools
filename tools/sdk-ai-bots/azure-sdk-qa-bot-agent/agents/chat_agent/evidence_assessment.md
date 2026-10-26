# Evidence assessment

Rubric version: evidence-v3.

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
| **Support** | Supplied evidence establishes the important claims, directly or by sound deduction with all premises supported; claimed current states or completed actions have sufficiently recent observations for the correct target. | Evidence establishes the main conclusion, but a disclosed, evidence-bounded estimate or extrapolation leaves uncertainty in a non-decisive detail; no important claim relies on an unsupported premise. | An important asserted claim lacks evidence, contradicts it, or depends on an unsupported premise; a claimed current state or completed action is unverified. Documentation alone cannot establish live state or completion. |
| **Fit** | The main answer and recommendations apply to the user's stated situation at the level of specificity requested. Supplementary information is appropriately scoped. | The guidance is relevant and useful, but an identifiable applicability gap meaningfully limits how confidently the user can apply part of it. | The main guidance applies to the wrong situation, or a missing condition prevents determining whether the main recommendation is appropriate. |
| **Source quality** | Supplied provenance establishes trustworthy sources with the authority required for each important claim; no relevant provenance or authority limitation remains. | An identified, attributable secondary source meets the minimum authority needed for the guidance, but lack of first-party confirmation leaves a disclosed, non-decisive authority limitation. | An important claim relies on an unreliable or unattributable source, or lacks required authority; secondary guidance cannot substitute for an authorized policy decision or approval. |
| **Consistency** | No relevant disagreement remains after evidence-backed reconciliation of scope, time and authority. Coexisting requirement and approval labels, or reviews of different commits, are not conflicts by themselves. | A disclosed disagreement remains in a separable, non-decisive detail, but all supported interpretations yield the same main diagnosis, recommendation and required next step. | An unresolved disagreement could change an important claim, the main diagnosis, recommendation or required next step; choosing one source without an evidence-backed reason does not resolve the conflict. |
| **Coverage** | The answer resolves the main concern and any explicitly required outcomes. Missing optional details or additional relevant information do not reduce the rating. | The main concern is substantially addressed, but a material requested part remains unresolved and needs follow-up. | The main concern remains unresolved, or a missing essential part prevents the answer from satisfying the request. |
