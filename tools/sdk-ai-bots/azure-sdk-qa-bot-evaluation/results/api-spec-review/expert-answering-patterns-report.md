# API Spec Review: expert answering patterns

Tracking: [Azure/azure-sdk-pr#3042](https://github.com/Azure/azure-sdk-pr/issues/3042). This behavioral reanalysis builds on the [historical evaluation](evaluation-report.md), [expert-knowledge audit](expert-knowledge-report.md), and [classified 473-case dataset](../../evaluation_datasets/api-spec-review/README.md). It asks how experts diagnose, clarify, decide, and respond, not merely which API rules they know.

## Summary

All **500 historical threads** were accounted for. Source-grounded screening retained **861 decision-bearing expert exchanges in 384 threads**, including **651 Mike Harder exchanges in 340 threads**. The central transferable pattern is **specific evidence -> decision-changing condition -> narrow supported action**, with an explicit correction when later evidence changes the diagnosis. It is not a rigid tree and not a personality imitation.

Mike's supported examples repeatedly distinguish an optional failure from a merge blocker, a validator defect from a specification defect, a shipping boundary from customer-count arguments, and a helper's advice from an approver's authority. He also asks targeted questions, acknowledges uncertainty, and changes his initial view. Other contributors show the same kinds of behavior; attribution here describes this corpus, not an exclusive trait or a controlled expert comparison.

His retained exchanges contain 1242 distinct expert-message positions, with a median of 28 words per source message. Short verdicts, pointed questions, and focused links often build an answer over several turns. This is not a recommended bot word limit: a single human reply can rely on surrounding context or privileged investigation that a self-contained, read-only bot answer cannot assume.

The tenant change is deliberately small: a compact diagnostic-answering section plus corrections to two overbroad heuristics. It introduces no new tools, tool rounds, routing rules, evidence-contract machinery, global prompt changes, or copied historical exceptions. This report does **not** establish a deployed-bot accuracy improvement.

## Corpus, eligibility, and evidence

| Measure | Count / interpretation |
| --- | --- |
| Export threads | 500; dates span 2023-12-01 through 2026-09-22, not strictly one year |
| Original replies | 3,726 |
| Text-bearing normalized replies | 3,321; 405 empty/non-text replies are not visually interpreted |
| Human messages accounted for | 3,359: 490 roots and 2,869 replies |
| Mike messages in normalized text | 1,379 across 364 threads, including his own questions and coordination |
| Candidate other-human exchanges | 916; a candidate is not automatically a valid expert answer |
| Retained decision exchanges | 861; 651 Mike and 210 other-human |
| Coordination-only candidates | 49; status, pings, approvals, or operational promises without decision guidance |
| Withheld candidate summaries | 6; source did not support the extracted guidance |
| Threads without retained decision evidence | 116; fully accounted for, not silently discarded |

A valid exchange has a preceding human question/context and a substantive contribution by a different human. Authorship is resolved per exchange, not by assuming the thread's root author asked every question. Clarification sequences may interleave; another expert can become the question author in a later exchange. Bots and the question author's own conclusions never count as independent expert evidence. A concise document pointer, informative owner boundary, or decision-changing clarification can qualify without a complete final answer; a bare ping or approval does not. 'Expert' describes the substantive responder role, not a verified authority credential for every contributor. Mike's requested attribution is checked against one consistent exported author identity; those private identity values are not published.

The complete normalized text chronology was analyzed, including the previously evaluation-excluded threads. A second model-assisted pass checked candidate behavior against the cited original question and expert turns, rejected unsupported observations, and coded the retained evidence. Machine checks enforce exact thread/message coverage, human authorship, distinct question/expert authors, chronology, valid category membership, and exact support IDs. Six difficult multi-author/interleaved threads received manual source adjudication: T0076, T0123, T0321, T0333, T0381, T0414. Selected original sequences and withheld records were reviewed directly. These are analyst/model-assisted labels, not independently adjudicated expert gold labels.

T0001-T0500 preserve the previous report's original export order. M0000 is a thread's root; subsequent M positions refer to its nonempty replies sorted by original timestamp and message ID. E identifiers denote this pass's grouped expert exchange, not a new dataset case. Appendix A covers every thread; Appendix B gives the exact anonymous question/expert positions and all candidate dispositions. Raw messages, participant IDs, Teams links, private artifacts, and model intermediates remain local. Supporting QA below is paraphrased; it is not a quotation or a current policy declaration.

Historical links were not revisited to replace old facts with today's PR state. Incident workarounds, group names, migration deadlines, approvals, and tool bugs remain historical. The 405 non-text/empty replies, missing material behind links, and conversations continued elsewhere can contain unavailable context; no visual or external contents were inferred.

## Recurring answering patterns

These are analyst-defined cross-cutting codes grounded in observed moves, not inferred private reasoning. An exchange may carry several codes, so counts overlap. Counts are descriptive corpus frequencies, not performance scores; Mike's larger contribution volume and different question mix prevent interpreting raw counts as superiority. P10 preserves residual behavior rather than forcing an artificial match.

| Code | Observable pattern | Mike exchanges | Other-human exchanges | Transfer boundary |
| --- | --- | ---: | ---: | --- |
| P01 | Diagnose from specific evidence | 133 | 46 | Inspect or refer to exact log, diff, check result, prior attempt, branch history, configuration, baseline, or source to locate the failing layer/root cause. Not just quoting a general rule. |
| P02 | Ask a decision-changing question | 116 | 16 | Actually ask for a missing fact, example, goal, branch, link, prerequisite, or intended scope needed to decide the next action. Do not tag merely because conditions are stated. |
| P03 | Correct the premise or reframe the task | 177 | 47 | Explicitly challenge a mistaken assumption, distinguish symptom from cause, reject the attempted route while preserving the goal, split conflated changes or reviews, or identify the question as belonging elsewhere. |
| P04 | Keep a verdict attached to its conditions | 175 | 68 | Explicit branch/plane/version/state/intent/prerequisite distinction changes what is required or possible. Do not tag merely because the original question has constraints. |
| P05 | Give the narrow next action | 213 | 81 | Concrete bounded instruction, command/config/file edit, minimal remedy or explicit ignore/no-change verdict. Not just linking a broad process, requesting clarification, or announcing an action the responder themselves took. |
| P06 | Identify the owner and authority boundary | 222 | 29 | Differentiate helper from approver; route decision/bug to responsible team, language, repository, or review board; use supported escalation rather than inventing approval or promising privileged action. |
| P07 | Calibrate uncertainty or temporary relief | 147 | 26 | Explicitly hedge a diagnosis, admit missing evidence, separate a provisional workaround from durable resolution, or qualify a historical/transient incident. Not just ordinary if/then policy conditions. |
| P08 | Close or revise the diagnostic loop | 35 | 18 | Actually use a follow-up outcome or new evidence to confirm/alter diagnosis, request rerun after a change, check whether a remedy worked, or clearly separate fixed infrastructure from remaining real errors. |
| P09 | Point to the existing answer or process | 214 | 41 | Use a targeted canonical document/known issue/prior answer/required checklist instead of recreating the whole explanation, including concise supported confirmation of prior advice. |
| P10 | Other observed behavior | 18 | 10 | Use only when none of the nine above covers an explicitly observed move; describe it without forcing a match. |

Only 116/651 retained Mike exchanges (17.8%) are coded as actually asking for a decision-changing fact. This supports selective clarification, not making every answer start with questions. The denominator is grouped decision exchanges; several messages can belong to one exchange, and the annotation is not a causal test.

## Category-specific patterns and supporting QA

The eight categories describe the primary decision in this behavior pass; they are not a relabeling of the earlier 473 evaluation cases. Supporting examples may illustrate an adjacent facet of another category. Counts use one primary category per retained exchange.

| Category | All decision exchanges | Mike | Other-human |
| --- | ---: | ---: | ---: |
| auth | 70 | 60 | 10 |
| spec-review | 138 | 122 | 16 |
| sdk-review | 101 | 40 | 61 |
| review-process | 217 | 179 | 38 |
| versioning | 91 | 75 | 16 |
| authoring | 73 | 41 | 32 |
| repository-workflow | 127 | 100 | 27 |
| documentation | 44 | 34 | 10 |

### auth

Verify the identity and capability that actually failed; distinguish author access, approver eligibility, and UI state. Reuse facts already established instead of restarting the checklist.

**T0016-E02 - Mike Harder (P09, P01).** Question: An approval exists, but the gate is still unsatisfied. Why?

Observed response: Check the actual approver's eligibility; another person's approval is not interchangeable with the required repository/organization access.

Transfer limit: Separate the existence of an approval from whether it satisfies the gate. Do not reuse historical group names as current instructions. Support: question M0005; expert M0006.

Related curated question provenance: [spec-review-0017](../../evaluation_datasets/api-spec-review/review-process.jsonl#L1)

**T0229-E01 - Mike Harder (P01, P03, P04, P02, P05).** Question: The teammate appears to have access but cannot find the approval control.

Observed response: After checking membership and permissions, accept evidence that they look correct, then ask what the teammate sees in the relevant UI and consider the actual reviewer assignment.

Transfer limit: Do not keep repeating an access checklist after evidence resolves it. Support: question M0000; expert M0002, M0003, M0004, M0006.

Related curated question provenance: [spec-review-0198](../../evaluation_datasets/api-spec-review/auth.jsonl#L14)

**T0003-E02 - Another expert (P01, P09).** Question: Reruns started pipelines, but two statuses still never appeared.

Observed response: Use the earlier authorization evidence and the identity of the affected pipelines to narrow the access diagnosis and the next prerequisite to verify.

Transfer limit: The same symptom without authorization evidence does not establish a permission problem. Support: question M0003, M0004; expert M0005, M0006, M0007.

Related curated question provenance: [spec-review-0002](../../evaluation_datasets/api-spec-review/spec-review.jsonl#L2)


### spec-review

Identify the exact diagnostic and whether it is a rule finding, a tool crash, or an inapplicable check. Attach any suppression to its documented condition and revise a mistaken diagnosis explicitly.

**T0001-E01 - Mike Harder (P04, P09).** Question: Avocado reports missing APIs; can the PR be unblocked?

Observed response: Offer the documented suppression only if those APIs are intentionally absent by design.

Transfer limit: Keep the applicability condition attached to the workaround; do not generalize to all Avocado errors. Support: question M0000; expert M0003.

Related curated question provenance: No matching curated case; this conversation is still included in the complete behavior pass.

**T0164-E01 - Mike Harder (P01, P04, P06, P08).** Question: Are breaking-check crashes and lint findings caused by the changed spec or old content?

Observed response: Start from the reported diagnostics, then explicitly revise the initial spec-defect diagnosis when a tool defect is identified; separate that correction from remaining lint work.

Transfer limit: Expert answers can be corrected. Preserve the correction rather than freezing the first diagnosis as truth. Support: question M0000; expert M0002, M0003, M0004, M0005, M0006, M0007.

Related curated question provenance: [spec-review-0147](../../evaluation_datasets/api-spec-review/spec-review.jsonl#L38), [spec-review-0148](../../evaluation_datasets/api-spec-review/spec-review.jsonl#L39)

**T0350-E05 - Another expert (P08).** Question: How was the expensive validator rule identified?

Observed response: Explain the isolating experiment: compare running with the rule removed against running only that rule.

Transfer limit: This supports a diagnostic method, not a current instruction to disable that rule. Support: question M0028; expert M0029.

Related curated question provenance: No matching curated case; this conversation is still included in the complete behavior pass.


### sdk-review

Locate the failing stage before discussing API design: installation, checkout, generation, compilation, package configuration, or compatibility. A retry is an experiment, not a substitute for ownership or diagnosis.

**T0007-E01 - Mike Harder (P01, P05, P07).** Question: SDK validation failed during installation with an API rate-limit error. Is the spec wrong?

Observed response: Locate the failure before actual SDK validation and describe the infrastructure hypothesis with uncertainty.

Transfer limit: The historical retry advice is incident-bound; do not make endless retries a default remedy. Support: question M0000; expert M0002, M0003.

Related curated question provenance: [spec-review-0006](../../evaluation_datasets/api-spec-review/sdk-review.jsonl#L1)

**T0070-E01 - Mike Harder (P07, P01, P08, P06).** Question: An SDK job cannot open its language repository. What next?

Observed response: Try the initial transient-failure hypothesis, notice another affected PR, then route to the relevant owner when rerunning does not fix it.

Transfer limit: A failed retry should update the plan, not trigger a loop of identical advice. Support: question M0000; expert M0002, M0003, M0004.

Related curated question provenance: No matching curated case; this conversation is still included in the complete behavior pass.

**T0123-E03 - Another expert (P03, P05).** Question: Does SDK generation automatically resolve a dependency already present in the specification repository?

Observed response: Correct the dependency-resolution assumption and identify the SDK-side configuration that actually supplies generation dependencies.

Transfer limit: Distinguish package presence in the spec repo from the configuration used by the SDK toolchain. Support: question M0006; expert M0008, M0009.

Related curated question provenance: No matching curated case; this conversation is still included in the complete behavior pass.


### review-process

Answer the requested review stage and actual gate. Distinguish optional failures, required checks, review eligibility, and who has authority to approve an exception.

**T0048-E01 - Mike Harder (P04, P07, P05, P09).** Question: A red documentation-preview check appears to be holding up merge.

Observed response: Say the optional check is not the blocker; identify the missing required checks and the evidence-backed rerun/access actions separately.

Transfer limit: Do not turn every red status into required repair work. Support: question M0000; expert M0002.

Related curated question provenance: [spec-review-0043](../../evaluation_datasets/api-spec-review/repository-workflow.jsonl#L8)

**T0256-E01 - Mike Harder (P02, P03, P04, P07, P05, P06).** Question: Can an approval/access requirement be removed so review can continue?

Observed response: Ask for the concrete PR, distinguish the known security requirement from uncertain historical implementation details, and separate the immediate supported path from a possible longer-term process redesign.

Transfer limit: Do not invent a waiver or claim that a helper can change the security policy. Support: question M0002; expert M0003, M0006, M0009, M0010, M0011.

Related curated question provenance: No matching curated case; this conversation is still included in the complete behavior pass.

**T0002-E01 - Another expert (P03, P01).** Question: Does a suppression-review label require another request?

Observed response: Question whether the label should apply in this scenario at all before sending the user through another review process.

Transfer limit: A label can be an automation symptom; validate its applicability instead of treating it as policy by itself. Support: question M0000, M0002, M0004; expert M0007.

Related curated question provenance: [spec-review-0001](../../evaluation_datasets/api-spec-review/spec-review.jsonl#L1)


### versioning

Use the shipping/branch/version boundary rather than the label 'preview' or a claimed absence of customers. Separate comparison-tool behavior from actual contract changes.

**T0189-E01 - Mike Harder (P09, P03).** Question: An existing preview is feature-gated and has no exposed customers. Is normal review still needed?

Observed response: Retain the ordinary versioning/breaking-review path rather than infer an exemption from the customer's exposure claim.

Transfer limit: The reusable behavior is rejecting an unsupported exception, not asserting one historical policy for all branches today. Support: question M0000; expert M0002.

Related curated question provenance: [spec-review-0163](../../evaluation_datasets/api-spec-review/versioning.jsonl#L27)

**T0240-E03 - Mike Harder (P04, P08, P01, P07).** Question: Are thousands of lint findings real changes or a crash in a comparison input?

Observed response: Explain the comparison inputs, then retract the earlier diagnosis when the prior-version baseline is shown to crash and focus on the tool failure first.

Transfer limit: A large error count is not itself proof of a large set of API defects. Support: question M0012; expert M0013, M0014, M0017, M0019, M0020, M0021.

Related curated question provenance: No matching curated case; this conversation is still included in the complete behavior pass.

**T0225-E02 - Another expert (P04, P10).** Question: Can an already merged version be edited without a version bump because it is still new?

Observed response: Explain the shipping-boundary rationale and route exceptions through the responsible review authority.

Transfer limit: Preserve the branch/lifecycle condition; do not substitute elapsed time or perceived customer impact. Support: question M0000; expert M0003.

Related curated question provenance: [spec-review-0195](../../evaluation_datasets/api-spec-review/versioning.jsonl#L33)


### authoring

Resolve the modeling or workflow distinction that changes the answer; preserve the intended contract and generated-consumer constraints. Do not propose unsupported tooling to make the answer sound complete.

**T0150-E04 - Mike Harder (P02, P06, P10).** Question: Is a naming/folder choice safe across several resource providers?

Observed response: Ask whether the surface is management-plane or data-plane, then route the decision to the appropriate review authority and explain why choosing names early matters.

Transfer limit: A missing plane classification can change ownership; do not prescribe a folder layout first. Support: question M0009; expert M0011.

Related curated question provenance: No matching curated case; this conversation is still included in the complete behavior pass.

**T0335-E01 - Mike Harder (P09, P05, P10).** Question: A legacy version-copy script broke after repository layout changes. What replaces it?

Observed response: State that no supported replacement is established in the conversation and distinguish the immediate manual route from the upcoming TypeSpec workflow change.

Transfer limit: Do not invent a supported automation tool; the historical manual workaround is not current blanket guidance. Support: question M0000; expert M0002.

Related curated question provenance: [spec-review-0316](../../evaluation_datasets/api-spec-review/authoring.jsonl#L29)

**T0055-E02 - Another expert (P08, P05, P01).** Question: An SDK validation problem is tied to the chosen update-operation templates.

Observed response: Recommend a template-specific adjustment, explain its expected generated-model effect, and distinguish that partial workaround from the unresolved underlying issue.

Transfer limit: Keep template/library-version applicability and the outstanding defect separate. Support: question M0000; expert M0004.

Related curated question provenance: [spec-review-0049](../../evaluation_datasets/api-spec-review/sdk-review.jsonl#L5)


### repository-workflow

Use target-branch history and changed scope to separate an inherited repository problem from a new feature defect. Isolate repairs and state which PR or branch each recommendation applies to.

**T0321-E01 - Mike Harder (P01, P02, P04, P05).** Question: A release-branch PR hits duplicate readme inputs. Should its feature changes be rewritten?

Observed response: Check the main-branch baseline, establish that it is already broken, and isolate the readme repair in a dedicated PR without unrelated feature work.

Transfer limit: Inherited failure and introduced regression require different repair scopes. Support: question M0000, M0004; expert M0001, M0003, M0007, M0008, M0009.

Related curated question provenance: [spec-review-0294](../../evaluation_datasets/api-spec-review/spec-review.jsonl#L96)

**T0321-E04 - Mike Harder (P03, P09, P06).** Question: Does advice to fix main mean the original feature PR must also target main?

Observed response: Correct the scope: the readme repair belongs in every affected branch; the feature PR's destination depends on its own release workflow and owner.

Transfer limit: Do not promote a branch recommendation for one repair into a rule for every change. Support: question M0018; expert M0019, M0020, M0021.

Related curated question provenance: [spec-review-0295](../../evaluation_datasets/api-spec-review/repository-workflow.jsonl#L41)

**T0004-E03 - Another expert (P05, P02).** Question: A repository refactor and another spec PR may conflict. Which should merge first?

Observed response: Offer two workable orderings, explain where conflict-resolution help fits, and ask which ordering suits the author.

Transfer limit: When both options are supported, expose the actual tradeoff instead of inventing a mandatory order. Support: question M0007, M0008; expert M0009.

Related curated question provenance: [spec-review-0003](../../evaluation_datasets/api-spec-review/repository-workflow.jsonl#L1)


### documentation

Match the reference to the user's real scope and distinguish maintained guidance from local or unfamiliar pages. Route policy questions to the document owner instead of declaring unknown content obsolete.

**T0224-E02 - Mike Harder (P07, P06).** Question: Is an older review document obsolete?

Observed response: Admit unfamiliarity, distinguish a possibly local document from central policy, and direct the question to its maintainers.

Transfer limit: An expert's uncertainty is not evidence that the document is obsolete. Support: question M0003; expert M0004, M0005.

Related curated question provenance: No matching curated case; this conversation is still included in the complete behavior pass.

**T0131-E02 - Mike Harder (P09, P02).** Question: Where are the end-to-end steps for adding a version?

Observed response: Provide the relevant TypeSpec versioning reference conditionally, then clarify whether the requester needs language-versioning details or the broader workflow.

Transfer limit: Do not mistake a technically related document for the requested process. Support: question M0000; expert M0003.

Related curated question provenance: [spec-review-0121](../../evaluation_datasets/api-spec-review/authoring.jsonl#L9)

**T0402-E03 - Another expert (P01).** Question: The SDK failure message sent the user to the wrong troubleshooting layer.

Observed response: Compare a better language-specific error-message example and identify the concrete message-quality follow-up.

Transfer limit: Improving the diagnostic message does not diagnose or fix the underlying SDK failure. Support: question M0003; expert M0005.

Related curated question provenance: No matching curated case; this conversation is still included in the complete behavior pass.

## Mike-focused worked sequences

### T0108: a seemingly exceptional conversion request

The requester described a new API version and TypeSpec conversion as a compliance/deprecation exercise. In T0108-E01, Mike first separated conversion from version change, then asked what 'retirement' meant: was this the final service version, would SDKs still be generated, or were only some APIs being retired? The follow-up facts changed the apparent exception: the service and SDK work would continue, so he returned to the ordinary two-step migration/version workflow. His answer was not just a memorized conversion rule; it combined a supported default with a focused test of the claimed exception.

In T0108-E02, a question about special breaking-change handling went to a reviewer rather than becoming an invented approval policy. Other experts then discussed the SDK-generation and modeling implications in T0108-E03/E04. **Transfer:** use the user's actual future lifecycle to test whether a special path applies; preserve the existing approval boundary. The old tool-retirement claims and any one-release exception are not embedded in the new prompt.

### T0350: from memory hypothesis to rule isolation and remaining errors

Mike initially considered disk and memory constraints rather than affirming 'large spec' as the proven cause (E01). He separated urgent temporary relief from a sustainable fix (E02), checked earlier-version behavior to show the crash predated the current PR (E03), and narrowed the problem to a validator rule whose traversal consumed excessive memory (E04). Another expert described the with/without-rule isolation experiment (E05). Mike distinguished a likely equivalent tool-side optimization from rewriting a long-standing common model (E07), and after the infrastructure improvement he still identified real validation work and a separate known bug (E10).

**Transfer:** keep hypotheses provisional, compare the baseline, isolate the failing layer, and distinguish 'tool runs again' from 'PR has no errors'. The bot must not copy the human's privileged merge/deploy actions or the historical bypass permission. The status-only deployment promise in E09 is excluded from decision-pattern counts.

### Counterexamples that prevent overfitting

T0224-E02 shows uncertainty and an owner redirect rather than a decisive technical verdict. T0131-E02 shows that a related link is not enough when the user may want a wider workflow. T0229-E01 shows abandoning a repetitive access checklist after the facts look correct. T0164-E01 and T0240-E03 contain revised diagnoses: treating every first expert reply as infallible would train the wrong pattern. Numerous one-line approvals and pings are tracked but deliberately excluded. Not every expert message yields a reusable decision, and not every question requires additional clarification.

## Reusable decision flow, not a mandatory questionnaire

1. Identify the user's goal and the exact current decision: explanation, fix, merge gate, review, versioning, access, or ownership. Use supplied facts; do not restart context gathering.
2. Retrieve the evidence for that decision using the existing shared tool policy. For PRs, distinguish the authoritative required gates from incidental red checks; for failures, locate the actual stage and diagnostic.
3. Resolve only distinctions that change action: branch/shipping state, management versus data plane, prior baseline versus newly introduced error, spec versus SDK/tooling, and the authority needed for an exception.
4. If a decisive fact is unknown, ask for it or give a bounded conditional answer. If it is already established, act on it rather than asking again.
5. Give the narrow supported verdict and next action. Keep an allowed outcome separate from a rejected route, nonblocking status separate from suppression permission, and a temporary unblock separate from a lasting fix.
6. Reassess after a rerun or new evidence. State what changed, correct a wrong diagnosis, and distinguish remaining errors from the issue just fixed. Stop once the requested decision is answered.

This flow adds no requirement for sequential tool calls or more than the shared budget. It does not demand all branches in every answer, a visible evidence contract, or a rigid owner/source-role filter.

## Tenant-prompt application

Runtime path: `config/tenant_config.py` selects `prompts/tenants/api_spec_review.md` for `api-spec-review`; `skills/tenant_skills.py` loads that guideline into the registered inline skill; `agents/chat_agent/init.py` supplies tenant skills alongside the shared chat instructions. This is an inline tenant skill, not a new filesystem `SKILL.md`, so adding frontmatter or a separate skill would not activate the requested behavior.

The prompt is changed only in [the API Spec Review tenant guideline](../../../azure-sdk-qa-bot-agent/prompts/tenants/api_spec_review.md). The new diagnostic section maps evidence localization to P01/P03, selective clarification to P02, scope preservation to P04, narrow next action to P05, authority boundaries to P06, and uncertainty/update behavior to P07/P08. P09 reinforces using the existing specific process instead of recreating generic advice. Existing heuristics are narrowed: non-required checks do not imply suppression authorization; stuck checks do not establish a permission diagnosis without evidence; and concrete code examples require the actual spec format and evidence for the exact change.

The existing specific validation/migration/version guidance is otherwise retained. Shared safety, read-only capability limits, retrieval rules, tool budget, tenant routing, and other tenant prompts are unchanged. No person's name or historical private exception is added to the prompt.

## Development validation and limitations

The existing negative research in [Azure/azure-sdk-tools#17265](https://github.com/Azure/azure-sdk-tools/pull/17265) is a constraint, not evidence of success: more evidence, stricter evidence contracts, and role-bound filtering previously failed quality gates. This change does not enable those prototypes, alter official data/graders, deploy a hosted agent, mutate shared configuration, or expose the 30 reserved research validation questions.

A frozen **composition-only** development screen uses eight paired scenarios (16 inputs) where one decisive fact changes the correct action. Baseline and each candidate use identical synthetic evidence packets, shared instructions, deployment, temperature zero, and one generation per input. The first candidate broadened a shipping-branch review requirement into an unsupported prohibition and still invented a schema-format example; that negative result was retained locally, the prompt was refined, and the same development fixtures were rerun. This iteration is development-set tuning, not independent validation. The packets are supplied as completed KB/wiki tool results; retrieval selection, skill routing, real tool execution, memory, latency, and hosted-bot behavior are not tested. These are newly authored development fixtures, not historical holdouts or current policy sources. They are not added to official evaluation datasets.

Both arms completed all 16 responses on `gpt-5.4`. Median answer length was 110 words at baseline and 111 for the candidate (same word-count regex, including references/code). Length is descriptive, not a correctness criterion or proof of improved usefulness.

| Pair | Fact changed | Required change in answer |
| --- | --- | --- |
| required-gate | A: The complete Next Steps to Merge comment lists only a passing SpecValidation check as required. OptionalAnalysis is explicitly not required. B: The complete Next Steps to Merge comment lists OptionalAnalysis as a required blocker. Its log reports a missing required response schema. | A: Do not make the optional failure a merge blocker or prescribe fixing it. B: Report OptionalAnalysis as blocking and recommend fixing the logged schema issue. |
| failing-layer | A: The log says execution was denied because the contributor lacks repository CI authorization; no specification validation ran. Repository-access prerequisites are maintained by the onboarding process. B: The run was authorized and reached specification validation. Its log identifies a newly added operation with a missing required response schema. | A: Identify access/CI authorization as the failing layer and route to the access prerequisites, not spec edits or suppression. B: Recommend fixing the logged schema issue, not guessing that repository permissions failed. |
| shipping-boundary | A: The target branch is the shipping branch. The retrieved branch policy treats merged APIs there as shipped, independent of the reported customer count, and requires breaking-change review for in-place changes. B: The target branch is a documented development branch. The retrieved branch policy explicitly allows iterative in-place edits there before a separate shipping PR; the current request is only for that development branch. | A: Do not infer an exemption from the customer count; preserve shipping-branch review requirements. B: Allow the documented development-only workflow without importing shipping-branch approval requirements. |
| conversion-sequencing | A: The existing released version has not been converted. The retrieved migration process requires a same-version, contract-preserving conversion PR first, then a separate API-version PR. B: The existing released version has already been converted in an approved, merged same-version PR. The retrieved process now calls for the separate new-version PR. | A: Separate contract-preserving conversion from adding the new version; do not present a combined PR as the supported workflow. B: Proceed with the new-version PR; do not ask for or repeat an already-completed conversion. |
| baseline-applicability | A: The earlier API version is still supported. The retrieved policy requires keeping supported specifications and using normal breaking-change review rather than selecting an arbitrary baseline. B: The earlier API version is confirmed fully deprecated. The retrieved repository policy permits removing fully deprecated specifications in a separate cleanup PR, then rerunning checks against the resulting state. | A: Keep the supported version; do not delete it merely to change the baseline. B: Give the separate permitted cleanup/rerun path, without implying arbitrary baseline selection. |
| introduced-or-inherited | A: The identical validator crash reproduces on the unchanged target branch. The tool owner confirmed a validator defect and is investigating; no schema defect has been established. B: The target-branch baseline passes. The PR introduces a malformed response reference, and the log identifies that exact changed reference as the error. | A: Distinguish tool defect from spec defect; do not invent a schema rewrite or promise to deploy a fix. B: Recommend correcting the changed reference; do not classify it as an established infrastructure incident. |
| approval-authority | A: The retrieved approval rule requires an eligible service owner. The colleague is not an eligible owner. The PR still lists service-owner approval as a blocker. B: The retrieved approval rule requires an eligible service owner. The PR confirms this colleague is an eligible owner, their latest approval is accepted, all listed gates are satisfied, and there are no blocking comments. | A: Explain that this approval does not satisfy the owner gate; point to an eligible owner without inventing an identity or claiming to approve. B: Recognize the owner gate is satisfied; do not ask to repeat an accepted approval. |
| workaround-authority | A: The branch does not require this check. No retrieved source authorizes suppressing the reported rule or grants an override. The immediate goal is only whether it blocks merge. B: The exact diagnostic is Avocado missing APIs, and the missing APIs are intentional by design. Retrieved known-issues guidance explicitly permits a documented suppression for this precise condition. | A: Distinguish a nonblocking check from suppression permission; do not infer an authorized bypass. B: Provide the narrowly documented conditional suppression, without blanket permission for unrelated errors. |

Manual single-reviewer screening found the decisive condition preserved in all 16 baseline answers and all 16 refined-candidate answers. This is no demonstrated accuracy gain. The first candidate's shipping-branch over-prohibition was corrected in the rerun, but the final candidate still supplied unsupported schema-format examples in failing-layer-B and introduced-or-inherited-B despite the new instruction, and appended unnecessary offers in some answers. The median length did not improve. These residual problems prevent treating the prompt as a validated production improvement; the historical analysis and prompt intent are the deliverables, not a positive experiment result.

| Input | Baseline decision condition | Candidate decision condition |
| --- | --- | --- |
| approval-authority-A | Condition preserved: ineligible colleague does not satisfy owner approval. | Condition preserved; no invented approver identity. |
| approval-authority-B | Condition preserved: accepted eligible-owner approval counts. | Condition preserved; no repeated approval request. |
| baseline-applicability-A | Condition preserved: keep the supported version. | Condition preserved: no arbitrary comparison-baseline deletion. |
| baseline-applicability-B | Condition preserved: permitted deprecated-version cleanup and rerun. | Condition preserved: separate cleanup, not arbitrary baseline selection. |
| conversion-sequencing-A | Condition preserved: same-version conversion first. | Condition preserved: separate migration and new-version PRs. |
| conversion-sequencing-B | Condition preserved: proceed after completed conversion. | Condition preserved: no request to redo the conversion. |
| failing-layer-A | Condition preserved: authorization failed before spec validation. | Condition preserved, but appends an unnecessary request for already adequate diagnostic context. |
| failing-layer-B | Condition preserved, but invents format-specific schema examples and extra response guidance. | Condition preserved, but still invents an OpenAPI-format example without the format being supplied; unnecessary follow-up offer remains. |
| introduced-or-inherited-A | Condition preserved: confirmed baseline tool defect is not a demonstrated schema defect. | Condition preserved: no speculative schema rewrite. |
| introduced-or-inherited-B | Condition preserved: correct the exact new malformed reference; adds generic diagnostic guesses. | Condition preserved, but adds an unsupported format-specific example. |
| required-gate-A | Condition preserved: optional red check is not a blocker. | Condition preserved: does not require optional repair. |
| required-gate-B | Condition preserved: listed required failing check blocks merge. | Condition preserved: fix the logged required schema error. |
| shipping-boundary-A | Condition preserved: customer count does not remove review; approval path remains possible. | Condition preserved in refined candidate: new version or reviewed in-place change. First candidate wrongly narrowed this to a prohibition. |
| shipping-boundary-B | Condition preserved: documented development-branch iteration allowed. | Condition preserved: shipping restrictions not imported into development scope. |
| workaround-authority-A | Condition preserved in body, but initial yes-for-merge framing is potentially confusing. | Condition preserved: not verified for suppression, explicitly distinct from nonblocking status. |
| workaround-authority-B | Condition preserved: precise documented Avocado suppression, not generic bypass. | Condition preserved: exact applicability and no invented suppression syntax. |

These narrow fixtures cannot establish an end-to-end gain or regression rate. A deployed-bot comparison with actual retrieval and independently assessed usefulness is still needed; no such deployment or official replay is claimed.

Existing tenant-module checks before the prompt edit yielded 10 passes and one pre-existing installed-framework API incompatibility: `FileSkillsSource.get_skills()` now requires a `context` argument in the unrelated Azure MCP filesystem-skill test. This compatibility issue is not changed by the tenant Markdown edit. After the edit, the two relevant hosted-agent routing/partition checks passed; a direct assertion also confirmed the actual registered `api-spec-review` inline skill's `instructions` contain the complete edited guideline. These checks validate wiring, not model behavior. Filesystem skill lint is not applicable to this registered `InlineSkill` guideline.

## Appendix A: all-thread coverage

All 500 records appear once below. `No retained decision evidence` can mean bot-only answers, the asker's own follow-up, status/coordination, unsupported candidate guidance, or no human response; it does not mean a missing analysis. Candidate-level decisions and exact evidence are in Appendix B.

| Thread | Human messages classified | Candidate exchanges | Retained decisions | Mike decisions | Accounting |
| --- | ---: | ---: | ---: | ---: | --- |
| T0001 | 4 | 1 | 1 | 1 | Retained decision evidence |
| T0002 | 5 | 1 | 1 | 0 | Retained decision evidence |
| T0003 | 8 | 2 | 2 | 0 | Retained decision evidence |
| T0004 | 9 | 3 | 3 | 0 | Retained decision evidence |
| T0005 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0006 | 2 | 1 | 1 | 0 | Retained decision evidence |
| T0007 | 20 | 8 | 6 | 2 | Retained decision evidence |
| T0008 | 6 | 0 | 0 | 0 | No retained decision evidence |
| T0009 | 2 | 0 | 0 | 0 | No retained decision evidence |
| T0010 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0011 | 18 | 7 | 7 | 3 | Retained decision evidence |
| T0012 | 3 | 1 | 1 | 1 | Retained decision evidence |
| T0013 | 3 | 1 | 1 | 1 | Retained decision evidence |
| T0014 | 2 | 0 | 0 | 0 | No retained decision evidence |
| T0015 | 7 | 2 | 2 | 2 | Retained decision evidence |
| T0016 | 6 | 2 | 2 | 2 | Retained decision evidence |
| T0017 | 5 | 1 | 1 | 1 | Retained decision evidence |
| T0018 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0019 | 6 | 1 | 1 | 1 | Retained decision evidence |
| T0020 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0021 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0022 | 5 | 2 | 2 | 2 | Retained decision evidence |
| T0023 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0024 | 21 | 4 | 4 | 4 | Retained decision evidence |
| T0025 | 4 | 1 | 1 | 1 | Retained decision evidence |
| T0026 | 17 | 5 | 5 | 5 | Retained decision evidence |
| T0027 | 20 | 4 | 4 | 4 | Retained decision evidence |
| T0028 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0029 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0030 | 3 | 1 | 1 | 1 | Retained decision evidence |
| T0031 | 4 | 1 | 0 | 0 | No retained decision evidence |
| T0032 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0033 | 4 | 1 | 1 | 1 | Retained decision evidence |
| T0034 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0035 | 3 | 1 | 1 | 1 | Retained decision evidence |
| T0036 | 5 | 2 | 2 | 2 | Retained decision evidence |
| T0037 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0038 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0039 | 5 | 1 | 1 | 0 | Retained decision evidence |
| T0040 | 12 | 5 | 3 | 0 | Retained decision evidence |
| T0041 | 6 | 3 | 3 | 3 | Retained decision evidence |
| T0042 | 2 | 1 | 0 | 0 | No retained decision evidence |
| T0043 | 13 | 6 | 6 | 6 | Retained decision evidence |
| T0044 | 7 | 1 | 1 | 1 | Retained decision evidence |
| T0045 | 3 | 1 | 1 | 1 | Retained decision evidence |
| T0046 | 9 | 3 | 3 | 2 | Retained decision evidence |
| T0047 | 4 | 1 | 1 | 1 | Retained decision evidence |
| T0048 | 3 | 1 | 1 | 1 | Retained decision evidence |
| T0049 | 10 | 3 | 3 | 0 | Retained decision evidence |
| T0050 | 9 | 2 | 2 | 2 | Retained decision evidence |
| T0051 | 4 | 2 | 2 | 2 | Retained decision evidence |
| T0052 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0053 | 2 | 0 | 0 | 0 | No retained decision evidence |
| T0054 | 2 | 0 | 0 | 0 | No retained decision evidence |
| T0055 | 5 | 2 | 2 | 1 | Retained decision evidence |
| T0056 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0057 | 5 | 3 | 3 | 3 | Retained decision evidence |
| T0058 | 6 | 3 | 3 | 2 | Retained decision evidence |
| T0059 | 5 | 2 | 2 | 2 | Retained decision evidence |
| T0060 | 4 | 1 | 1 | 0 | Retained decision evidence |
| T0061 | 4 | 2 | 2 | 2 | Retained decision evidence |
| T0062 | 2 | 1 | 0 | 0 | No retained decision evidence |
| T0063 | 2 | 0 | 0 | 0 | No retained decision evidence |
| T0064 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0065 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0066 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0067 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0068 | 19 | 7 | 6 | 3 | Retained decision evidence |
| T0069 | 5 | 2 | 2 | 2 | Retained decision evidence |
| T0070 | 19 | 3 | 2 | 1 | Retained decision evidence |
| T0071 | 9 | 2 | 2 | 2 | Retained decision evidence |
| T0072 | 5 | 1 | 1 | 1 | Retained decision evidence |
| T0073 | 10 | 3 | 3 | 1 | Retained decision evidence |
| T0074 | 2 | 1 | 0 | 0 | No retained decision evidence |
| T0075 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0076 | 13 | 2 | 2 | 1 | Retained decision evidence |
| T0077 | 7 | 2 | 2 | 2 | Retained decision evidence |
| T0078 | 5 | 1 | 1 | 1 | Retained decision evidence |
| T0079 | 13 | 3 | 3 | 3 | Retained decision evidence |
| T0080 | 3 | 2 | 2 | 1 | Retained decision evidence |
| T0081 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0082 | 2 | 0 | 0 | 0 | No retained decision evidence |
| T0083 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0084 | 6 | 2 | 1 | 0 | Retained decision evidence |
| T0085 | 4 | 1 | 1 | 1 | Retained decision evidence |
| T0086 | 4 | 1 | 1 | 1 | Retained decision evidence |
| T0087 | 7 | 2 | 2 | 2 | Retained decision evidence |
| T0088 | 3 | 1 | 1 | 1 | Retained decision evidence |
| T0089 | 13 | 2 | 2 | 2 | Retained decision evidence |
| T0090 | 2 | 0 | 0 | 0 | No retained decision evidence |
| T0091 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0092 | 6 | 3 | 3 | 3 | Retained decision evidence |
| T0093 | 8 | 3 | 3 | 3 | Retained decision evidence |
| T0094 | 3 | 1 | 1 | 1 | Retained decision evidence |
| T0095 | 4 | 1 | 1 | 1 | Retained decision evidence |
| T0096 | 10 | 5 | 2 | 0 | Retained decision evidence |
| T0097 | 8 | 4 | 4 | 1 | Retained decision evidence |
| T0098 | 2 | 0 | 0 | 0 | No retained decision evidence |
| T0099 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0100 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0101 | 3 | 0 | 0 | 0 | No retained decision evidence |
| T0102 | 5 | 2 | 2 | 1 | Retained decision evidence |
| T0103 | 4 | 1 | 1 | 1 | Retained decision evidence |
| T0104 | 2 | 1 | 0 | 0 | No retained decision evidence |
| T0105 | 4 | 1 | 1 | 1 | Retained decision evidence |
| T0106 | 3 | 1 | 1 | 1 | Retained decision evidence |
| T0107 | 12 | 4 | 4 | 0 | Retained decision evidence |
| T0108 | 29 | 4 | 4 | 2 | Retained decision evidence |
| T0109 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0110 | 6 | 2 | 2 | 2 | Retained decision evidence |
| T0111 | 2 | 1 | 0 | 0 | No retained decision evidence |
| T0112 | 23 | 8 | 8 | 3 | Retained decision evidence |
| T0113 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0114 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0115 | 9 | 2 | 2 | 1 | Retained decision evidence |
| T0116 | 14 | 7 | 7 | 2 | Retained decision evidence |
| T0117 | 2 | 1 | 0 | 0 | No retained decision evidence |
| T0118 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0119 | 5 | 2 | 2 | 2 | Retained decision evidence |
| T0120 | 5 | 1 | 1 | 1 | Retained decision evidence |
| T0121 | 3 | 1 | 0 | 0 | No retained decision evidence |
| T0122 | 14 | 2 | 2 | 2 | Retained decision evidence |
| T0123 | 10 | 4 | 4 | 0 | Retained decision evidence |
| T0124 | 6 | 2 | 2 | 0 | Retained decision evidence |
| T0125 | 7 | 2 | 2 | 0 | Retained decision evidence |
| T0126 | 3 | 1 | 0 | 0 | No retained decision evidence |
| T0127 | 16 | 1 | 1 | 0 | Retained decision evidence |
| T0128 | 2 | 1 | 1 | 0 | Retained decision evidence |
| T0129 | 2 | 0 | 0 | 0 | No retained decision evidence |
| T0130 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0131 | 10 | 4 | 4 | 4 | Retained decision evidence |
| T0132 | 7 | 3 | 3 | 1 | Retained decision evidence |
| T0133 | 6 | 2 | 2 | 0 | Retained decision evidence |
| T0134 | 16 | 6 | 4 | 1 | Retained decision evidence |
| T0135 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0136 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0137 | 8 | 2 | 1 | 0 | Retained decision evidence |
| T0138 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0139 | 4 | 0 | 0 | 0 | No retained decision evidence |
| T0140 | 5 | 1 | 1 | 0 | Retained decision evidence |
| T0141 | 17 | 5 | 4 | 1 | Retained decision evidence |
| T0142 | 9 | 3 | 1 | 0 | Retained decision evidence |
| T0143 | 4 | 1 | 1 | 1 | Retained decision evidence |
| T0144 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0145 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0146 | 3 | 1 | 1 | 0 | Retained decision evidence |
| T0147 | 6 | 2 | 2 | 0 | Retained decision evidence |
| T0148 | 2 | 1 | 0 | 0 | No retained decision evidence |
| T0149 | 3 | 1 | 1 | 1 | Retained decision evidence |
| T0150 | 16 | 5 | 5 | 5 | Retained decision evidence |
| T0151 | 3 | 1 | 0 | 0 | No retained decision evidence |
| T0152 | 5 | 3 | 3 | 2 | Retained decision evidence |
| T0153 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0154 | 6 | 3 | 3 | 3 | Retained decision evidence |
| T0155 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0156 | 22 | 6 | 6 | 3 | Retained decision evidence |
| T0157 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0158 | 2 | 0 | 0 | 0 | No retained decision evidence |
| T0159 | 2 | 0 | 0 | 0 | No retained decision evidence |
| T0160 | 2 | 1 | 0 | 0 | No retained decision evidence |
| T0161 | 3 | 1 | 1 | 0 | Retained decision evidence |
| T0162 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0163 | 2 | 0 | 0 | 0 | No retained decision evidence |
| T0164 | 16 | 4 | 4 | 4 | Retained decision evidence |
| T0165 | 10 | 1 | 1 | 1 | Retained decision evidence |
| T0166 | 3 | 1 | 1 | 1 | Retained decision evidence |
| T0167 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0168 | 2 | 0 | 0 | 0 | No retained decision evidence |
| T0169 | 4 | 1 | 0 | 0 | No retained decision evidence |
| T0170 | 6 | 2 | 2 | 2 | Retained decision evidence |
| T0171 | 9 | 3 | 3 | 1 | Retained decision evidence |
| T0172 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0173 | 2 | 0 | 0 | 0 | No retained decision evidence |
| T0174 | 0 | 0 | 0 | 0 | No retained decision evidence |
| T0175 | 0 | 0 | 0 | 0 | No retained decision evidence |
| T0176 | 0 | 0 | 0 | 0 | No retained decision evidence |
| T0177 | 0 | 0 | 0 | 0 | No retained decision evidence |
| T0178 | 0 | 0 | 0 | 0 | No retained decision evidence |
| T0179 | 0 | 0 | 0 | 0 | No retained decision evidence |
| T0180 | 0 | 0 | 0 | 0 | No retained decision evidence |
| T0181 | 10 | 2 | 2 | 2 | Retained decision evidence |
| T0182 | 4 | 2 | 2 | 2 | Retained decision evidence |
| T0183 | 2 | 0 | 0 | 0 | No retained decision evidence |
| T0184 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0185 | 5 | 2 | 2 | 2 | Retained decision evidence |
| T0186 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0187 | 12 | 2 | 2 | 2 | Retained decision evidence |
| T0188 | 10 | 4 | 4 | 4 | Retained decision evidence |
| T0189 | 3 | 1 | 1 | 1 | Retained decision evidence |
| T0190 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0191 | 4 | 1 | 1 | 1 | Retained decision evidence |
| T0192 | 2 | 1 | 1 | 0 | Retained decision evidence |
| T0193 | 8 | 2 | 2 | 2 | Retained decision evidence |
| T0194 | 15 | 3 | 3 | 0 | Retained decision evidence |
| T0195 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0196 | 24 | 8 | 8 | 8 | Retained decision evidence |
| T0197 | 3 | 0 | 0 | 0 | No retained decision evidence |
| T0198 | 4 | 2 | 2 | 2 | Retained decision evidence |
| T0199 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0200 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0201 | 3 | 1 | 1 | 1 | Retained decision evidence |
| T0202 | 3 | 2 | 2 | 2 | Retained decision evidence |
| T0203 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0204 | 26 | 1 | 1 | 1 | Retained decision evidence |
| T0205 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0206 | 2 | 0 | 0 | 0 | No retained decision evidence |
| T0207 | 28 | 10 | 8 | 8 | Retained decision evidence |
| T0208 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0209 | 4 | 1 | 1 | 1 | Retained decision evidence |
| T0210 | 4 | 1 | 1 | 1 | Retained decision evidence |
| T0211 | 4 | 1 | 1 | 1 | Retained decision evidence |
| T0212 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0213 | 8 | 1 | 1 | 0 | Retained decision evidence |
| T0214 | 5 | 2 | 1 | 0 | Retained decision evidence |
| T0215 | 0 | 0 | 0 | 0 | No retained decision evidence |
| T0216 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0217 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0218 | 4 | 1 | 1 | 1 | Retained decision evidence |
| T0219 | 4 | 1 | 1 | 1 | Retained decision evidence |
| T0220 | 5 | 2 | 2 | 2 | Retained decision evidence |
| T0221 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0222 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0223 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0224 | 16 | 4 | 4 | 4 | Retained decision evidence |
| T0225 | 5 | 3 | 2 | 1 | Retained decision evidence |
| T0226 | 5 | 1 | 1 | 0 | Retained decision evidence |
| T0227 | 2 | 0 | 0 | 0 | No retained decision evidence |
| T0228 | 3 | 1 | 1 | 1 | Retained decision evidence |
| T0229 | 8 | 2 | 2 | 2 | Retained decision evidence |
| T0230 | 5 | 2 | 1 | 0 | Retained decision evidence |
| T0231 | 5 | 2 | 1 | 1 | Retained decision evidence |
| T0232 | 3 | 1 | 1 | 1 | Retained decision evidence |
| T0233 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0234 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0235 | 3 | 1 | 1 | 1 | Retained decision evidence |
| T0236 | 4 | 1 | 1 | 1 | Retained decision evidence |
| T0237 | 5 | 1 | 1 | 1 | Retained decision evidence |
| T0238 | 7 | 1 | 1 | 1 | Retained decision evidence |
| T0239 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0240 | 23 | 5 | 4 | 3 | Retained decision evidence |
| T0241 | 4 | 2 | 2 | 2 | Retained decision evidence |
| T0242 | 3 | 2 | 2 | 1 | Retained decision evidence |
| T0243 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0244 | 5 | 0 | 0 | 0 | No retained decision evidence |
| T0245 | 18 | 6 | 6 | 4 | Retained decision evidence |
| T0246 | 5 | 1 | 1 | 1 | Retained decision evidence |
| T0247 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0248 | 5 | 2 | 2 | 2 | Retained decision evidence |
| T0249 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0250 | 8 | 2 | 1 | 1 | Retained decision evidence |
| T0251 | 13 | 3 | 3 | 1 | Retained decision evidence |
| T0252 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0253 | 6 | 1 | 1 | 1 | Retained decision evidence |
| T0254 | 5 | 1 | 1 | 1 | Retained decision evidence |
| T0255 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0256 | 12 | 1 | 1 | 1 | Retained decision evidence |
| T0257 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0258 | 18 | 5 | 5 | 5 | Retained decision evidence |
| T0259 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0260 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0261 | 12 | 3 | 3 | 3 | Retained decision evidence |
| T0262 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0263 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0264 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0265 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0266 | 3 | 1 | 1 | 1 | Retained decision evidence |
| T0267 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0268 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0269 | 3 | 1 | 1 | 1 | Retained decision evidence |
| T0270 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0271 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0272 | 15 | 3 | 3 | 2 | Retained decision evidence |
| T0273 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0274 | 4 | 1 | 1 | 1 | Retained decision evidence |
| T0275 | 3 | 1 | 1 | 1 | Retained decision evidence |
| T0276 | 8 | 1 | 1 | 1 | Retained decision evidence |
| T0277 | 6 | 1 | 1 | 1 | Retained decision evidence |
| T0278 | 3 | 1 | 1 | 1 | Retained decision evidence |
| T0279 | 19 | 9 | 9 | 9 | Retained decision evidence |
| T0280 | 5 | 1 | 1 | 1 | Retained decision evidence |
| T0281 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0282 | 13 | 3 | 3 | 3 | Retained decision evidence |
| T0283 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0284 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0285 | 2 | 0 | 0 | 0 | No retained decision evidence |
| T0286 | 4 | 2 | 1 | 0 | Retained decision evidence |
| T0287 | 3 | 1 | 1 | 1 | Retained decision evidence |
| T0288 | 5 | 2 | 2 | 2 | Retained decision evidence |
| T0289 | 4 | 1 | 1 | 1 | Retained decision evidence |
| T0290 | 50 | 14 | 14 | 12 | Retained decision evidence |
| T0291 | 6 | 2 | 2 | 1 | Retained decision evidence |
| T0292 | 5 | 2 | 2 | 1 | Retained decision evidence |
| T0293 | 11 | 4 | 4 | 2 | Retained decision evidence |
| T0294 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0295 | 3 | 0 | 0 | 0 | No retained decision evidence |
| T0296 | 3 | 1 | 1 | 1 | Retained decision evidence |
| T0297 | 2 | 0 | 0 | 0 | No retained decision evidence |
| T0298 | 4 | 1 | 1 | 1 | Retained decision evidence |
| T0299 | 7 | 1 | 1 | 1 | Retained decision evidence |
| T0300 | 12 | 4 | 4 | 2 | Retained decision evidence |
| T0301 | 4 | 1 | 1 | 1 | Retained decision evidence |
| T0302 | 22 | 5 | 5 | 4 | Retained decision evidence |
| T0303 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0304 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0305 | 9 | 4 | 4 | 4 | Retained decision evidence |
| T0306 | 6 | 2 | 2 | 2 | Retained decision evidence |
| T0307 | 5 | 2 | 2 | 2 | Retained decision evidence |
| T0308 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0309 | 3 | 1 | 1 | 0 | Retained decision evidence |
| T0310 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0311 | 48 | 16 | 16 | 10 | Retained decision evidence |
| T0312 | 10 | 1 | 1 | 1 | Retained decision evidence |
| T0313 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0314 | 6 | 2 | 2 | 0 | Retained decision evidence |
| T0315 | 36 | 8 | 7 | 0 | Retained decision evidence |
| T0316 | 3 | 1 | 1 | 1 | Retained decision evidence |
| T0317 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0318 | 13 | 4 | 4 | 3 | Retained decision evidence |
| T0319 | 7 | 1 | 1 | 1 | Retained decision evidence |
| T0320 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0321 | 51 | 14 | 13 | 13 | Retained decision evidence |
| T0322 | 27 | 4 | 4 | 4 | Retained decision evidence |
| T0323 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0324 | 7 | 2 | 2 | 1 | Retained decision evidence |
| T0325 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0326 | 7 | 2 | 2 | 2 | Retained decision evidence |
| T0327 | 3 | 1 | 1 | 1 | Retained decision evidence |
| T0328 | 3 | 1 | 1 | 1 | Retained decision evidence |
| T0329 | 2 | 1 | 0 | 0 | No retained decision evidence |
| T0330 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0331 | 8 | 1 | 1 | 1 | Retained decision evidence |
| T0332 | 29 | 9 | 9 | 0 | Retained decision evidence |
| T0333 | 51 | 12 | 10 | 0 | Retained decision evidence |
| T0334 | 9 | 4 | 4 | 2 | Retained decision evidence |
| T0335 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0336 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0337 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0338 | 3 | 1 | 1 | 1 | Retained decision evidence |
| T0339 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0340 | 8 | 1 | 1 | 1 | Retained decision evidence |
| T0341 | 4 | 1 | 1 | 1 | Retained decision evidence |
| T0342 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0343 | 5 | 1 | 1 | 1 | Retained decision evidence |
| T0344 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0345 | 2 | 1 | 1 | 0 | Retained decision evidence |
| T0346 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0347 | 11 | 3 | 3 | 3 | Retained decision evidence |
| T0348 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0349 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0350 | 51 | 10 | 9 | 8 | Retained decision evidence |
| T0351 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0352 | 10 | 1 | 1 | 1 | Retained decision evidence |
| T0353 | 12 | 3 | 3 | 3 | Retained decision evidence |
| T0354 | 6 | 2 | 2 | 2 | Retained decision evidence |
| T0355 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0356 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0357 | 24 | 4 | 4 | 4 | Retained decision evidence |
| T0358 | 11 | 2 | 2 | 2 | Retained decision evidence |
| T0359 | 19 | 3 | 3 | 2 | Retained decision evidence |
| T0360 | 4 | 1 | 1 | 1 | Retained decision evidence |
| T0361 | 32 | 5 | 5 | 5 | Retained decision evidence |
| T0362 | 45 | 9 | 9 | 9 | Retained decision evidence |
| T0363 | 8 | 3 | 3 | 3 | Retained decision evidence |
| T0364 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0365 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0366 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0367 | 3 | 1 | 1 | 1 | Retained decision evidence |
| T0368 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0369 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0370 | 6 | 1 | 1 | 1 | Retained decision evidence |
| T0371 | 3 | 1 | 1 | 1 | Retained decision evidence |
| T0372 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0373 | 7 | 3 | 3 | 2 | Retained decision evidence |
| T0374 | 10 | 5 | 5 | 2 | Retained decision evidence |
| T0375 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0376 | 27 | 8 | 8 | 6 | Retained decision evidence |
| T0377 | 3 | 1 | 0 | 0 | No retained decision evidence |
| T0378 | 48 | 8 | 7 | 7 | Retained decision evidence |
| T0379 | 9 | 3 | 3 | 3 | Retained decision evidence |
| T0380 | 11 | 2 | 2 | 2 | Retained decision evidence |
| T0381 | 25 | 5 | 5 | 3 | Retained decision evidence |
| T0382 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0383 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0384 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0385 | 3 | 1 | 1 | 1 | Retained decision evidence |
| T0386 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0387 | 2 | 1 | 1 | 0 | Retained decision evidence |
| T0388 | 19 | 3 | 3 | 3 | Retained decision evidence |
| T0389 | 3 | 1 | 1 | 1 | Retained decision evidence |
| T0390 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0391 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0392 | 22 | 3 | 3 | 3 | Retained decision evidence |
| T0393 | 7 | 2 | 2 | 2 | Retained decision evidence |
| T0394 | 28 | 5 | 5 | 5 | Retained decision evidence |
| T0395 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0396 | 4 | 1 | 1 | 1 | Retained decision evidence |
| T0397 | 10 | 4 | 4 | 4 | Retained decision evidence |
| T0398 | 6 | 1 | 1 | 1 | Retained decision evidence |
| T0399 | 2 | 1 | 0 | 0 | No retained decision evidence |
| T0400 | 11 | 4 | 4 | 4 | Retained decision evidence |
| T0401 | 4 | 2 | 2 | 2 | Retained decision evidence |
| T0402 | 8 | 4 | 4 | 2 | Retained decision evidence |
| T0403 | 9 | 3 | 3 | 3 | Retained decision evidence |
| T0404 | 3 | 1 | 1 | 0 | Retained decision evidence |
| T0405 | 25 | 7 | 6 | 4 | Retained decision evidence |
| T0406 | 12 | 3 | 3 | 3 | Retained decision evidence |
| T0407 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0408 | 4 | 1 | 1 | 1 | Retained decision evidence |
| T0409 | 16 | 8 | 7 | 3 | Retained decision evidence |
| T0410 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0411 | 3 | 1 | 1 | 0 | Retained decision evidence |
| T0412 | 9 | 1 | 1 | 1 | Retained decision evidence |
| T0413 | 3 | 2 | 2 | 1 | Retained decision evidence |
| T0414 | 37 | 7 | 7 | 4 | Retained decision evidence |
| T0415 | 20 | 3 | 3 | 3 | Retained decision evidence |
| T0416 | 22 | 2 | 2 | 2 | Retained decision evidence |
| T0417 | 3 | 1 | 1 | 0 | Retained decision evidence |
| T0418 | 8 | 3 | 3 | 1 | Retained decision evidence |
| T0419 | 9 | 1 | 1 | 1 | Retained decision evidence |
| T0420 | 5 | 1 | 1 | 1 | Retained decision evidence |
| T0421 | 6 | 3 | 3 | 3 | Retained decision evidence |
| T0422 | 3 | 1 | 1 | 1 | Retained decision evidence |
| T0423 | 3 | 2 | 2 | 1 | Retained decision evidence |
| T0424 | 11 | 3 | 3 | 2 | Retained decision evidence |
| T0425 | 4 | 1 | 1 | 1 | Retained decision evidence |
| T0426 | 34 | 3 | 3 | 3 | Retained decision evidence |
| T0427 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0428 | 14 | 4 | 4 | 3 | Retained decision evidence |
| T0429 | 21 | 3 | 3 | 2 | Retained decision evidence |
| T0430 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0431 | 1 | 0 | 0 | 0 | No retained decision evidence |
| T0432 | 3 | 1 | 1 | 0 | Retained decision evidence |
| T0433 | 7 | 1 | 1 | 1 | Retained decision evidence |
| T0434 | 15 | 5 | 5 | 5 | Retained decision evidence |
| T0435 | 5 | 1 | 1 | 1 | Retained decision evidence |
| T0436 | 4 | 1 | 0 | 0 | No retained decision evidence |
| T0437 | 6 | 1 | 1 | 1 | Retained decision evidence |
| T0438 | 4 | 2 | 2 | 2 | Retained decision evidence |
| T0439 | 4 | 1 | 1 | 1 | Retained decision evidence |
| T0440 | 3 | 1 | 1 | 1 | Retained decision evidence |
| T0441 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0442 | 10 | 1 | 1 | 1 | Retained decision evidence |
| T0443 | 7 | 3 | 3 | 3 | Retained decision evidence |
| T0444 | 3 | 1 | 1 | 1 | Retained decision evidence |
| T0445 | 4 | 0 | 0 | 0 | No retained decision evidence |
| T0446 | 5 | 2 | 2 | 1 | Retained decision evidence |
| T0447 | 5 | 2 | 2 | 2 | Retained decision evidence |
| T0448 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0449 | 6 | 2 | 2 | 2 | Retained decision evidence |
| T0450 | 3 | 1 | 1 | 1 | Retained decision evidence |
| T0451 | 10 | 2 | 2 | 2 | Retained decision evidence |
| T0452 | 2 | 1 | 1 | 0 | Retained decision evidence |
| T0453 | 4 | 1 | 1 | 1 | Retained decision evidence |
| T0454 | 6 | 2 | 2 | 2 | Retained decision evidence |
| T0455 | 6 | 1 | 1 | 1 | Retained decision evidence |
| T0456 | 3 | 1 | 1 | 1 | Retained decision evidence |
| T0457 | 28 | 10 | 9 | 9 | Retained decision evidence |
| T0458 | 17 | 7 | 7 | 2 | Retained decision evidence |
| T0459 | 11 | 3 | 3 | 3 | Retained decision evidence |
| T0460 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0461 | 3 | 1 | 1 | 1 | Retained decision evidence |
| T0462 | 6 | 2 | 2 | 2 | Retained decision evidence |
| T0463 | 10 | 4 | 4 | 2 | Retained decision evidence |
| T0464 | 16 | 6 | 6 | 6 | Retained decision evidence |
| T0465 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0466 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0467 | 3 | 0 | 0 | 0 | No retained decision evidence |
| T0468 | 3 | 2 | 2 | 0 | Retained decision evidence |
| T0469 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0470 | 2 | 0 | 0 | 0 | No retained decision evidence |
| T0471 | 16 | 4 | 4 | 4 | Retained decision evidence |
| T0472 | 2 | 1 | 0 | 0 | No retained decision evidence |
| T0473 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0474 | 3 | 2 | 2 | 1 | Retained decision evidence |
| T0475 | 22 | 6 | 6 | 5 | Retained decision evidence |
| T0476 | 9 | 3 | 3 | 3 | Retained decision evidence |
| T0477 | 5 | 2 | 1 | 1 | Retained decision evidence |
| T0478 | 6 | 2 | 2 | 0 | Retained decision evidence |
| T0479 | 0 | 0 | 0 | 0 | No retained decision evidence |
| T0480 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0481 | 6 | 1 | 1 | 1 | Retained decision evidence |
| T0482 | 15 | 2 | 2 | 2 | Retained decision evidence |
| T0483 | 2 | 1 | 0 | 0 | No retained decision evidence |
| T0484 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0485 | 18 | 3 | 3 | 3 | Retained decision evidence |
| T0486 | 31 | 5 | 5 | 5 | Retained decision evidence |
| T0487 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0488 | 2 | 1 | 1 | 1 | Retained decision evidence |
| T0489 | 5 | 1 | 1 | 1 | Retained decision evidence |
| T0490 | 12 | 2 | 2 | 2 | Retained decision evidence |
| T0491 | 4 | 1 | 1 | 1 | Retained decision evidence |
| T0492 | 10 | 5 | 5 | 5 | Retained decision evidence |
| T0493 | 13 | 5 | 4 | 2 | Retained decision evidence |
| T0494 | 4 | 1 | 1 | 1 | Retained decision evidence |
| T0495 | 3 | 1 | 1 | 1 | Retained decision evidence |
| T0496 | 3 | 1 | 1 | 1 | Retained decision evidence |
| T0497 | 3 | 2 | 2 | 1 | Retained decision evidence |
| T0498 | 3 | 1 | 1 | 1 | Retained decision evidence |
| T0499 | 15 | 2 | 2 | 1 | Retained decision evidence |
| T0500 | 3 | 2 | 2 | 1 | Retained decision evidence |

## Appendix B: complete candidate-exchange provenance

Q and E are normalized source-message positions, not quotations. The retained/coordination/withheld classification is independent of whether a historical response happened to be correct. Policy authority must still be checked at answer time. Full raw transcripts and per-message semantic labels are intentionally not published.

| Exchange | Contributor | Primary topic | Q positions | Expert positions | Disposition | Behavior codes |
| --- | --- | --- | --- | --- | --- | --- |
| T0001-E01 | Mike | spec-review | M0000 | M0003 | Retained | P04, P09 |
| T0002-E01 | Other human | review-process | M0000, M0002, M0004 | M0007 | Retained | P03, P01 |
| T0003-E01 | Other human | repository-workflow | M0000 | M0002 | Retained | P01, P05 |
| T0003-E02 | Other human | auth | M0003, M0004 | M0005, M0006, M0007 | Retained | P01, P09 |
| T0004-E01 | Other human | authoring | M0000 | M0003 | Retained | P02, P09 |
| T0004-E02 | Other human | authoring | M0005 | M0006 | Retained | P03 |
| T0004-E03 | Other human | repository-workflow | M0007, M0008 | M0009 | Retained | P05, P02 |
| T0006-E01 | Other human | versioning | M0000 | M0002 | Retained | P09 |
| T0007-E01 | Mike | sdk-review | M0000 | M0002, M0003 | Retained | P01, P05, P07 |
| T0007-E02 | Mike | sdk-review | M0004 | M0005 | Coordination only | - |
| T0007-E03 | Mike | review-process | M0006 | M0008 | Retained | P04 |
| T0007-E04 | Other human | review-process | M0006 | M0007 | Coordination only | - |
| T0007-E05 | Other human | sdk-review | M0009 | M0011 | Retained | P03, P05 |
| T0007-E06 | Other human | sdk-review | M0011 | M0012 | Retained | P03 |
| T0007-E07 | Other human | sdk-review | M0013 | M0014 | Retained | P04, P05 |
| T0007-E08 | Other human | sdk-review | M0015 | M0016 | Retained | P01, P05, P07 |
| T0011-E01 | Mike | review-process | M0000, M0003 | M0002, M0004 | Retained | P06, P02 |
| T0011-E02 | Other human | versioning | M0000 | M0006, M0007 | Retained | P03, P04 |
| T0011-E03 | Other human | repository-workflow | M0008 | M0009, M0011, M0012 | Retained | P03, P05, P04 |
| T0011-E04 | Mike | versioning | M0008 | M0010 | Retained | P03, P04 |
| T0011-E05 | Other human | review-process | M0013 | M0014 | Retained | P09 |
| T0011-E06 | Other human | review-process | M0015 | M0016 | Retained | P06 |
| T0011-E07 | Mike | review-process | M0017 | M0018 | Retained | P06 |
| T0012-E01 | Mike | versioning | M0000, M0002 | M0004 | Retained | P09, P05 |
| T0013-E01 | Mike | auth | M0000 | M0002, M0003 | Retained | P03, P02, P09, P01 |
| T0015-E01 | Mike | repository-workflow | M0000, M0002 | M0006 | Retained | P04, P09, P05 |
| T0015-E02 | Mike | review-process | M0004 | M0007, M0008, M0009 | Retained | P07, P01, P06 |
| T0016-E01 | Mike | review-process | M0000 | M0002, M0003, M0004 | Retained | P06, P09, P01 |
| T0016-E02 | Mike | auth | M0005 | M0006 | Retained | P09, P01 |
| T0017-E01 | Mike | sdk-review | M0000 | M0002, M0003, M0004 | Retained | P01, P02, P05, P04 |
| T0019-E01 | Mike | versioning | M0000 | M0002, M0003, M0004, M0005, M0006 | Retained | P03, P04, P07, P06 |
| T0020-E01 | Mike | review-process | M0000 | M0002 | Retained | P04, P05, P09 |
| T0021-E01 | Mike | auth | M0000 | M0002 | Retained | P06 |
| T0022-E01 | Mike | auth | M0000 | M0001 | Retained | P06, P05 |
| T0022-E02 | Mike | auth | M0002 | M0003, M0004 | Retained | P02, P09 |
| T0024-E01 | Mike | repository-workflow | M0000 | M0002, M0003, M0004, M0005 | Retained | P06, P04, P07, P05 |
| T0024-E02 | Mike | repository-workflow | M0006 | M0007, M0008, M0010, M0011 | Retained | P09, P04, P07 |
| T0024-E03 | Mike | repository-workflow | M0012 | M0013, M0014, M0015 | Retained | P04 |
| T0024-E04 | Mike | repository-workflow | M0017 | M0018 | Retained | P03, P06 |
| T0025-E01 | Mike | authoring | M0000 | M0002, M0003 | Retained | P01, P03, P04 |
| T0026-E01 | Mike | repository-workflow | M0000 | M0002, M0004 | Retained | P01 |
| T0026-E02 | Mike | repository-workflow | M0005 | M0006, M0007 | Retained | P05, P06 |
| T0026-E03 | Mike | repository-workflow | M0008 | M0009, M0010 | Retained | P03 |
| T0026-E04 | Mike | repository-workflow | M0013 | M0014, M0015 | Retained | P01, P06 |
| T0026-E05 | Mike | review-process | M0016 | M0017 | Retained | P06, P09 |
| T0027-E01 | Mike | auth | M0000 | M0002 | Retained | P05 |
| T0027-E02 | Mike | repository-workflow | M0003 | M0004, M0005, M0006, M0007, M0008, M0009, M0011, M0012 | Retained | P03, P04, P01, P05, P02 |
| T0027-E03 | Mike | repository-workflow | M0003 | M0015 | Retained | P05, P01 |
| T0027-E04 | Mike | auth | M0003 | M0017, M0018, M0019 | Retained | P08, P05 |
| T0028-E01 | Mike | review-process | M0000 | M0002 | Retained | P06 |
| T0029-E01 | Mike | auth | M0000 | M0002 | Retained | P06 |
| T0030-E01 | Mike | auth | M0000 | M0002, M0003 | Retained | P09, P05 |
| T0031-E01 | Other human | review-process | M0000, M0002 | M0005 | Coordination only | - |
| T0033-E01 | Mike | review-process | M0000 | M0002, M0003 | Retained | P09, P05, P03 |
| T0034-E01 | Mike | versioning | M0000 | M0002 | Retained | P04, P10 |
| T0035-E01 | Mike | repository-workflow | M0000 | M0002 | Retained | P02, P03, P05 |
| T0036-E01 | Mike | spec-review | M0000 | M0002 | Retained | P03, P04, P05 |
| T0036-E02 | Mike | review-process | M0003 | M0005 | Retained | P09 |
| T0037-E01 | Mike | review-process | M0000 | M0002 | Retained | P06, P09 |
| T0038-E01 | Mike | review-process | M0000 | M0002 | Retained | P06, P09 |
| T0039-E01 | Other human | review-process | M0000 | M0003, M0004 | Retained | P03, P08 |
| T0040-E01 | Other human | review-process | M0000 | M0002 | Retained | P05, P07 |
| T0040-E02 | Mike | review-process | M0003 | M0005 | Coordination only | - |
| T0040-E03 | Other human | review-process | M0003 | M0006 | Coordination only | - |
| T0040-E04 | Other human | review-process | M0003 | M0007, M0008 | Retained | P09, P04, P05 |
| T0040-E05 | Other human | documentation | M0010 | M0012 | Retained | P09, P04 |
| T0041-E01 | Mike | repository-workflow | M0000 | M0002 | Retained | P03, P02 |
| T0041-E02 | Mike | documentation | M0003 | M0004 | Retained | P09 |
| T0041-E03 | Mike | review-process | M0000 | M0005 | Retained | P05 |
| T0042-E01 | Other human | sdk-review | M0000 | M0002 | Coordination only | - |
| T0043-E01 | Mike | versioning | M0000 | M0002 | Retained | P03 |
| T0043-E02 | Mike | review-process | M0003 | M0005 | Retained | P06 |
| T0043-E03 | Mike | versioning | M0006 | M0007 | Retained | P04 |
| T0043-E04 | Mike | review-process | M0008 | M0009 | Retained | P09 |
| T0043-E05 | Mike | auth | M0010 | M0011 | Retained | P05, P03 |
| T0043-E06 | Mike | auth | M0010, M0012 | M0013 | Retained | P05 |
| T0044-E01 | Mike | versioning | M0000, M0004 | M0005, M0006, M0007 | Retained | P03, P04, P06 |
| T0045-E01 | Mike | review-process | M0000 | M0002 | Retained | P03, P05 |
| T0046-E01 | Mike | review-process | M0004, M0006 | M0008 | Retained | P09 |
| T0046-E02 | Mike | review-process | M0009 | M0010 | Retained | P02, P06 |
| T0046-E03 | Other human | review-process | M0009 | M0011 | Retained | P04 |
| T0047-E01 | Mike | auth | M0000, M0004 | M0006 | Retained | P06 |
| T0048-E01 | Mike | auth | M0000 | M0002 | Retained | P04, P07, P05, P09 |
| T0049-E01 | Other human | repository-workflow | M0000 | M0004 | Retained | P03, P01, P07 |
| T0049-E02 | Other human | repository-workflow | M0005 | M0006, M0007 | Retained | P04, P10 |
| T0049-E03 | Other human | repository-workflow | M0008 | M0010 | Retained | P01, P09 |
| T0050-E01 | Mike | auth | M0000 | M0002, M0003, M0005 | Retained | P09, P01, P07, P05 |
| T0050-E02 | Mike | repository-workflow | M0006 | M0007, M0008 | Retained | P05 |
| T0051-E01 | Mike | authoring | M0000 | M0002 | Retained | P02, P06, P07 |
| T0051-E02 | Mike | review-process | M0003 | M0004 | Retained | P06, P07 |
| T0052-E01 | Mike | review-process | M0000 | M0002 | Retained | P06, P09 |
| T0055-E01 | Mike | sdk-review | M0000 | M0002 | Retained | P04, P06 |
| T0055-E02 | Other human | sdk-review | M0000 | M0004 | Retained | P08, P05, P01 |
| T0057-E01 | Mike | spec-review | M0000 | M0002 | Retained | P09, P05 |
| T0057-E02 | Mike | spec-review | M0000 | M0003, M0004 | Retained | P03, P01, P05 |
| T0057-E03 | Mike | versioning | M0000 | M0005 | Retained | P03, P06 |
| T0058-E01 | Mike | auth | M0000 | M0002 | Retained | P01, P09 |
| T0058-E02 | Other human | sdk-review | M0000 | M0003 | Retained | P03, P10 |
| T0058-E03 | Mike | auth | M0004, M0005 | M0006 | Retained | P06, P05 |
| T0059-E01 | Mike | auth | M0000, M0002 | M0003 | Retained | P01, P07, P09 |
| T0059-E02 | Mike | review-process | M0004 | M0005 | Retained | P09, P03 |
| T0060-E01 | Other human | authoring | M0000 | M0002, M0003 | Retained | P05 |
| T0061-E01 | Mike | spec-review | M0000 | M0002 | Retained | P05, P09, P02 |
| T0061-E02 | Mike | authoring | M0003 | M0004 | Retained | P03, P06 |
| T0062-E01 | Mike | review-process | M0000 | M0002 | Coordination only | - |
| T0064-E01 | Mike | review-process | M0000 | M0002 | Retained | P06, P04 |
| T0065-E01 | Mike | versioning | M0000 | M0002 | Retained | P03 |
| T0066-E01 | Mike | auth | M0000 | M0002 | Retained | P09 |
| T0068-E01 | Mike | auth | M0000 | M0002 | Retained | P07, P09 |
| T0068-E02 | Other human | auth | M0003 | M0005 | Retained | P07, P01, P03 |
| T0068-E03 | Mike | review-process | M0000 | M0004 | Retained | P04 |
| T0068-E04 | Mike | repository-workflow | M0008 | M0009 | Withheld: source support | - |
| T0068-E05 | Other human | repository-workflow | M0012 | M0013, M0015 | Retained | P02, P06 |
| T0068-E06 | Mike | review-process | M0012, M0014, M0016 | M0017 | Retained | P04, P02, P07 |
| T0068-E07 | Other human | review-process | M0017 | M0018 | Retained | P07 |
| T0069-E01 | Mike | review-process | M0002 | M0003 | Retained | P02, P09 |
| T0069-E02 | Mike | versioning | M0004 | M0005 | Retained | P05, P04, P09 |
| T0070-E01 | Mike | sdk-review | M0000 | M0002, M0003, M0004 | Retained | P07, P01, P08, P06 |
| T0070-E02 | Other human | sdk-review | M0010 | M0011 | Coordination only | - |
| T0070-E03 | Other human | repository-workflow | M0016 | M0017, M0018 | Retained | P04, P05 |
| T0071-E01 | Mike | sdk-review | M0000 | M0002, M0004 | Retained | P03, P02, P07 |
| T0071-E02 | Mike | review-process | M0007 | M0008 | Retained | P05, P04, P10 |
| T0072-E01 | Mike | sdk-review | M0000 | M0002, M0003 | Retained | P06, P09 |
| T0073-E01 | Other human | sdk-review | M0000 | M0002 | Retained | P09 |
| T0073-E02 | Other human | sdk-review | M0003 | M0010 | Retained | P08, P01, P07 |
| T0073-E03 | Mike | repository-workflow | M0007 | M0008 | Retained | P05 |
| T0074-E01 | Other human | review-process | M0000 | M0002 | Coordination only | - |
| T0076-E01 | Other human | sdk-review | M0000, M0006 | M0016 | Retained | P05, P04 |
| T0076-E02 | Mike | sdk-review | M0000, M0006, M0013 | M0012, M0014, M0015 | Retained | P02, P09, P06 |
| T0077-E01 | Mike | repository-workflow | M0000 | M0003 | Retained | P09, P06 |
| T0077-E02 | Mike | repository-workflow | M0004 | M0006 | Retained | P06 |
| T0078-E01 | Mike | review-process | M0000 | M0002, M0003 | Retained | P09, P06 |
| T0079-E01 | Mike | review-process | M0000, M0001 | M0002 | Retained | P05, P06, P04, P02 |
| T0079-E02 | Mike | spec-review | M0004 | M0005, M0006, M0007, M0008 | Retained | P08, P05, P01 |
| T0079-E03 | Mike | repository-workflow | M0009 | M0010 | Retained | P05 |
| T0080-E01 | Mike | review-process | M0000 | M0002 | Retained | P02, P09 |
| T0080-E02 | Other human | review-process | M0000 | M0003 | Retained | P09, P06 |
| T0084-E01 | Other human | review-process | M0000, M0002 | M0005 | Retained | P09, P05, P04 |
| T0084-E02 | Other human | review-process | M0006 | M0007 | Coordination only | - |
| T0085-E01 | Mike | authoring | M0000, M0002 | M0004 | Retained | P05, P07 |
| T0086-E01 | Mike | review-process | M0000 | M0002 | Retained | P03, P05, P09 |
| T0087-E01 | Mike | review-process | M0000 | M0002 | Retained | P04 |
| T0087-E02 | Mike | review-process | M0000, M0005 | M0004, M0006 | Retained | P02, P04 |
| T0088-E01 | Mike | review-process | M0000 | M0003 | Retained | P06, P09 |
| T0089-E01 | Mike | review-process | M0000 | M0002 | Retained | P09, P06 |
| T0089-E02 | Mike | authoring | M0000 | M0003, M0005, M0007, M0010, M0011, M0012, M0013 | Retained | P02, P01, P05, P07 |
| T0091-E01 | Mike | review-process | M0000 | M0002 | Retained | P09, P03, P05 |
| T0092-E01 | Mike | review-process | M0000 | M0002 | Retained | P03 |
| T0092-E02 | Mike | review-process | M0003 | M0004 | Retained | P02, P10 |
| T0092-E03 | Mike | review-process | M0005 | M0006 | Retained | P05, P03, P09, P06 |
| T0093-E01 | Mike | review-process | M0000 | M0002 | Retained | P02, P04 |
| T0093-E02 | Mike | versioning | M0003, M0004 | M0005 | Retained | P03, P09 |
| T0093-E03 | Mike | versioning | M0006, M0007 | M0008 | Retained | P04, P05 |
| T0094-E01 | Mike | auth | M0000 | M0002 | Retained | P09, P02, P06 |
| T0095-E01 | Mike | auth | M0000 | M0002, M0003 | Retained | P01, P09, P05 |
| T0096-E01 | Mike | sdk-review | M0004 | M0006 | Coordination only | - |
| T0096-E02 | Other human | sdk-review | M0004 | M0007 | Coordination only | - |
| T0096-E03 | Other human | sdk-review | M0004 | M0008 | Retained | P01, P02 |
| T0096-E04 | Other human | sdk-review | M0008 | M0009 | Retained | P05 |
| T0096-E05 | Other human | review-process | M0010 | M0011 | Coordination only | - |
| T0097-E01 | Mike | versioning | M0000 | M0002 | Retained | P04 |
| T0097-E02 | Other human | versioning | M0000 | M0003 | Retained | P07, P04 |
| T0097-E03 | Other human | versioning | M0000 | M0005, M0006 | Retained | P03, P04 |
| T0097-E04 | Other human | versioning | M0007 | M0008 | Retained | P04 |
| T0100-E01 | Mike | versioning | M0000 | M0002 | Retained | P06 |
| T0102-E01 | Mike | sdk-review | M0000 | M0002 | Retained | P01, P06 |
| T0102-E02 | Other human | sdk-review | M0003 | M0004 | Retained | P01 |
| T0103-E01 | Mike | auth | M0000 | M0003, M0005 | Retained | P07, P01, P05 |
| T0104-E01 | Other human | sdk-review | M0000 | M0002 | Coordination only | - |
| T0105-E01 | Mike | auth | M0000 | M0002, M0003 | Retained | P01, P04, P05, P08 |
| T0106-E01 | Mike | auth | M0000, M0002 | M0004 | Retained | P03, P05 |
| T0107-E01 | Other human | sdk-review | M0000 | M0002, M0003 | Retained | P05, P04 |
| T0107-E02 | Other human | sdk-review | M0004, M0005 | M0007 | Retained | P04, P05 |
| T0107-E03 | Other human | sdk-review | M0008 | M0009 | Retained | P03, P09 |
| T0107-E04 | Other human | sdk-review | M0011 | M0012 | Retained | P04, P09 |
| T0108-E01 | Mike | repository-workflow | M0000, M0005, M0009, M0011 | M0002, M0003, M0004, M0006, M0007, M0010, M0012, M0013 | Retained | P03, P09, P02, P05 |
| T0108-E02 | Mike | review-process | M0015 | M0016 | Retained | P06, P02 |
| T0108-E03 | Other human | sdk-review | M0017, M0018 | M0020 | Retained | P04, P07, P03 |
| T0108-E04 | Other human | authoring | M0022 | M0023, M0024, M0025 | Retained | P05, P07 |
| T0110-E01 | Mike | review-process | M0000 | M0002 | Retained | P06, P09 |
| T0110-E02 | Mike | auth | M0003 | M0004, M0005 | Retained | P03, P02, P05 |
| T0111-E01 | Mike | documentation | M0000 | M0002 | Coordination only | - |
| T0112-E01 | Mike | spec-review | M0000 | M0002 | Retained | P02, P04, P06 |
| T0112-E02 | Other human | authoring | M0000 | M0003 | Retained | P03 |
| T0112-E03 | Other human | authoring | M0006 | M0007 | Retained | P03, P05 |
| T0112-E04 | Other human | authoring | M0009 | M0010, M0011 | Retained | P08 |
| T0112-E05 | Other human | sdk-review | M0012 | M0013 | Retained | P06, P09 |
| T0112-E06 | Other human | review-process | M0014, M0015 | M0016 | Retained | P09, P08 |
| T0112-E07 | Mike | repository-workflow | M0006 | M0017 | Retained | P04, P03, P07 |
| T0112-E08 | Mike | review-process | M0021 | M0023 | Retained | P09, P07 |
| T0114-E01 | Mike | review-process | M0000 | M0002 | Retained | P09, P03 |
| T0115-E01 | Other human | auth | M0000 | M0001, M0003 | Retained | P03, P06 |
| T0115-E02 | Mike | review-process | M0005, M0008 | M0009 | Retained | P03, P05, P07 |
| T0116-E01 | Mike | sdk-review | M0000 | M0002, M0003 | Retained | P01, P05 |
| T0116-E02 | Mike | sdk-review | M0004 | M0005 | Retained | P06, P04 |
| T0116-E03 | Other human | sdk-review | M0004 | M0006 | Retained | P01, P02 |
| T0116-E04 | Other human | sdk-review | M0004 | M0007 | Retained | P01 |
| T0116-E05 | Other human | sdk-review | M0009 | M0010 | Retained | P08, P01, P05 |
| T0116-E06 | Other human | authoring | M0011 | M0012 | Retained | P09 |
| T0116-E07 | Other human | sdk-review | M0013 | M0014 | Retained | P03, P04, P06 |
| T0117-E01 | Mike | versioning | M0000 | M0002 | Coordination only | - |
| T0119-E01 | Mike | review-process | M0000 | M0001 | Retained | P09, P06 |
| T0119-E02 | Mike | review-process | M0002, M0003 | M0004 | Retained | P03, P09 |
| T0120-E01 | Mike | auth | M0000 | M0002, M0003 | Retained | P05, P07, P01 |
| T0121-E01 | Mike | spec-review | M0000, M0002 | M0003 | Coordination only | - |
| T0122-E01 | Mike | repository-workflow | M0000 | M0002, M0003 | Retained | P06, P03, P01 |
| T0122-E02 | Mike | repository-workflow | M0004 | M0006, M0007 | Retained | P05, P07, P06 |
| T0123-E01 | Other human | sdk-review | M0000 | M0002 | Retained | P01, P06 |
| T0123-E02 | Other human | sdk-review | M0002 | M0003 | Retained | P07, P04 |
| T0123-E03 | Other human | sdk-review | M0006 | M0008, M0009 | Retained | P03, P05 |
| T0123-E04 | Other human | sdk-review | M0004 | M0005, M0006 | Retained | P02, P07 |
| T0124-E01 | Other human | review-process | M0000 | M0001 | Retained | P05 |
| T0124-E02 | Other human | review-process | M0002 | M0003 | Retained | P06 |
| T0125-E01 | Other human | auth | M0000 | M0004 | Retained | P01, P05, P02 |
| T0125-E02 | Other human | auth | M0005 | M0006 | Retained | P05 |
| T0126-E01 | Mike | review-process | M0000 | M0001 | Coordination only | - |
| T0127-E01 | Other human | sdk-review | M0022 | M0026 | Retained | P01, P05 |
| T0128-E01 | Other human | auth | M0000 | M0002 | Retained | P05 |
| T0131-E01 | Mike | authoring | M0000 | M0002 | Retained | P05, P01 |
| T0131-E02 | Mike | documentation | M0000 | M0003 | Retained | P09, P02 |
| T0131-E03 | Mike | review-process | M0000 | M0004, M0005 | Retained | P06, P04, P05 |
| T0131-E04 | Mike | review-process | M0006 | M0007, M0008 | Retained | P06 |
| T0132-E01 | Mike | review-process | M0000 | M0002 | Retained | P03, P09, P06 |
| T0132-E02 | Other human | review-process | M0000 | M0003 | Retained | P03, P06 |
| T0132-E03 | Other human | spec-review | M0004 | M0006 | Retained | P08 |
| T0133-E01 | Other human | repository-workflow | M0000 | M0001 | Retained | P01, P03 |
| T0133-E02 | Other human | repository-workflow | M0004 | M0005 | Retained | P04, P05 |
| T0134-E01 | Mike | sdk-review | M0000 | M0002 | Coordination only | - |
| T0134-E02 | Other human | sdk-review | M0000 | M0003 | Retained | P06 |
| T0134-E03 | Other human | sdk-review | M0000 | M0004, M0006 | Retained | P08, P06 |
| T0134-E04 | Other human | sdk-review | M0009 | M0010 | Coordination only | - |
| T0134-E05 | Other human | sdk-review | M0011 | M0012 | Retained | P07, P05 |
| T0134-E06 | Mike | review-process | M0008, M0014 | M0015, M0016 | Retained | P09, P06, P02 |
| T0135-E01 | Mike | review-process | M0000 | M0002 | Retained | P06, P03, P02 |
| T0137-E01 | Other human | sdk-review | M0000 | M0005 | Retained | P01, P05 |
| T0137-E02 | Other human | sdk-review | M0006 | M0007 | Coordination only | - |
| T0140-E01 | Other human | spec-review | M0000, M0002, M0003 | M0004, M0005 | Retained | P05 |
| T0141-E01 | Mike | review-process | M0000 | M0002 | Retained | P06 |
| T0141-E02 | Other human | review-process | M0000 | M0003, M0005, M0008, M0011 | Retained | P02, P01, P03, P06 |
| T0141-E03 | Other human | review-process | M0000 | M0012 | Coordination only | - |
| T0141-E04 | Other human | review-process | M0013 | M0015 | Retained | P09, P06 |
| T0141-E05 | Other human | documentation | M0016 | M0017 | Retained | P03 |
| T0142-E01 | Other human | spec-review | M0000 | M0003, M0005 | Retained | P02, P03, P05, P04 |
| T0142-E02 | Other human | versioning | M0006 | M0007 | Coordination only | - |
| T0142-E03 | Other human | sdk-review | M0008 | M0009 | Coordination only | - |
| T0143-E01 | Mike | versioning | M0000 | M0002 | Retained | P05, P04 |
| T0145-E01 | Mike | versioning | M0000 | M0002 | Retained | P09 |
| T0146-E01 | Other human | review-process | M0000 | M0003 | Retained | P09, P03 |
| T0147-E01 | Other human | versioning | M0000 | M0003 | Retained | P03, P04 |
| T0147-E02 | Other human | review-process | M0005 | M0007 | Retained | P06, P04 |
| T0148-E01 | Mike | spec-review | M0000 | M0002 | Coordination only | - |
| T0149-E01 | Mike | versioning | M0000 | M0004 | Retained | P05, P04 |
| T0150-E01 | Mike | repository-workflow | M0000 | M0002, M0003 | Retained | P03, P05 |
| T0150-E02 | Mike | repository-workflow | M0004 | M0005, M0006, M0007, M0008 | Retained | P03, P05, P06 |
| T0150-E03 | Mike | repository-workflow | M0009 | M0010 | Retained | P03, P05 |
| T0150-E04 | Mike | repository-workflow | M0009 | M0011 | Retained | P02, P06, P10 |
| T0150-E05 | Mike | repository-workflow | M0012 | M0014, M0015 | Retained | P05, P03 |
| T0151-E01 | Mike | spec-review | M0000 | M0002 | Coordination only | - |
| T0152-E01 | Mike | spec-review | M0000 | M0002 | Retained | P09 |
| T0152-E02 | Other human | authoring | M0003 | M0004 | Retained | P01, P05 |
| T0152-E03 | Mike | authoring | M0003 | M0005 | Retained | P09 |
| T0153-E01 | Mike | review-process | M0000 | M0002 | Retained | P09 |
| T0154-E01 | Mike | review-process | M0000 | M0003 | Retained | P02 |
| T0154-E02 | Mike | auth | M0000 | M0004 | Retained | P01, P09 |
| T0154-E03 | Mike | review-process | M0005 | M0006 | Retained | P09 |
| T0156-E01 | Mike | review-process | M0000 | M0002, M0004 | Retained | P09, P02, P06, P07 |
| T0156-E02 | Other human | review-process | M0012 | M0013 | Retained | P10 |
| T0156-E03 | Other human | review-process | M0014 | M0015 | Retained | P03, P06 |
| T0156-E04 | Other human | review-process | M0016 | M0017 | Retained | P05, P09 |
| T0156-E05 | Mike | repository-workflow | M0018 | M0019 | Retained | P01, P10 |
| T0156-E06 | Mike | repository-workflow | M0020 | M0021 | Retained | P08 |
| T0160-E01 | Mike | spec-review | M0000 | M0002 | Coordination only | - |
| T0161-E01 | Other human | review-process | M0000 | M0002 | Retained | P09 |
| T0162-E01 | Mike | authoring | M0000 | M0002 | Retained | P06 |
| T0164-E01 | Mike | spec-review | M0000 | M0002, M0003, M0004, M0005, M0006, M0007 | Retained | P01, P04, P06, P08 |
| T0164-E02 | Mike | spec-review | M0009 | M0010, M0015 | Retained | P02, P03 |
| T0164-E03 | Mike | spec-review | M0011 | M0012, M0013 | Retained | P03, P04 |
| T0164-E04 | Mike | spec-review | M0008 | M0014 | Retained | P03, P05, P07, P06 |
| T0165-E01 | Mike | review-process | M0000, M0004, M0007, M0009 | M0003, M0005, M0006, M0008, M0010 | Retained | P02, P04, P05, P03, P06 |
| T0166-E01 | Mike | auth | M0000 | M0002 | Retained | P01, P09 |
| T0169-E01 | Other human | review-process | M0000, M0002 | M0003 | Coordination only | - |
| T0170-E01 | Mike | documentation | M0000 | M0002, M0003 | Retained | P06, P05 |
| T0170-E02 | Mike | review-process | M0004 | M0005 | Retained | P04, P06 |
| T0171-E01 | Mike | spec-review | M0000, M0002 | M0003 | Retained | P06 |
| T0171-E02 | Other human | spec-review | M0004 | M0005 | Retained | P08 |
| T0171-E03 | Other human | spec-review | M0006 | M0007, M0009 | Retained | P04, P08 |
| T0172-E01 | Mike | spec-review | M0000 | M0002 | Retained | P09, P05, P07 |
| T0181-E01 | Mike | auth | M0000 | M0002, M0004, M0006 | Retained | P01, P08, P07 |
| T0181-E02 | Mike | review-process | M0009 | M0010 | Retained | P03, P09 |
| T0182-E01 | Mike | spec-review | M0000 | M0002 | Retained | P05 |
| T0182-E02 | Mike | review-process | M0003 | M0004 | Retained | P06, P09 |
| T0184-E01 | Mike | review-process | M0000 | M0002 | Retained | P06 |
| T0185-E01 | Mike | versioning | M0000 | M0002 | Retained | P09, P05, P07 |
| T0185-E02 | Mike | documentation | M0003 | M0004 | Retained | P07, P06 |
| T0187-E01 | Mike | versioning | M0000 | M0002, M0003, M0004, M0005 | Retained | P05, P09 |
| T0187-E02 | Mike | sdk-review | M0006 | M0007, M0009, M0010, M0011 | Retained | P01, P08, P03 |
| T0188-E01 | Mike | versioning | M0000 | M0003, M0004 | Retained | P03, P02 |
| T0188-E02 | Mike | authoring | M0005 | M0006 | Retained | P06 |
| T0188-E03 | Mike | review-process | M0000 | M0007 | Retained | P03, P01, P09 |
| T0188-E04 | Mike | review-process | M0008 | M0009 | Retained | P06 |
| T0189-E01 | Mike | versioning | M0000 | M0002 | Retained | P09, P03 |
| T0191-E01 | Mike | review-process | M0000 | M0002, M0003, M0004 | Retained | P01, P04, P09, P06 |
| T0192-E01 | Other human | sdk-review | M0000 | M0002 | Retained | P06 |
| T0193-E01 | Mike | versioning | M0000 | M0002 | Retained | P09, P04 |
| T0193-E02 | Mike | spec-review | M0004 | M0005, M0007, M0008 | Retained | P02, P03, P07 |
| T0194-E01 | Other human | sdk-review | M0010 | M0011 | Retained | P05 |
| T0194-E02 | Other human | versioning | M0012 | M0013, M0014 | Retained | P03, P05 |
| T0194-E03 | Other human | review-process | M0015 | M0016 | Retained | P05, P06 |
| T0195-E01 | Mike | repository-workflow | M0000 | M0002 | Retained | P09, P03, P07 |
| T0196-E01 | Mike | versioning | M0002 | M0003 | Retained | P09, P06 |
| T0196-E02 | Mike | spec-review | M0004 | M0005 | Retained | P04, P09 |
| T0196-E03 | Mike | authoring | M0006 | M0007 | Retained | P03, P04, P06 |
| T0196-E04 | Mike | review-process | M0008 | M0009 | Retained | P09 |
| T0196-E05 | Mike | spec-review | M0008 | M0010, M0012, M0013, M0014, M0015, M0016 | Retained | P02, P09, P04 |
| T0196-E06 | Mike | repository-workflow | M0017 | M0018 | Retained | P05, P03, P07 |
| T0196-E07 | Mike | authoring | M0019 | M0020, M0021, M0022 | Retained | P05, P03, P06 |
| T0196-E08 | Mike | authoring | M0023 | M0024 | Retained | P07 |
| T0198-E01 | Mike | review-process | M0000 | M0002 | Retained | P09, P04 |
| T0198-E02 | Mike | review-process | M0003 | M0004 | Retained | P03, P01, P05 |
| T0199-E01 | Mike | review-process | M0000 | M0002 | Retained | P09, P02 |
| T0200-E01 | Mike | spec-review | M0000 | M0002 | Retained | P03, P05, P09 |
| T0201-E01 | Mike | review-process | M0000 | M0003 | Retained | P06 |
| T0202-E01 | Mike | spec-review | M0000 | M0002 | Retained | P01, P09 |
| T0202-E02 | Mike | review-process | M0000 | M0003 | Retained | P04, P05 |
| T0204-E01 | Mike | auth | M0004 | M0006, M0007, M0009, M0010, M0011, M0012, M0013, M0014, M0016, M0017, M0018, M0019, M0020, M0021, M0022, M0023 | Retained | P01, P05, P04, P08 |
| T0207-E01 | Mike | review-process | M0000 | M0002 | Retained | P03, P07 |
| T0207-E02 | Mike | repository-workflow | M0000, M0003 | M0004 | Retained | P08 |
| T0207-E03 | Mike | repository-workflow | M0005 | M0006 | Retained | P04, P10 |
| T0207-E04 | Mike | review-process | M0008 | M0013 | Coordination only | - |
| T0207-E05 | Mike | review-process | M0009 | M0010 | Retained | P04 |
| T0207-E06 | Mike | repository-workflow | M0014 | M0015 | Retained | P05 |
| T0207-E07 | Mike | repository-workflow | M0014, M0016, M0017 | M0018 | Coordination only | - |
| T0207-E08 | Mike | repository-workflow | M0019 | M0020 | Retained | P01 |
| T0207-E09 | Mike | repository-workflow | M0022, M0023 | M0024, M0025 | Retained | P04, P09, P03 |
| T0207-E10 | Mike | repository-workflow | M0026 | M0027, M0028 | Retained | P09, P05 |
| T0208-E01 | Mike | auth | M0000 | M0002 | Retained | P03, P09, P05 |
| T0209-E01 | Mike | documentation | M0000, M0002 | M0003 | Retained | P09 |
| T0210-E01 | Mike | auth | M0000, M0003 | M0002, M0004 | Retained | P02, P05, P04 |
| T0211-E01 | Mike | auth | M0000, M0002 | M0003 | Retained | P01, P05, P07 |
| T0213-E01 | Other human | authoring | M0000 | M0002 | Retained | P04, P10 |
| T0214-E01 | Other human | sdk-review | M0000, M0002 | M0003 | Coordination only | - |
| T0214-E02 | Other human | sdk-review | M0003 | M0004 | Retained | P07 |
| T0216-E01 | Mike | review-process | M0000 | M0002 | Retained | P06 |
| T0217-E01 | Mike | spec-review | M0000 | M0002 | Retained | P05, P03, P10 |
| T0218-E01 | Mike | review-process | M0000, M0002 | M0005 | Retained | P09 |
| T0219-E01 | Mike | repository-workflow | M0000, M0002 | M0003 | Retained | P06 |
| T0220-E01 | Mike | review-process | M0002 | M0003 | Retained | P05, P09 |
| T0220-E02 | Mike | review-process | M0004 | M0005 | Retained | P06 |
| T0224-E01 | Mike | review-process | M0000 | M0002 | Retained | P02, P09 |
| T0224-E02 | Mike | documentation | M0003 | M0004, M0005 | Retained | P07, P06 |
| T0224-E03 | Mike | documentation | M0006 | M0007 | Retained | P07, P02 |
| T0224-E04 | Mike | documentation | M0006 | M0008, M0009, M0011, M0013 | Retained | P03, P07, P01 |
| T0225-E01 | Mike | versioning | M0000 | M0002 | Retained | P09, P04 |
| T0225-E02 | Other human | versioning | M0000 | M0003 | Retained | P04, P10 |
| T0225-E03 | Mike | repository-workflow | M0004 | M0005 | Coordination only | - |
| T0226-E01 | Other human | auth | M0000, M0002 | M0003, M0005 | Retained | P03, P01, P04 |
| T0228-E01 | Mike | review-process | M0000 | M0002 | Retained | P06, P03, P05 |
| T0229-E01 | Mike | auth | M0000 | M0002, M0003, M0004, M0006 | Retained | P01, P03, P04, P02, P05 |
| T0229-E02 | Mike | documentation | M0007 | M0008 | Retained | P02, P06 |
| T0230-E01 | Other human | review-process | M0000, M0002 | M0003 | Retained | P06, P04 |
| T0230-E02 | Other human | review-process | M0004 | M0005 | Coordination only | - |
| T0231-E01 | Mike | authoring | M0000, M0002 | M0003 | Retained | P02, P09 |
| T0231-E02 | Mike | spec-review | M0004 | M0005 | Withheld: source support | - |
| T0232-E01 | Mike | versioning | M0000 | M0003 | Retained | P06 |
| T0233-E01 | Mike | review-process | M0000 | M0002 | Retained | P09, P04 |
| T0235-E01 | Mike | review-process | M0002 | M0004 | Retained | P03, P01 |
| T0236-E01 | Mike | auth | M0000 | M0002, M0003, M0004 | Retained | P01, P09, P05 |
| T0237-E01 | Mike | review-process | M0000 | M0002, M0003, M0004, M0005 | Retained | P03, P01, P07, P06 |
| T0238-E01 | Mike | authoring | M0000 | M0002, M0003, M0004 | Retained | P03, P02, P05, P06, P09, P07 |
| T0239-E01 | Mike | review-process | M0000 | M0002 | Retained | P06 |
| T0240-E01 | Other human | spec-review | M0000 | M0002, M0003 | Retained | P01, P07 |
| T0240-E02 | Mike | spec-review | M0000 | M0006, M0009, M0010, M0011 | Retained | P07, P02, P03, P05, P04 |
| T0240-E03 | Mike | spec-review | M0012 | M0013, M0014, M0017, M0019, M0020, M0021 | Retained | P04, P08, P01, P07 |
| T0240-E04 | Mike | spec-review | M0000 | M0015 | Coordination only | - |
| T0240-E05 | Mike | spec-review | M0000 | M0022, M0023 | Retained | P08 |
| T0241-E01 | Mike | review-process | M0000 | M0001 | Retained | P09, P04 |
| T0241-E02 | Mike | review-process | M0002 | M0003 | Retained | P09, P08 |
| T0242-E01 | Mike | sdk-review | M0000 | M0002 | Retained | P02, P05, P07 |
| T0242-E02 | Other human | review-process | M0000 | M0003 | Retained | P04, P09 |
| T0245-E01 | Other human | repository-workflow | M0000 | M0008 | Retained | P04 |
| T0245-E02 | Mike | repository-workflow | M0002 | M0006 | Retained | P09, P04 |
| T0245-E03 | Mike | repository-workflow | M0004 | M0007 | Retained | P04, P06 |
| T0245-E04 | Other human | repository-workflow | M0009 | M0018 | Retained | P03, P04 |
| T0245-E05 | Mike | repository-workflow | M0013 | M0015 | Retained | P03, P06 |
| T0245-E06 | Mike | review-process | M0016 | M0017 | Retained | P06 |
| T0246-E01 | Mike | repository-workflow | M0000 | M0002, M0003, M0005 | Retained | P07, P05, P08 |
| T0247-E01 | Mike | documentation | M0000 | M0002 | Retained | P06 |
| T0248-E01 | Mike | review-process | M0000 | M0001 | Retained | P09 |
| T0248-E02 | Mike | review-process | M0002 | M0003 | Retained | P06 |
| T0250-E01 | Mike | spec-review | M0000, M0002, M0003, M0004, M0005 | M0008 | Retained | P04 |
| T0250-E02 | Other human | spec-review | M0000, M0002, M0003, M0004, M0005 | M0006 | Coordination only | - |
| T0251-E01 | Mike | authoring | M0000 | M0002, M0003, M0004 | Retained | P04, P05, P06 |
| T0251-E02 | Other human | authoring | M0000 | M0005, M0006 | Retained | P04, P01 |
| T0251-E03 | Other human | authoring | M0008 | M0009, M0010, M0012 | Retained | P05, P09 |
| T0253-E01 | Mike | review-process | M0000 | M0002, M0004, M0005 | Retained | P03, P06, P09 |
| T0254-E01 | Mike | sdk-review | M0000 | M0002, M0003, M0005 | Retained | P01, P06, P02 |
| T0255-E01 | Mike | documentation | M0000 | M0002 | Retained | P05 |
| T0256-E01 | Mike | auth | M0002 | M0003, M0006, M0009, M0010, M0011 | Retained | P02, P03, P04, P07, P05, P06 |
| T0258-E01 | Mike | spec-review | M0000 | M0002, M0003, M0005, M0006 | Retained | P01, P03 |
| T0258-E02 | Mike | review-process | M0000, M0004 | M0008, M0010 | Retained | P02 |
| T0258-E03 | Mike | versioning | M0011 | M0012 | Retained | P09, P10 |
| T0258-E04 | Mike | versioning | M0011 | M0014, M0015 | Retained | P07, P04 |
| T0258-E05 | Mike | auth | M0016 | M0017 | Retained | P04 |
| T0260-E01 | Mike | versioning | M0000 | M0002 | Retained | P09 |
| T0261-E01 | Mike | spec-review | M0000 | M0003, M0007 | Retained | P01, P05 |
| T0261-E02 | Mike | spec-review | M0000 | M0005 | Retained | P03, P07 |
| T0261-E03 | Mike | spec-review | M0008 | M0009, M0011 | Retained | P03, P05 |
| T0262-E01 | Mike | review-process | M0000 | M0002 | Retained | P09, P04 |
| T0263-E01 | Mike | versioning | M0000 | M0002 | Retained | P09 |
| T0264-E01 | Mike | sdk-review | M0000 | M0002 | Retained | P09 |
| T0265-E01 | Mike | auth | M0000 | M0002 | Retained | P06 |
| T0266-E01 | Mike | auth | M0002 | M0003 | Retained | P07, P06 |
| T0267-E01 | Mike | review-process | M0000 | M0002 | Retained | P04, P01 |
| T0269-E01 | Mike | repository-workflow | M0000, M0002 | M0003 | Retained | P03 |
| T0270-E01 | Mike | authoring | M0000 | M0002 | Retained | P06 |
| T0272-E01 | Other human | repository-workflow | M0000 | M0002 | Retained | P05 |
| T0272-E02 | Mike | repository-workflow | M0000, M0004, M0005, M0008 | M0003, M0006, M0007, M0009 | Retained | P04, P02, P07, P08 |
| T0272-E03 | Mike | repository-workflow | M0012, M0014 | M0013, M0015 | Retained | P03, P05, P04 |
| T0274-E01 | Mike | repository-workflow | M0000 | M0002, M0003, M0004 | Retained | P01, P07, P05 |
| T0275-E01 | Mike | sdk-review | M0000 | M0003 | Retained | P01, P07 |
| T0276-E01 | Mike | spec-review | M0000 | M0002, M0003, M0005, M0006 | Retained | P01, P05, P04 |
| T0277-E01 | Mike | sdk-review | M0000 | M0002, M0003 | Retained | P06, P09 |
| T0278-E01 | Mike | review-process | M0000 | M0002, M0003 | Retained | P03, P02 |
| T0279-E01 | Mike | review-process | M0000 | M0002, M0010 | Retained | P06 |
| T0279-E02 | Mike | auth | M0000 | M0003, M0004, M0005 | Retained | P01, P09 |
| T0279-E03 | Mike | auth | M0000 | M0007 | Retained | P09 |
| T0279-E04 | Mike | review-process | M0000 | M0008 | Retained | P05 |
| T0279-E05 | Mike | review-process | M0000 | M0009 | Retained | P01, P06, P09 |
| T0279-E06 | Mike | auth | M0011 | M0012 | Retained | P01 |
| T0279-E07 | Mike | repository-workflow | M0013 | M0014, M0015 | Retained | P03, P07, P05 |
| T0279-E08 | Mike | review-process | M0017 | M0019 | Retained | P09, P06 |
| T0279-E09 | Mike | review-process | M0017 | M0020 | Retained | P09, P04 |
| T0280-E01 | Mike | spec-review | M0000 | M0002, M0003, M0004, M0005 | Retained | P07, P01, P05 |
| T0282-E01 | Mike | repository-workflow | M0000 | M0002, M0003, M0005 | Retained | P03, P02, P01, P07 |
| T0282-E02 | Mike | repository-workflow | M0007 | M0008, M0009 | Retained | P05, P04 |
| T0282-E03 | Mike | auth | M0010 | M0011, M0012, M0013 | Retained | P04, P06 |
| T0283-E01 | Mike | documentation | M0000 | M0002 | Retained | P06 |
| T0284-E01 | Mike | versioning | M0000 | M0002 | Retained | P09, P06 |
| T0286-E01 | Mike | spec-review | M0000 | M0002 | Coordination only | - |
| T0286-E02 | Other human | spec-review | M0000 | M0004 | Retained | P07, P05 |
| T0287-E01 | Mike | versioning | M0002 | M0004 | Retained | P03, P04 |
| T0288-E01 | Mike | review-process | M0002 | M0003 | Retained | P03, P09 |
| T0288-E02 | Mike | review-process | M0004 | M0005 | Retained | P09, P01, P07 |
| T0289-E01 | Mike | repository-workflow | M0000 | M0002, M0004 | Retained | P03, P07, P05, P04 |
| T0290-E01 | Mike | versioning | M0000 | M0001, M0002, M0003, M0004, M0005, M0006 | Retained | P03, P09, P04 |
| T0290-E02 | Mike | authoring | M0007 | M0008, M0009 | Retained | P03, P04 |
| T0290-E03 | Other human | versioning | M0010, M0011 | M0012 | Retained | P06, P07 |
| T0290-E04 | Mike | versioning | M0010, M0011 | M0013, M0014, M0018 | Retained | P02, P05, P07, P06 |
| T0290-E05 | Mike | documentation | M0019 | M0025, M0033 | Retained | P06, P09 |
| T0290-E06 | Mike | spec-review | M0000 | M0020, M0023, M0024 | Retained | P04 |
| T0290-E07 | Other human | versioning | M0019 | M0028 | Retained | P02, P04 |
| T0290-E08 | Mike | authoring | M0026 | M0029 | Retained | P03, P05 |
| T0290-E09 | Mike | versioning | M0030 | M0032 | Retained | P05, P04 |
| T0290-E10 | Mike | repository-workflow | M0034, M0035 | M0036 | Retained | P07 |
| T0290-E11 | Mike | spec-review | M0034, M0035 | M0038, M0039, M0040 | Retained | P05, P04 |
| T0290-E12 | Mike | spec-review | M0041 | M0042 | Retained | P09, P05 |
| T0290-E13 | Mike | documentation | M0043 | M0045 | Retained | P09 |
| T0290-E14 | Mike | sdk-review | M0046 | M0048, M0049 | Retained | P04 |
| T0291-E01 | Mike | authoring | M0000 | M0002 | Retained | P09, P05 |
| T0291-E02 | Other human | authoring | M0003 | M0004 | Retained | P04, P05 |
| T0292-E01 | Mike | authoring | M0000 | M0003, M0004 | Retained | P06, P03 |
| T0292-E02 | Other human | authoring | M0000 | M0006 | Retained | P01, P05 |
| T0293-E01 | Mike | auth | M0000 | M0008, M0009 | Retained | P04, P08, P07 |
| T0293-E02 | Other human | auth | M0002 | M0003, M0004, M0006 | Retained | P01, P03, P08 |
| T0293-E03 | Other human | sdk-review | M0006 | M0007 | Retained | P10 |
| T0293-E04 | Mike | authoring | M0010 | M0011 | Retained | P05, P07 |
| T0296-E01 | Mike | sdk-review | M0000 | M0002 | Retained | P08 |
| T0298-E01 | Mike | authoring | M0000, M0002, M0004 | M0006 | Retained | P06, P07 |
| T0299-E01 | Mike | spec-review | M0000 | M0002, M0003, M0004, M0005, M0006, M0007 | Retained | P01, P04, P05 |
| T0300-E01 | Other human | review-process | M0000 | M0001 | Retained | P05 |
| T0300-E02 | Mike | spec-review | M0002 | M0003, M0004, M0005, M0006 | Retained | P05, P08, P07 |
| T0300-E03 | Other human | spec-review | M0002 | M0009 | Retained | P01, P05 |
| T0300-E04 | Mike | review-process | M0010 | M0011 | Retained | P06, P09 |
| T0301-E01 | Mike | sdk-review | M0000 | M0002, M0003 | Retained | P04, P09 |
| T0302-E01 | Mike | documentation | M0000, M0002 | M0011 | Retained | P09, P02 |
| T0302-E02 | Mike | spec-review | M0003, M0005, M0007 | M0012 | Retained | P05, P07 |
| T0302-E03 | Mike | documentation | M0013, M0014, M0017, M0020 | M0015, M0016, M0019, M0021 | Retained | P07, P08, P06 |
| T0302-E04 | Other human | documentation | M0020 | M0022 | Retained | P03, P10 |
| T0302-E05 | Mike | documentation | M0020 | M0024, M0025 | Retained | P01, P05, P08 |
| T0304-E01 | Mike | documentation | M0000 | M0002 | Retained | P06 |
| T0305-E01 | Mike | spec-review | M0000 | M0002 | Retained | P01, P09, P05 |
| T0305-E02 | Mike | spec-review | M0003 | M0004, M0005 | Retained | P03, P05 |
| T0305-E03 | Mike | review-process | M0006 | M0007 | Retained | P06 |
| T0305-E04 | Mike | spec-review | M0008 | M0009 | Retained | P01, P05 |
| T0306-E01 | Mike | authoring | M0000 | M0004 | Retained | P06, P01 |
| T0306-E02 | Mike | review-process | M0005 | M0006 | Retained | P06 |
| T0307-E01 | Mike | review-process | M0000 | M0002 | Retained | P05, P04 |
| T0307-E02 | Mike | review-process | M0003 | M0004 | Retained | P06, P03 |
| T0309-E01 | Other human | review-process | M0000 | M0002 | Retained | P03, P01, P05 |
| T0311-E01 | Mike | spec-review | M0000 | M0001, M0002 | Retained | P07, P01 |
| T0311-E02 | Mike | repository-workflow | M0000 | M0003, M0004, M0005, M0007 | Retained | P05, P09 |
| T0311-E03 | Mike | repository-workflow | M0008, M0009 | M0010, M0011 | Retained | P04, P10 |
| T0311-E04 | Mike | repository-workflow | M0012 | M0013 | Retained | P07, P06 |
| T0311-E05 | Mike | repository-workflow | M0014, M0015 | M0016, M0017 | Retained | P01, P06 |
| T0311-E06 | Mike | repository-workflow | M0020 | M0021 | Retained | P07, P04 |
| T0311-E07 | Other human | repository-workflow | M0012, M0014, M0015 | M0022, M0023 | Retained | P04, P01 |
| T0311-E08 | Other human | repository-workflow | M0024 | M0025 | Retained | P03 |
| T0311-E09 | Other human | repository-workflow | M0026 | M0027 | Retained | P04, P10 |
| T0311-E10 | Other human | repository-workflow | M0028 | M0029 | Retained | P09 |
| T0311-E11 | Mike | authoring | M0030 | M0031 | Retained | P06 |
| T0311-E12 | Mike | repository-workflow | M0032, M0033 | M0034, M0035 | Retained | P04, P07 |
| T0311-E13 | Mike | review-process | M0036 | M0037 | Retained | P06, P07 |
| T0311-E14 | Other human | repository-workflow | M0036 | M0038 | Retained | P04, P05 |
| T0311-E15 | Other human | review-process | M0039, M0040 | M0041 | Retained | P06 |
| T0311-E16 | Mike | review-process | M0043 | M0044, M0045, M0046 | Retained | P05, P09, P03 |
| T0312-E01 | Mike | spec-review | M0000 | M0002, M0003, M0005, M0006, M0007, M0008, M0010 | Retained | P01, P02, P05, P08 |
| T0314-E01 | Other human | versioning | M0000 | M0003 | Retained | P04, P09 |
| T0314-E02 | Other human | spec-review | M0005 | M0006 | Retained | P07, P05 |
| T0315-E01 | Other human | authoring | M0000, M0002, M0003 | M0004 | Retained | P05, P10 |
| T0315-E02 | Other human | authoring | M0005, M0006, M0007 | M0008, M0009 | Retained | P05 |
| T0315-E03 | Other human | repository-workflow | M0011 | M0012 | Coordination only | - |
| T0315-E04 | Other human | authoring | M0011 | M0016, M0017, M0019 | Retained | P09, P04 |
| T0315-E05 | Other human | repository-workflow | M0020, M0021, M0022 | M0023 | Retained | P08 |
| T0315-E06 | Other human | authoring | M0011 | M0026, M0027 | Retained | P05, P04 |
| T0315-E07 | Other human | sdk-review | M0028 | M0029, M0030 | Retained | P04, P05 |
| T0315-E08 | Other human | review-process | M0032 | M0033 | Retained | P09 |
| T0316-E01 | Mike | auth | M0000 | M0001 | Retained | P05, P09 |
| T0317-E01 | Mike | documentation | M0000 | M0002 | Retained | P04, P05, P02 |
| T0318-E01 | Mike | versioning | M0005 | M0007 | Retained | P01, P06 |
| T0318-E02 | Mike | authoring | M0005 | M0008 | Retained | P02, P04 |
| T0318-E03 | Mike | versioning | M0005 | M0009 | Retained | P09 |
| T0318-E04 | Other human | versioning | M0013 | M0014 | Retained | P09 |
| T0319-E01 | Mike | versioning | M0000 | M0002, M0003, M0004, M0006 | Retained | P03, P01, P05, P07, P06 |
| T0321-E01 | Mike | spec-review | M0000, M0004 | M0001, M0003, M0007, M0008, M0009 | Retained | P01, P02, P04, P05 |
| T0321-E02 | Mike | repository-workflow | M0010, M0011 | M0012, M0013 | Retained | P03, P05, P06 |
| T0321-E03 | Mike | repository-workflow | M0014 | M0015, M0016 | Retained | P06, P04 |
| T0321-E04 | Mike | repository-workflow | M0018 | M0019, M0020, M0021 | Retained | P03, P09, P06 |
| T0321-E05 | Mike | repository-workflow | M0022 | M0023 | Retained | P10 |
| T0321-E06 | Mike | repository-workflow | M0024 | M0025 | Retained | P04 |
| T0321-E07 | Mike | repository-workflow | M0026, M0029, M0032 | M0027, M0028, M0030, M0033 | Retained | P02, P07 |
| T0321-E08 | Mike | repository-workflow | M0034 | M0035 | Retained | P03, P06 |
| T0321-E09 | Mike | repository-workflow | M0036 | M0037 | Retained | P05 |
| T0321-E10 | Mike | auth | M0039, M0042 | M0040, M0041, M0043 | Retained | P05, P08 |
| T0321-E11 | Mike | spec-review | M0045 | M0046 | Retained | P07, P09 |
| T0321-E12 | Mike | spec-review | M0048 | M0049 | Coordination only | - |
| T0321-E13 | Mike | documentation | M0000 | M0002, M0005 | Retained | P03 |
| T0321-E14 | Mike | review-process | M0029 | M0031 | Retained | P06 |
| T0322-E01 | Mike | spec-review | M0000 | M0005, M0006 | Retained | P01, P07 |
| T0322-E02 | Mike | spec-review | M0002 | M0004 | Retained | P03, P09 |
| T0322-E03 | Mike | spec-review | M0000 | M0011, M0013, M0014 | Retained | P06, P07 |
| T0322-E04 | Mike | spec-review | M0028 | M0029 | Retained | P05 |
| T0324-E01 | Mike | documentation | M0000 | M0003 | Retained | P03, P07 |
| T0324-E02 | Other human | documentation | M0000 | M0004, M0005, M0006 | Retained | P03, P05 |
| T0325-E01 | Mike | review-process | M0000 | M0003 | Retained | P10 |
| T0326-E01 | Mike | repository-workflow | M0000, M0002 | M0003, M0004 | Retained | P03, P06 |
| T0326-E02 | Mike | repository-workflow | M0006 | M0007 | Retained | P04 |
| T0327-E01 | Mike | versioning | M0000, M0002 | M0003 | Retained | P09, P04 |
| T0328-E01 | Mike | review-process | M0000 | M0002, M0003 | Retained | P03, P01, P05 |
| T0329-E01 | Other human | review-process | M0000 | M0002 | Coordination only | - |
| T0331-E01 | Mike | authoring | M0000, M0002, M0004 | M0003, M0005 | Retained | P02, P06, P05 |
| T0332-E01 | Other human | sdk-review | M0002 | M0005 | Retained | P01, P04, P05 |
| T0332-E02 | Other human | sdk-review | M0006 | M0009 | Retained | P03, P01 |
| T0332-E03 | Other human | sdk-review | M0010 | M0011 | Retained | P07, P05 |
| T0332-E04 | Other human | sdk-review | M0012 | M0013 | Retained | P04, P05 |
| T0332-E05 | Other human | review-process | M0015 | M0018 | Retained | P09 |
| T0332-E06 | Other human | sdk-review | M0025 | M0028 | Retained | P02 |
| T0332-E07 | Other human | sdk-review | M0025 | M0029 | Retained | P01, P06 |
| T0332-E08 | Other human | sdk-review | M0025 | M0030 | Retained | P01, P09 |
| T0332-E09 | Other human | repository-workflow | M0031 | M0032 | Retained | P05, P07 |
| T0333-E01 | Other human | authoring | M0002 | M0003 | Retained | P10 |
| T0333-E02 | Other human | review-process | M0004 | M0005 | Retained | P05 |
| T0333-E03 | Mike | repository-workflow | M0004 | M0006 | Coordination only | - |
| T0333-E04 | Other human | authoring | M0009, M0010, M0011, M0012 | M0013 | Retained | P01, P05, P07 |
| T0333-E05 | Other human | authoring | M0014, M0015 | M0016 | Retained | P06, P07, P05 |
| T0333-E06 | Other human | documentation | M0017 | M0018 | Retained | P03, P04 |
| T0333-E07 | Other human | authoring | M0019, M0020 | M0021, M0022 | Retained | P09, P04 |
| T0333-E08 | Other human | review-process | M0023 | M0024 | Retained | P02 |
| T0333-E09 | Other human | authoring | M0030 | M0031 | Retained | P08, P04 |
| T0333-E10 | Other human | authoring | M0033, M0037 | M0038 | Withheld: source support | - |
| T0333-E11 | Other human | documentation | M0043 | M0044, M0045 | Retained | P09 |
| T0333-E12 | Other human | spec-review | M0048, M0049 | M0050 | Retained | P03, P04, P06 |
| T0334-E01 | Mike | review-process | M0000 | M0002 | Retained | P09, P06 |
| T0334-E02 | Mike | versioning | M0003 | M0004 | Retained | P03, P09 |
| T0334-E03 | Other human | review-process | M0005, M0006 | M0007 | Retained | P04, P06 |
| T0334-E04 | Other human | review-process | M0005, M0006 | M0008 | Retained | P01 |
| T0335-E01 | Mike | repository-workflow | M0000 | M0002 | Retained | P09, P05, P10 |
| T0336-E01 | Mike | review-process | M0000 | M0002 | Retained | P01, P05 |
| T0337-E01 | Mike | review-process | M0000 | M0002 | Retained | P06, P09 |
| T0338-E01 | Mike | review-process | M0000 | M0002, M0003 | Retained | P06, P04 |
| T0340-E01 | Mike | spec-review | M0000 | M0002, M0003, M0004, M0006, M0007 | Retained | P03, P01, P02, P05 |
| T0341-E01 | Mike | spec-review | M0000 | M0002, M0003 | Retained | P08 |
| T0343-E01 | Mike | spec-review | M0000 | M0002, M0003, M0004 | Retained | P01, P05, P07, P06 |
| T0344-E01 | Mike | repository-workflow | M0000 | M0002 | Retained | P01, P05, P09 |
| T0345-E01 | Other human | review-process | M0000 | M0002 | Retained | P02, P05 |
| T0347-E01 | Mike | versioning | M0000 | M0003 | Retained | P09 |
| T0347-E02 | Mike | review-process | M0004 | M0005 | Retained | P09, P06 |
| T0347-E03 | Mike | documentation | M0007 | M0008, M0010, M0011 | Retained | P02, P07, P06 |
| T0348-E01 | Mike | review-process | M0000 | M0002 | Retained | P09, P05 |
| T0350-E01 | Mike | spec-review | M0000 | M0001, M0002, M0003 | Retained | P01, P04 |
| T0350-E02 | Mike | spec-review | M0005 | M0006, M0007, M0008, M0010, M0012, M0013 | Retained | P05, P02, P04 |
| T0350-E03 | Mike | spec-review | M0000 | M0016, M0017, M0018 | Retained | P03, P01 |
| T0350-E04 | Mike | spec-review | M0000 | M0019, M0020, M0021, M0023, M0024, M0025, M0026 | Retained | P01, P07 |
| T0350-E05 | Other human | spec-review | M0028 | M0029 | Retained | P08 |
| T0350-E06 | Mike | spec-review | M0032 | M0033 | Retained | P06 |
| T0350-E07 | Mike | spec-review | M0032 | M0035, M0036, M0037, M0038 | Retained | P03, P04 |
| T0350-E08 | Mike | spec-review | M0028 | M0043 | Retained | P07 |
| T0350-E09 | Mike | spec-review | M0045 | M0047 | Coordination only | - |
| T0350-E10 | Mike | spec-review | M0045 | M0049, M0050 | Retained | P08, P01 |
| T0352-E01 | Mike | authoring | M0000 | M0002, M0003, M0004, M0006, M0007, M0008, M0009, M0010 | Retained | P02, P01, P04, P06 |
| T0353-E01 | Mike | repository-workflow | M0000 | M0002 | Retained | P09, P05 |
| T0353-E02 | Mike | sdk-review | M0004 | M0005, M0006 | Retained | P07, P06 |
| T0353-E03 | Mike | review-process | M0004, M0009 | M0008, M0010 | Retained | P03, P04, P02 |
| T0354-E01 | Mike | repository-workflow | M0000 | M0003, M0004 | Retained | P05, P03, P05, P04, P09 |
| T0354-E02 | Mike | auth | M0005 | M0006, M0007 | Retained | P09, P05 |
| T0357-E01 | Mike | spec-review | M0000 | M0002, M0003, M0004 | Retained | P03, P05 |
| T0357-E02 | Mike | spec-review | M0005 | M0006, M0007, M0008, M0010, M0011 | Retained | P01, P07, P02 |
| T0357-E03 | Mike | spec-review | M0013 | M0014, M0015, M0017, M0018, M0019 | Retained | P07, P03, P01 |
| T0357-E04 | Mike | review-process | M0020 | M0021 | Retained | P04 |
| T0358-E01 | Mike | spec-review | M0000 | M0001 | Retained | P07, P05 |
| T0358-E02 | Mike | review-process | M0003 | M0004, M0005, M0007, M0008 | Retained | P05, P08 |
| T0359-E01 | Mike | authoring | M0000 | M0004, M0005, M0006, M0007, M0010, M0013 | Retained | P07, P06, P02, P04 |
| T0359-E02 | Mike | repository-workflow | M0014 | M0015, M0018 | Retained | P06, P10 |
| T0359-E03 | Other human | versioning | M0014 | M0016, M0019 | Retained | P05, P09, P08 |
| T0360-E01 | Mike | versioning | M0002 | M0003 | Retained | P07, P06 |
| T0361-E01 | Mike | spec-review | M0000 | M0003, M0004, M0005, M0006, M0007, M0008, M0009 | Retained | P03, P01, P05 |
| T0361-E02 | Mike | authoring | M0010, M0011 | M0012, M0014 | Retained | P06, P04 |
| T0361-E03 | Mike | authoring | M0013 | M0016, M0017, M0020 | Retained | P02, P09, P07 |
| T0361-E04 | Mike | authoring | M0022, M0023 | M0024 | Retained | P04 |
| T0361-E05 | Mike | authoring | M0025 | M0026, M0029, M0030, M0031, M0033 | Retained | P04, P05, P07 |
| T0362-E01 | Mike | review-process | M0000 | M0001 | Retained | P05 |
| T0362-E02 | Mike | repository-workflow | M0000 | M0002, M0003, M0004 | Retained | P03, P01, P06 |
| T0362-E03 | Mike | documentation | M0005 | M0006, M0007, M0008 | Retained | P02, P06, P07 |
| T0362-E04 | Mike | review-process | M0009 | M0010, M0012, M0014, M0016 | Retained | P07, P02, P04, P06 |
| T0362-E05 | Mike | review-process | M0018 | M0019, M0021, M0022, M0023 | Retained | P02, P01, P04 |
| T0362-E06 | Mike | spec-review | M0024 | M0025, M0026 | Retained | P03, P06 |
| T0362-E07 | Mike | review-process | M0027 | M0028 | Retained | P09, P04, P05 |
| T0362-E09 | Mike | spec-review | M0036 | M0038 | Retained | P04 |
| T0362-E10 | Mike | spec-review | M0039 | M0040, M0041, M0042, M0044 | Retained | P04, P03, P01 |
| T0363-E01 | Mike | spec-review | M0000 | M0001, M0002 | Retained | P07, P05, P08 |
| T0363-E02 | Mike | spec-review | M0003 | M0004 | Retained | P05, P07, P09 |
| T0363-E03 | Mike | spec-review | M0003 | M0005, M0006, M0007 | Retained | P08, P06, P07 |
| T0367-E01 | Mike | repository-workflow | M0000 | M0001 | Retained | P01, P03, P05 |
| T0369-E01 | Mike | review-process | M0000 | M0001 | Retained | P09, P05, P04 |
| T0370-E01 | Mike | versioning | M0000 | M0001, M0004 | Retained | P04, P07, P09 |
| T0371-E01 | Mike | review-process | M0000 | M0001 | Retained | P06 |
| T0372-E01 | Mike | review-process | M0000 | M0001 | Retained | P06, P05, P09 |
| T0373-E01 | Mike | spec-review | M0000 | M0001, M0002, M0003 | Retained | P02, P06 |
| T0373-E02 | Other human | spec-review | M0000 | M0004 | Retained | P05, P03 |
| T0373-E03 | Mike | spec-review | M0005 | M0006 | Retained | P04 |
| T0374-E01 | Other human | repository-workflow | M0000 | M0001 | Retained | P05, P04 |
| T0374-E02 | Other human | authoring | M0002 | M0003 | Retained | P04 |
| T0374-E03 | Other human | authoring | M0004 | M0005 | Retained | P04 |
| T0374-E04 | Mike | documentation | M0000, M0002, M0004 | M0006 | Retained | P06, P03 |
| T0374-E05 | Mike | documentation | M0007 | M0008 | Retained | P09, P03, P02 |
| T0376-E01 | Mike | sdk-review | M0000 | M0001, M0002, M0003 | Retained | P01, P07 |
| T0376-E02 | Other human | sdk-review | M0000 | M0004 | Retained | P05 |
| T0376-E03 | Mike | sdk-review | M0000 | M0005, M0007 | Retained | P06, P02, P03 |
| T0376-E04 | Mike | review-process | M0009 | M0010, M0012 | Retained | P03, P05 |
| T0376-E05 | Mike | review-process | M0014 | M0015 | Retained | P06 |
| T0376-E06 | Mike | review-process | M0014 | M0017 | Retained | P03, P05 |
| T0376-E07 | Other human | repository-workflow | M0018 | M0019 | Retained | P03, P05 |
| T0376-E08 | Mike | auth | M0021 | M0022, M0023, M0025 | Retained | P03, P06, P09 |
| T0377-E01 | Mike | spec-review | M0000 | M0001 | Coordination only | - |
| T0378-E01 | Mike | review-process | M0000 | M0002, M0003 | Retained | P09, P05 |
| T0378-E02 | Mike | review-process | M0004 | M0005, M0006, M0007 | Retained | P03, P04 |
| T0378-E03 | Mike | review-process | M0008 | M0009 | Withheld: source support | - |
| T0378-E04 | Mike | review-process | M0010 | M0011, M0012 | Retained | P03, P01 |
| T0378-E05 | Mike | review-process | M0013 | M0014, M0015, M0016, M0017, M0018, M0019 | Retained | P06, P07, P01 |
| T0378-E06 | Mike | review-process | M0020, M0021, M0022 | M0023, M0024 | Retained | P04, P01 |
| T0378-E07 | Mike | repository-workflow | M0044 | M0045 | Retained | P05 |
| T0378-E08 | Mike | repository-workflow | M0044 | M0047 | Retained | P08, P05 |
| T0379-E01 | Mike | review-process | M0000 | M0001, M0002, M0003 | Retained | P03, P04 |
| T0379-E02 | Mike | review-process | M0004 | M0005 | Retained | P09 |
| T0379-E03 | Mike | review-process | M0004 | M0007, M0008 | Retained | P04, P07 |
| T0380-E01 | Mike | versioning | M0000 | M0001, M0003, M0004, M0006, M0007 | Retained | P02, P01, P03, P06, P07 |
| T0380-E02 | Mike | review-process | M0009 | M0010 | Retained | P06, P09, P04 |
| T0381-E01 | Other human | spec-review | M0000 | M0001 | Retained | P01 |
| T0381-E02 | Mike | repository-workflow | M0000, M0003, M0009, M0013 | M0004, M0005, M0006, M0007, M0008, M0010, M0012, M0014, M0015 | Retained | P02, P01, P03, P06 |
| T0381-E03 | Mike | repository-workflow | M0017 | M0019 | Retained | P05 |
| T0381-E04 | Mike | spec-review | M0020 | M0021 | Retained | P05, P06, P07, P09 |
| T0381-E05 | Other human | repository-workflow | M0017 | M0018 | Retained | P05 |
| T0382-E01 | Mike | auth | M0000 | M0001 | Retained | P03, P01, P05, P09 |
| T0383-E01 | Mike | review-process | M0000 | M0001 | Retained | P09, P04 |
| T0384-E01 | Mike | review-process | M0000 | M0001 | Retained | P03, P01 |
| T0385-E01 | Mike | spec-review | M0000 | M0001, M0002 | Retained | P06, P01 |
| T0386-E01 | Mike | review-process | M0000 | M0001 | Retained | P06 |
| T0387-E01 | Other human | sdk-review | M0000 | M0001 | Retained | P05 |
| T0388-E01 | Mike | repository-workflow | M0000 | M0001, M0002, M0003 | Retained | P09, P06, P01 |
| T0388-E02 | Mike | repository-workflow | M0004 | M0005, M0006, M0008, M0009 | Retained | P05, P02, P07 |
| T0388-E03 | Mike | repository-workflow | M0010 | M0011, M0012, M0013, M0015, M0016, M0017 | Retained | P04, P03 |
| T0389-E01 | Mike | repository-workflow | M0000 | M0001 | Retained | P05, P04 |
| T0390-E01 | Mike | auth | M0000 | M0001 | Retained | P06, P04, P09 |
| T0391-E01 | Mike | sdk-review | M0000 | M0001 | Retained | P06 |
| T0392-E01 | Mike | versioning | M0000 | M0001, M0003, M0004 | Retained | P02, P03, P06 |
| T0392-E02 | Mike | versioning | M0005 | M0006, M0007, M0008, M0009 | Retained | P07, P02, P06 |
| T0392-E03 | Mike | versioning | M0011 | M0012, M0013, M0014, M0016, M0018, M0019, M0021 | Retained | P04, P07, P05 |
| T0393-E01 | Mike | versioning | M0000, M0001 | M0002 | Retained | P04, P05 |
| T0393-E02 | Mike | repository-workflow | M0000, M0001 | M0004, M0005 | Retained | P06, P03, P07 |
| T0394-E01 | Mike | spec-review | M0000 | M0001, M0003 | Retained | P02, P07 |
| T0394-E02 | Mike | spec-review | M0004 | M0005, M0007 | Retained | P05, P03 |
| T0394-E03 | Mike | spec-review | M0006 | M0009, M0011, M0012, M0014, M0015, M0016 | Retained | P01, P08, P05, P07 |
| T0394-E04 | Mike | spec-review | M0018 | M0019 | Retained | P09 |
| T0394-E05 | Mike | spec-review | M0020 | M0021, M0022, M0024, M0025, M0027 | Retained | P02, P06, P01 |
| T0395-E01 | Mike | review-process | M0000 | M0001 | Retained | P02 |
| T0396-E01 | Mike | repository-workflow | M0000 | M0001, M0002 | Retained | P02, P05, P04 |
| T0397-E01 | Mike | versioning | M0000, M0001 | M0002 | Retained | P04, P07, P06, P09 |
| T0397-E02 | Mike | versioning | M0003 | M0005 | Retained | P03, P04, P05 |
| T0397-E03 | Mike | versioning | M0004 | M0006 | Retained | P02 |
| T0397-E04 | Mike | versioning | M0008 | M0009 | Retained | P06 |
| T0398-E01 | Mike | review-process | M0000 | M0001, M0004 | Retained | P09, P03, P01 |
| T0399-E01 | Mike | review-process | M0000 | M0001 | Coordination only | - |
| T0400-E01 | Mike | repository-workflow | M0000, M0001 | M0002 | Retained | P03, P02 |
| T0400-E02 | Mike | repository-workflow | M0003 | M0004 | Retained | P02, P04, P07 |
| T0400-E03 | Mike | review-process | M0005 | M0006, M0007 | Retained | P09, P03 |
| T0400-E04 | Mike | review-process | M0008 | M0009 | Retained | P09, P03 |
| T0401-E01 | Mike | repository-workflow | M0000 | M0001 | Retained | P05, P09, P03 |
| T0401-E02 | Mike | repository-workflow | M0002 | M0003 | Retained | P05, P06, P09 |
| T0402-E01 | Mike | sdk-review | M0000 | M0001 | Retained | P06, P04, P09 |
| T0402-E02 | Mike | documentation | M0003 | M0004 | Retained | P09, P02 |
| T0402-E03 | Other human | documentation | M0003 | M0005 | Retained | P01 |
| T0402-E04 | Other human | sdk-review | M0000 | M0007 | Retained | P01, P08 |
| T0403-E01 | Mike | spec-review | M0000 | M0001 | Retained | P09 |
| T0403-E02 | Mike | review-process | M0002 | M0004 | Retained | P03 |
| T0403-E03 | Mike | review-process | M0006 | M0007 | Retained | P06 |
| T0404-E01 | Other human | review-process | M0000 | M0001 | Retained | P04 |
| T0405-E01 | Mike | versioning | M0000 | M0001 | Retained | P03, P09 |
| T0405-E02 | Mike | documentation | M0005 | M0006 | Withheld: source support | - |
| T0405-E03 | Mike | authoring | M0010 | M0011, M0012 | Retained | P02, P06, P07 |
| T0405-E04 | Other human | authoring | M0010 | M0013 | Retained | P03, P04 |
| T0405-E05 | Mike | sdk-review | M0014 | M0015, M0017, M0018, M0019 | Retained | P01, P07, P02, P06 |
| T0405-E06 | Mike | sdk-review | M0020, M0021 | M0022 | Retained | P10 |
| T0405-E07 | Other human | versioning | M0023 | M0024 | Retained | P04, P09 |
| T0406-E01 | Mike | review-process | M0000 | M0001, M0002 | Retained | P05, P07, P04 |
| T0406-E02 | Mike | review-process | M0004 | M0006, M0007, M0008 | Retained | P01, P05, P04 |
| T0406-E03 | Mike | review-process | M0009 | M0010 | Retained | P04 |
| T0407-E01 | Mike | spec-review | M0000 | M0001 | Retained | P09 |
| T0408-E01 | Mike | review-process | M0000 | M0001, M0002 | Retained | P04, P09, P06 |
| T0409-E01 | Mike | sdk-review | M0000, M0001 | M0002 | Retained | P04, P09 |
| T0409-E02 | Mike | sdk-review | M0000, M0001 | M0003 | Retained | P01, P07 |
| T0409-E03 | Mike | sdk-review | M0000, M0001 | M0004 | Coordination only | - |
| T0409-E04 | Other human | sdk-review | M0000, M0001 | M0005 | Retained | P01 |
| T0409-E05 | Other human | sdk-review | M0006, M0007, M0008 | M0009 | Retained | P03, P04, P06 |
| T0409-E06 | Other human | sdk-review | M0000, M0001 | M0010 | Retained | P01, P09 |
| T0409-E07 | Mike | repository-workflow | M0000, M0001 | M0013 | Retained | P05, P07 |
| T0409-E08 | Other human | sdk-review | M0014 | M0015 | Retained | P05, P04 |
| T0410-E01 | Mike | authoring | M0000 | M0001 | Retained | P03, P06 |
| T0411-E01 | Other human | sdk-review | M0000 | M0001, M0002 | Retained | P05, P02, P04 |
| T0412-E01 | Mike | spec-review | M0000 | M0001, M0002, M0003, M0004, M0005, M0006, M0007 | Retained | P03, P09, P05 |
| T0413-E01 | Other human | documentation | M0000 | M0001 | Retained | P09, P07 |
| T0413-E02 | Mike | documentation | M0000 | M0002 | Retained | P09, P04, P07 |
| T0414-E01 | Mike | sdk-review | M0000 | M0001 | Retained | P09, P01 |
| T0414-E02 | Mike | sdk-review | M0002, M0004, M0011 | M0003, M0005, M0006, M0012, M0013 | Retained | P02, P03, P07, P05 |
| T0414-E03 | Other human | sdk-review | M0007 | M0009 | Retained | P04 |
| T0414-E04 | Other human | sdk-review | M0010, M0015 | M0016, M0017 | Retained | P01, P05, P04 |
| T0414-E05 | Mike | sdk-review | M0002, M0004, M0011 | M0018, M0021, M0022, M0023, M0027 | Retained | P01, P05 |
| T0414-E06 | Other human | sdk-review | M0019 | M0024 | Retained | P05, P04 |
| T0414-E07 | Mike | sdk-review | M0034 | M0035 | Retained | P05 |
| T0415-E01 | Mike | spec-review | M0000 | M0002, M0003, M0004, M0005, M0006, M0007, M0008 | Retained | P05, P01, P09, P07 |
| T0415-E02 | Mike | versioning | M0009 | M0010, M0011, M0012, M0014, M0015, M0016, M0017 | Retained | P03, P04, P06, P05 |
| T0415-E03 | Mike | spec-review | M0018 | M0019 | Retained | P06 |
| T0416-E01 | Mike | spec-review | M0000 | M0001, M0002, M0003, M0004, M0006, M0008, M0010, M0012, M0013, M0014 | Retained | P01, P07, P02 |
| T0416-E02 | Mike | spec-review | M0005, M0007, M0009, M0011 | M0017, M0018, M0019, M0021 | Retained | P03, P01, P08 |
| T0417-E01 | Other human | spec-review | M0000 | M0001, M0002 | Retained | P01 |
| T0418-E01 | Mike | authoring | M0000 | M0001, M0002, M0003 | Retained | P01, P07, P06, P09 |
| T0418-E02 | Other human | authoring | M0000 | M0004, M0005 | Retained | P05, P07 |
| T0418-E03 | Other human | sdk-review | M0006 | M0007 | Retained | P07 |
| T0419-E01 | Mike | versioning | M0000 | M0001, M0002, M0003, M0004, M0006, M0007 | Retained | P01, P03, P02, P06, P05, P04 |
| T0420-E01 | Mike | sdk-review | M0000 | M0002, M0004 | Retained | P02, P03, P07, P04 |
| T0421-E01 | Mike | review-process | M0000 | M0001 | Retained | P09 |
| T0421-E02 | Mike | review-process | M0002 | M0003 | Retained | P09, P04 |
| T0421-E03 | Mike | versioning | M0002 | M0004 | Retained | P03, P04 |
| T0422-E01 | Mike | versioning | M0000 | M0001, M0002 | Retained | P03, P09, P06 |
| T0423-E01 | Other human | review-process | M0000 | M0001 | Retained | P02, P04, P05 |
| T0423-E02 | Mike | review-process | M0000 | M0002 | Retained | P03, P02 |
| T0424-E01 | Mike | repository-workflow | M0000 | M0001, M0002 | Retained | P05, P07 |
| T0424-E02 | Mike | repository-workflow | M0000, M0004, M0005 | M0003, M0006 | Retained | P03, P02, P06 |
| T0424-E03 | Other human | repository-workflow | M0007, M0009 | M0010 | Retained | P06 |
| T0425-E01 | Mike | versioning | M0000 | M0001, M0002, M0003 | Retained | P09, P05, P04 |
| T0426-E01 | Mike | versioning | M0000 | M0001, M0002, M0003, M0004, M0005, M0008, M0011, M0012, M0013, M0017 | Retained | P07, P03, P06, P04 |
| T0426-E02 | Mike | review-process | M0019 | M0020, M0021 | Retained | P06, P03 |
| T0426-E03 | Mike | review-process | M0023 | M0024, M0026 | Retained | P06, P07, P08 |
| T0427-E01 | Mike | review-process | M0000 | M0001 | Retained | P09 |
| T0428-E01 | Mike | versioning | M0000 | M0001, M0003, M0005 | Retained | P02, P03, P04 |
| T0428-E02 | Other human | versioning | M0000 | M0006 | Retained | P04 |
| T0428-E03 | Mike | versioning | M0000 | M0009, M0011 | Retained | P06, P03, P04, P02 |
| T0428-E04 | Mike | review-process | M0012 | M0013 | Retained | P06, P07 |
| T0429-E01 | Other human | spec-review | M0000 | M0004, M0005, M0006 | Retained | P01, P05, P08 |
| T0429-E02 | Mike | spec-review | M0005 | M0007 | Retained | P07, P05 |
| T0429-E03 | Mike | spec-review | M0010 | M0012, M0013, M0014, M0015, M0016, M0018, M0019 | Retained | P01, P02, P03, P08 |
| T0430-E01 | Mike | authoring | M0000 | M0001 | Retained | P01, P03, P06 |
| T0432-E01 | Other human | review-process | M0000 | M0001, M0002 | Retained | P04, P06, P09 |
| T0433-E01 | Mike | versioning | M0000 | M0001, M0003, M0004, M0005 | Retained | P03, P04, P07, P09 |
| T0434-E01 | Mike | spec-review | M0000 | M0001, M0002 | Retained | P02, P04, P05, P07 |
| T0434-E02 | Mike | repository-workflow | M0000 | M0005 | Retained | P06, P02 |
| T0434-E03 | Mike | spec-review | M0000 | M0008 | Retained | P01, P05 |
| T0434-E04 | Mike | spec-review | M0009 | M0011 | Retained | P08, P05 |
| T0434-E05 | Mike | review-process | M0012 | M0013 | Retained | P09, P06 |
| T0435-E01 | Mike | repository-workflow | M0000 | M0001, M0002, M0004 | Retained | P04, P02, P06 |
| T0436-E01 | Other human | review-process | M0000, M0001 | M0002 | Coordination only | - |
| T0437-E01 | Mike | review-process | M0000, M0001 | M0002, M0004 | Retained | P04, P03 |
| T0438-E01 | Mike | review-process | M0000 | M0001 | Retained | P02, P06 |
| T0438-E02 | Mike | documentation | M0000, M0002 | M0003 | Retained | P09 |
| T0439-E01 | Mike | sdk-review | M0000 | M0001, M0002 | Retained | P07, P02, P06 |
| T0440-E01 | Mike | auth | M0000 | M0001, M0002 | Retained | P06, P07, P08 |
| T0441-E01 | Mike | authoring | M0000 | M0001 | Retained | P06 |
| T0442-E01 | Mike | versioning | M0000 | M0001, M0003, M0004, M0005, M0006 | Retained | P02, P04, P05, P07 |
| T0443-E01 | Mike | spec-review | M0000 | M0001 | Retained | P03 |
| T0443-E02 | Mike | spec-review | M0002 | M0003 | Retained | P09 |
| T0443-E03 | Mike | repository-workflow | M0004 | M0005, M0006 | Retained | P06, P09 |
| T0444-E01 | Mike | sdk-review | M0000 | M0001, M0002 | Retained | P03, P04, P06 |
| T0446-E01 | Mike | spec-review | M0000 | M0002, M0003 | Retained | P01, P03, P05 |
| T0446-E02 | Other human | sdk-review | M0000 | M0001 | Retained | P05 |
| T0447-E01 | Mike | versioning | M0000 | M0001 | Retained | P04, P03 |
| T0447-E02 | Mike | documentation | M0002 | M0003 | Retained | P09 |
| T0448-E01 | Mike | review-process | M0000 | M0001 | Retained | P09 |
| T0449-E01 | Mike | auth | M0000 | M0001 | Retained | P05 |
| T0449-E02 | Mike | review-process | M0002 | M0003, M0004 | Retained | P06, P05 |
| T0450-E01 | Mike | review-process | M0000 | M0001 | Retained | P06, P05 |
| T0451-E01 | Mike | versioning | M0000 | M0001, M0002, M0004 | Retained | P03, P06, P01, P02, P07 |
| T0451-E02 | Mike | versioning | M0006 | M0007, M0008 | Retained | P05, P06 |
| T0452-E01 | Other human | documentation | M0000 | M0001 | Retained | P09 |
| T0453-E01 | Mike | spec-review | M0000 | M0001, M0002 | Retained | P01, P05, P06 |
| T0454-E01 | Mike | review-process | M0000 | M0001, M0002 | Retained | P03, P06, P04 |
| T0454-E02 | Mike | review-process | M0003 | M0004 | Retained | P06, P07 |
| T0455-E01 | Mike | auth | M0000 | M0001, M0002, M0003, M0004 | Retained | P01, P09, P05 |
| T0456-E01 | Mike | review-process | M0000 | M0001 | Retained | P09 |
| T0457-E01 | Mike | spec-review | M0000 | M0001 | Retained | P04, P05, P09 |
| T0457-E02 | Mike | spec-review | M0002 | M0003 | Retained | P02 |
| T0457-E03 | Mike | spec-review | M0002 | M0005, M0006 | Retained | P02, P03 |
| T0457-E04 | Mike | spec-review | M0002 | M0008, M0009 | Retained | P02, P07 |
| T0457-E05 | Mike | spec-review | M0002 | M0012, M0013 | Retained | P02 |
| T0457-E06 | Mike | spec-review | M0002 | M0015 | Retained | P01, P05, P07 |
| T0457-E07 | Mike | spec-review | M0016 | M0017, M0018, M0019, M0020 | Retained | P01, P03, P05, P07 |
| T0457-E08 | Mike | review-process | M0021 | M0022, M0023 | Retained | P05, P09 |
| T0457-E09 | Mike | review-process | M0021 | M0025 | Coordination only | - |
| T0457-E10 | Mike | review-process | M0026 | M0027 | Retained | P06 |
| T0458-E01 | Mike | auth | M0000 | M0002 | Retained | P06 |
| T0458-E02 | Other human | authoring | M0000 | M0001 | Retained | P05, P03 |
| T0458-E03 | Other human | authoring | M0003 | M0004 | Retained | P05, P04 |
| T0458-E04 | Other human | sdk-review | M0007 | M0008 | Retained | P07, P05 |
| T0458-E05 | Mike | sdk-review | M0009, M0010 | M0011 | Retained | P09 |
| T0458-E06 | Other human | sdk-review | M0012 | M0013 | Retained | P03, P07, P04 |
| T0458-E07 | Other human | authoring | M0000, M0003, M0007 | M0014 | Retained | P03 |
| T0459-E01 | Mike | authoring | M0000, M0001 | M0004 | Retained | P05 |
| T0459-E02 | Mike | repository-workflow | M0000, M0001 | M0005 | Retained | P01, P05 |
| T0459-E03 | Mike | sdk-review | M0006 | M0007, M0009 | Retained | P02, P06, P07 |
| T0460-E01 | Mike | spec-review | M0000 | M0001 | Retained | P05, P04, P09 |
| T0461-E01 | Mike | documentation | M0000 | M0001 | Retained | P09, P03 |
| T0462-E01 | Mike | review-process | M0000 | M0001 | Retained | P09, P05 |
| T0462-E02 | Mike | review-process | M0003, M0004 | M0005 | Retained | P04 |
| T0463-E01 | Mike | sdk-review | M0000 | M0001, M0003 | Retained | P01, P07 |
| T0463-E02 | Other human | sdk-review | M0000 | M0002, M0004 | Retained | P01, P08 |
| T0463-E03 | Other human | sdk-review | M0000 | M0006 | Retained | P05 |
| T0463-E04 | Mike | review-process | M0007 | M0008 | Retained | P09 |
| T0464-E01 | Mike | auth | M0000 | M0001, M0002 | Retained | P06, P09 |
| T0464-E02 | Mike | repository-workflow | M0003 | M0004 | Retained | P05 |
| T0464-E03 | Mike | auth | M0005 | M0006 | Retained | P09 |
| T0464-E04 | Mike | auth | M0007 | M0008 | Retained | P03 |
| T0464-E05 | Mike | repository-workflow | M0009 | M0010, M0011 | Retained | P05, P07 |
| T0464-E06 | Mike | review-process | M0014 | M0015 | Retained | P09 |
| T0465-E01 | Mike | spec-review | M0000 | M0001 | Retained | P04, P09 |
| T0466-E01 | Mike | review-process | M0000 | M0001 | Retained | P06, P04 |
| T0468-E01 | Other human | auth | M0000 | M0001 | Retained | P09 |
| T0468-E02 | Other human | auth | M0000 | M0002 | Retained | P09 |
| T0469-E01 | Mike | auth | M0000 | M0001 | Retained | P07, P02, P09 |
| T0471-E01 | Mike | authoring | M0000 | M0005 | Retained | P05 |
| T0471-E02 | Mike | spec-review | M0001 | M0002 | Retained | P02, P04, P09 |
| T0471-E03 | Mike | spec-review | M0007 | M0008, M0010 | Retained | P04, P03, P07, P05 |
| T0471-E04 | Mike | review-process | M0012 | M0013 | Retained | P09, P05 |
| T0472-E01 | Mike | review-process | M0000 | M0001 | Coordination only | - |
| T0473-E01 | Mike | versioning | M0000 | M0001 | Retained | P05, P09, P07 |
| T0474-E01 | Mike | documentation | M0000 | M0001 | Retained | P06 |
| T0474-E02 | Other human | documentation | M0000 | M0002 | Retained | P01, P06, P04 |
| T0475-E01 | Mike | spec-review | M0000 | M0003 | Retained | P05, P07 |
| T0475-E02 | Other human | spec-review | M0000 | M0001 | Retained | P06 |
| T0475-E03 | Mike | spec-review | M0000, M0005 | M0004, M0006 | Retained | P02, P01, P07 |
| T0475-E04 | Mike | spec-review | M0007 | M0008 | Retained | P07, P06 |
| T0475-E05 | Mike | spec-review | M0009 | M0010, M0011, M0012, M0013, M0015, M0016 | Retained | P06, P04, P09, P10 |
| T0475-E06 | Mike | spec-review | M0017 | M0018, M0019, M0020 | Retained | P01, P04, P03 |
| T0476-E01 | Mike | versioning | M0000 | M0001, M0002 | Retained | P03, P07, P09 |
| T0476-E02 | Mike | versioning | M0003 | M0004, M0005 | Retained | P09, P04 |
| T0476-E03 | Mike | versioning | M0007 | M0008 | Retained | P04 |
| T0477-E01 | Mike | sdk-review | M0000, M0001 | M0002 | Retained | P09 |
| T0477-E02 | Other human | sdk-review | M0003 | M0004 | Coordination only | - |
| T0478-E01 | Other human | authoring | M0000 | M0001 | Retained | P05 |
| T0478-E02 | Other human | authoring | M0002 | M0003 | Retained | P02, P04, P05, P09 |
| T0480-E01 | Mike | review-process | M0000 | M0001 | Retained | P06, P09 |
| T0481-E01 | Mike | spec-review | M0000 | M0001, M0002, M0003, M0004 | Retained | P01, P03, P04, P05 |
| T0482-E01 | Mike | review-process | M0000 | M0001, M0002, M0003 | Retained | P01, P06, P07, P09 |
| T0482-E02 | Mike | versioning | M0010, M0011 | M0012, M0013 | Retained | P03, P05, P06 |
| T0483-E01 | Mike | spec-review | M0000 | M0001 | Withheld: source support | - |
| T0484-E01 | Mike | authoring | M0000 | M0001 | Retained | P06 |
| T0485-E01 | Mike | repository-workflow | M0000 | M0001, M0005, M0007, M0008 | Retained | P02, P01, P07 |
| T0485-E02 | Mike | spec-review | M0009 | M0011, M0012 | Retained | P03, P05, P04 |
| T0485-E03 | Mike | review-process | M0015 | M0016 | Retained | P10 |
| T0486-E01 | Mike | spec-review | M0000, M0001 | M0002, M0003, M0004, M0006, M0007, M0009 | Retained | P01, P10, P02, P05 |
| T0486-E02 | Mike | spec-review | M0011 | M0012 | Retained | P04 |
| T0486-E03 | Mike | authoring | M0011, M0013, M0014 | M0015, M0016, M0017, M0018, M0019 | Retained | P01, P04 |
| T0486-E04 | Mike | spec-review | M0021 | M0022 | Retained | P01 |
| T0486-E05 | Mike | spec-review | M0023 | M0024, M0025, M0026, M0027, M0028, M0029 | Retained | P01, P03 |
| T0487-E01 | Mike | auth | M0000 | M0001 | Retained | P06 |
| T0488-E01 | Mike | documentation | M0000 | M0001 | Retained | P09, P06 |
| T0489-E01 | Mike | repository-workflow | M0000 | M0001, M0002, M0004 | Retained | P04, P05, P08 |
| T0490-E01 | Mike | spec-review | M0000 | M0004, M0005, M0006 | Retained | P01, P03, P07 |
| T0490-E02 | Mike | spec-review | M0007 | M0008, M0009 | Retained | P03 |
| T0491-E01 | Mike | spec-review | M0000 | M0001, M0002 | Retained | P01, P09 |
| T0492-E01 | Mike | versioning | M0000 | M0001 | Retained | P04, P09 |
| T0492-E02 | Mike | versioning | M0002 | M0003 | Retained | P03 |
| T0492-E03 | Mike | versioning | M0004 | M0005 | Retained | P04 |
| T0492-E04 | Mike | versioning | M0004 | M0007 | Retained | P03 |
| T0492-E05 | Mike | auth | M0008 | M0009 | Retained | P04 |
| T0493-E01 | Mike | documentation | M0000, M0001, M0002 | M0003 | Retained | P06, P09 |
| T0493-E02 | Other human | repository-workflow | M0006, M0007 | M0008 | Retained | P03, P05 |
| T0493-E03 | Mike | repository-workflow | M0006, M0007 | M0010 | Retained | P06, P07 |
| T0493-E04 | Other human | repository-workflow | M0006, M0007 | M0011 | Retained | P03, P04 |
| T0493-E05 | Mike | repository-workflow | M0006, M0007 | M0012 | Coordination only | - |
| T0494-E01 | Mike | review-process | M0000, M0001 | M0002 | Retained | P09 |
| T0495-E01 | Mike | spec-review | M0000 | M0001, M0002 | Retained | P04, P05, P09 |
| T0496-E01 | Mike | spec-review | M0000 | M0001 | Retained | P01, P05, P07 |
| T0497-E01 | Mike | documentation | M0000 | M0001 | Retained | P03, P05, P07 |
| T0497-E02 | Other human | versioning | M0000 | M0002 | Retained | P09, P05 |
| T0498-E01 | Mike | versioning | M0000 | M0001, M0002 | Retained | P09, P10 |
| T0499-E01 | Mike | review-process | M0000, M0002, M0003, M0005 | M0001, M0004, M0006, M0007, M0008 | Retained | P02, P03, P06, P04, P05 |
| T0499-E02 | Other human | repository-workflow | M0012 | M0013 | Retained | P03 |
| T0500-E01 | Mike | sdk-review | M0000 | M0001 | Retained | P06 |
| T0500-E02 | Other human | sdk-review | M0000 | M0002 | Retained | P01, P09 |
