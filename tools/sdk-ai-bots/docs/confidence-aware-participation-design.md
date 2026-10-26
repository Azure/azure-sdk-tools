# Confidence Assessment for Chatbot Answers

**Status:** Draft; evaluation and rollout pending.
**Updated:** 2026-10-26

Related issue: [Azure/azure-sdk-pr#2964](https://github.com/Azure/azure-sdk-pr/issues/2964).
The current scope is backend evidence assessment only, not expert help, channel configuration, frontend presentation or continued participation after humans join.

## 1. Goal

Explain how well the chatbot's answer is supported by the evidence it actually used.
The answering agent supplies five aspect values and brief reasons alongside its answer in the same final generation.
Application code derives a provisional confidence band from those values.
This is not an independently verified correctness grade or a calibrated probability.

## 2. Design

### 2.1 Overview

Keep the normal answering workflow, including tenant instructions, skills, tools, memory, conversation handling and persistence.
Assessment is always enabled through the hosted agent's native structured response format and a static Markdown instruction file.
There is no second assessor call, mode switch, alternate backend route or provider-input capture middleware.

```text
Normal retrieval and answering
  -> complete answer + five aspect values/reasons in one final generation
  -> backend validates and extracts the visible answer
  -> application derives and logs the provisional confidence band
```

The aspect results do not decide whether to answer, change intention rules or authorize notifications.
The normal frontend continues to receive the processed answer and references through the existing API.
Assessment details remain in the stored hosted response for offline inspection and future evaluation reporting.

### 2.2 Evidence aspects

Assess Support, Fit, Source quality and Consistency against important claims; assess Coverage against the full request.
An important claim can change the diagnosis, recommendation, next step or expected result.
The aspect-specific definitions below are the source of truth.
Within each aspect, apply Fail before Limited before Pass; do not average claims.

| Aspect | Pass | Limited | Fail |
| --- | --- | --- | --- |
| **Support** | Supplied evidence establishes the important claims, directly or by sound deduction with all premises supported; claimed current states or completed actions have sufficiently recent observations for the correct target. | Evidence establishes the main conclusion, but a disclosed, evidence-bounded estimate or extrapolation leaves uncertainty in a non-decisive detail; no important claim relies on an unsupported premise. | An important asserted claim lacks evidence, contradicts it, or depends on an unsupported premise; a claimed current state or completed action is unverified. Documentation alone cannot establish live state or completion. |
| **Fit** | The main answer and recommendations apply to the user's stated situation at the level of specificity requested. Supplementary information is appropriately scoped. | The guidance is relevant and useful, but an identifiable applicability gap meaningfully limits how confidently the user can apply part of it. | The main guidance applies to the wrong situation, or a missing condition prevents determining whether the main recommendation is appropriate. |
| **Source quality** | Supplied provenance establishes trustworthy sources with the authority required for each important claim; no relevant provenance or authority limitation remains. | An identified, attributable secondary source meets the minimum authority needed for the guidance, but lack of first-party confirmation leaves a disclosed, non-decisive authority limitation. | An important claim relies on an unreliable or unattributable source, or lacks required authority; secondary guidance cannot substitute for an authorized policy decision or approval. |
| **Consistency** | No relevant disagreement remains after evidence-backed reconciliation of scope, time and authority. Coexisting requirement and approval labels, or reviews of different commits, are not conflicts by themselves. | A disclosed disagreement remains in a separable, non-decisive detail, but all supported interpretations yield the same main diagnosis, recommendation and required next step. | An unresolved disagreement could change an important claim, the main diagnosis, recommendation or required next step; choosing one source without an evidence-backed reason does not resolve the conflict. |
| **Coverage** | The answer resolves the main concern and any explicitly required outcomes. Missing optional details or additional relevant information do not reduce the rating. | The main concern is substantially addressed, but a material requested part remains unresolved and needs follow-up. | The main concern remains unresolved, or a missing essential part prevents the answer from satisfying the request. |

#### Aspect boundaries

**Support asks what the evidence establishes; Fit asks where it applies.**
Correctly quoting an authoritative Java-only procedure can Pass Support and Source quality but Fail Fit for a Python request.
Missing language, version or other context is not automatically Limited: Fit Fails when the missing condition prevents determining whether the main recommendation is appropriate.
Sound deductions with supported premises can Pass; hedging does not repair an unsupported important claim.

**Source quality checks authority, not agreement.**
An attributable secondary explanation can support non-authorizing guidance, but cannot grant an exception or substitute for a required approval.
A first-party URL alone does not establish the author or their authority.
Evaluate live state under Support, temporal applicability under Fit, and provenance under Source quality.

**Consistency checks residual disagreement, not merely different labels, dates or wording.**
Compare evidence about the same target, scope and relevant time.
Requirement and approval labels can coexist, and reviews of different commits are not conflicts by themselves.
Evidence-backed reconciliation can resolve an apparent conflict completely and therefore Pass.
One inspected source can Pass Consistency when no disagreement is present; this does not establish Support or Source quality.

**Coverage checks the main concern and required outcomes.**
Relevant supplementary information or a missing optional example does not reduce the rating.
Useful surrounding guidance does not resolve a missing essential answer.
The same gap can affect multiple aspects: inventing a requested result can Fail both Support and Coverage, while acknowledging the missing result can preserve Support but still Fail Coverage.

A failed tool call establishes neither the requested fact nor its negation.
Assess whether other gathered evidence fills the gap; the failure alone does not force every aspect to Fail.
Reserve `not_applicable` for a genuinely irrelevant aspect, never missing evidence.
Weak evidence can Fail; use a separate abstention only when assessment cannot be performed.
Provider or transport failures are unavailable assessments, not evidence Failures.

### 2.3 Evidence bands

The confidence band summarizes evidence sufficiency, not correctness probability.
The current policy conservatively counts every applicable aspect; no separate centrality classifier is implemented.

| Aspect results | Provisional band |
| --- | --- |
| At least one applicable Fail | **Low** |
| No Fail and at least one Limited | **Medium** |
| At least one Pass and all remaining aspects Pass or Not applicable | **High** |
| All aspects Not applicable, or assessment abstained | No band |

Reasons explain the aspect values but do not alter this deterministic policy.
A band does not select a response strategy, classify expert need or trigger a notification.

### 2.4 Structured generation

The current schema is `confidence-runtime-v3` and the rubric is `evidence-v3`.
The required generated fields are `schema_version`, `rubric_version`, `answer`, `status` and `aspects`.
The complete answer, normal formatting and References section remain inside `answer`.

A valid assessment contains all five aspect objects:

```json
{
  "schema_version": "confidence-runtime-v3",
  "rubric_version": "evidence-v3",
  "answer": "Complete answer with normal formatting and references",
  "status": "valid",
  "aspects": {
    "support": {"value": "pass", "reason": "The inspected evidence establishes the main claims."},
    "fit": {"value": "pass", "reason": "The guidance applies to the stated environment."},
    "source_quality": {"value": "pass", "reason": "The supplied provenance establishes the required authority."},
    "consistency": {"value": "pass", "reason": "No relevant disagreement remains in the inspected evidence."},
    "coverage": {"value": "pass", "reason": "The answer addresses the main concern and requested outcomes."}
  }
}
```

This example illustrates the schema, not a real or independently validated result.
Every `value` is `pass`, `limited`, `fail` or `not_applicable`.
Each `reason` is one short, nonblank, single-line sentence identifying the decisive evidence or remaining gap, not a reasoning trace.
For `not_applicable`, the reason explains why the aspect is irrelevant.
An abstained assessment has `status: abstained`, `aspects: null` and no derived band, while retaining an answer.

The schema forbids extra fields.
Missing aspects, blank or multiline reasons and inconsistent status fail validation explicitly.
There is no model-generated overall band, expert-help metadata or response-routing decision.
Historical experiment results remain unchanged and are not reclassified or supplied with retrospective reasons.

### 2.5 Backend integration

The static prompt is [`agents/chat_agent/evidence_assessment.md`](../azure-sdk-qa-bot-agent/agents/chat_agent/evidence_assessment.md), loaded with the same helper as the main `instruction.md`.
`GeneratedAssessmentAnswer` defines the native structured response format.
`services/evidence_assessment_service.py` derives the provisional band without another model call.

`ChatService._postprocess` validates recognized current-schema output and extracts `answer` before normal citation/reference processing.
It logs the assessment and derived band against the hosted response ID.
Malformed current assessments are logged and rejected instead of silently receiving invented defaults.
Plain output from older deployments retains the original postprocessing behavior.

The public `ChatRequest` is unchanged; `ChatResponse` adds an optional `assessment` containing `schema_version`, `rubric_version`, the original `status`, five `aspects` and the application-derived `provisional_band`, without another copy of `answer`.
Both `/completion` and `/agent/chat` expose these fields alongside the processed answer and references; requested `full_context` remains the document-context JSON list used by the evaluation pipeline.
Responses without assessment output return `assessment: null`.
Abstention retains `status: abstained` with null aspects and band, while all-N/A remains `valid` with all five value/reason pairs and a null band.
The backend persists the visible answer rather than the generated assessment envelope.
Stored hosted responses still retain the original structured output for inspection against the response ID.

No channel setting, confidence-format conversation flag, notification reservation, frontend label or new participation rule is required.

## 3. Evaluation

Evaluate answering-agent assessment (option A) before considering a separate assessor (option B).
Use independent reviewer judgments to assess aspect values and whether each reason identifies the actual evidence or gap.
Model agreement, plausible explanations and High bands are not proof of correctness.
Keep historical saved runs unchanged when prompts, questions or schemas evolve.

The reporting target is the [offline evaluation pipeline, definition 7918](https://dev.azure.com/azure-sdk/internal/_build?definitionId=7918&_a=summary), configured by `tools/sdk-ai-bots/offline-evaluation.yml`.
That pipeline enables the shared runner's opt-in `--assessment_report` with `--cache_result full`; perf and online pipelines are unchanged.
The pipeline can collect the processed answer and assessment directly through the API without retrieving the hosted response for assessment fields or changing document context or grader inputs.
The published cache artifact includes `assessment/<scenario>/<unique-invocation-id>/report.md` and companion `results.json`, with the question, complete visible answer, five values/reasons, provisional band, assessment status, schema/rubric versions and explicit unavailable/error states.
An input-index case ID keeps metadata associated with the correct case when titles repeat or grading results arrive out of order.
Only that opaque identifier is added to report-enabled inline evaluation items; assessments stay local and do not enter grader prompts.
Checkpoints and finalization preserve partial diagnostics without converting collection, retrieval or grading failures into success.
Existing external grades and baseline gates remain separate from the answering model's self-assessment.

Offline evaluation starts a local backend but invokes a deployed hosted answering agent.
Testing the new generation schema therefore requires a compatible hosted version and matching response-retrieval access.
Deployment and live evaluation require separate authorization and are not part of this change.

## 4. Non-goals

- Expert-help classification, mentions, notification thresholds or delivery policy.
- Bot configuration, frontend rendering or channel-specific confidence labels.
- Changed intention, participation, conversation storage or human-thread synchronization.
- Response routing or an automatic choice between answering, clarifying and escalation.
- A separate evaluator, probability estimates, custom input capture or option-B runtime scaffolding.

## 5. References

- [Tian et al., verbalized confidence](https://arxiv.org/abs/2305.14975) and [Xiong et al., uncertainty expression](https://arxiv.org/abs/2306.13063) motivate evaluating self-reported confidence rather than trusting it.
- [ALCE](https://arxiv.org/abs/2305.14627) separates answer correctness from citation support.
- [Selective QA under domain shift](https://aclanthology.org/2020.acl-main.503/) motivates evaluating risk and coverage.
- [Microsoft-Decision-1](https://learn.microsoft.com/en-us/azure/foundry/foundry-models/how-to/use-foundry-models-microsoft-decision) documents the model considered for a deferred separate-assessor experiment, not part of this runtime.
