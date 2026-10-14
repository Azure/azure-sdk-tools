# Isolated candidate evaluation

These helpers exercise experimental reasoning/source profiles on a branch-local
backend. They do not update App Configuration, the deployed bot, official datasets,
or hosted-agent definitions. The research profiles pin hosted-agent version 267.
They are research prototypes, not production policy.

The existing `offline-evaluation.yml` keeps its normal behavior when
`researchProfile` is empty. To evaluate a candidate, queue that pipeline on the
candidate branch with `scenarios: [apispec]`, a selected `researchProfile`,
`researchSnapshot`, and `researchDataset` template parameters.

For this pipeline's Runs REST API, send the template parameter values as strings;
JSON-encode the object/array values. Native nested JSON values were silently
ignored in qualification, including the profile selection. Before queuing, verify
the preview contains the selected `QA_RESEARCH_PROFILE`, the full decoded snapshot
and case array, the API-spec job only, and the research holdout step. After queuing,
verify the build's recorded template parameters and source commit as well.

`researchSnapshot` is a source-reviewed, frozen snapshot object containing
`captured_utc`, `source_revision`, and `contract`. The contract includes the
configuration facts and five SDK-tools source paths/URLs/SHA-256 checksums.
The server downloads the exact public source commit and rejects checksum
mismatches. This checks integrity; it does not independently validate metadata,
policy applicability, reviewer authority, or future freshness. The supplied
metadata must have been verified against its primary sources before testing.

`researchDataset` is an array of canonical source-reviewed evaluation rows.
The API-spec job runs the unchanged basic dataset and a separate frozen research
dataset through the existing `evals_run.py`, using actual returned evidence.
Both publish normal cache artifacts and online Foundry reports. The research
dataset is not uploaded as an official dataset asset.

Local answer generation plus online Foundry grading is **not** an Azure DevOps
pipeline run. Pipeline validation requires a completed run on the candidate
commit, the expected profile/evidence hashes in the server log, and complete
result artifacts. Grader scores do not substitute for source-based correctness
checks or authorize production deployment.

## First-pass bot-code candidates

Set `researchProfile: baseline`, `decisionAgentVersion` to the frozen hosted-agent
version, and `decisionMode` to `evidence_only`, `quoted`, `role_bound`, or
`extractive`. For the unchanged-bot arm, leave `decisionMode` empty but retain the
same `decisionAgentVersion`. Keep `researchSnapshot` empty for these modes:
evidence is acquired automatically by the actual bot service, not supplied as a
manually compiled source card. Do not combine these modes with the older
source-card candidates.

The candidate implementation is in the agent's `services/decision_context.py`
and normal `ChatService` execution path. These settings affect only the
branch-local server; they do not modify shared App Configuration or deploy an
agent. Candidate JSON is rendered once, with explicit rejection notices or
validation errors, not an answer-repair loop. Quote validity is not semantic
entailment, and model-produced roles/conditions are not verified observations.

With `decisionAgentVersion` set, CI also runs `research/audit_inputs.py` under
the server's authorized pipeline identity. It verifies recorded questions and
response identities, actual input roles and evidence delivery, and exact answer
rendering. Missing identities or unavailable response access fail this audit;
the script does not impersonate another identity or substitute source sidecars.

## Evidence-contract development screening

The October 14 research round froze three actual bot-code candidates before
generating answers: quote-bound claims (`quoted`), quote-and-role-bound claims
(`role_bound`), and quotation-only output (`extractive`). Baseline and automatic
evidence retrieval without the contract (`evidence_only`) were controls. Each
arm answered the same 15 development questions, using the existing hosted agent,
evaluation model, grading rules, weights, and threshold.

| Arm | Source-supported and adequate | Automated combined passes |
| --- | ---: | ---: |
| Baseline | 10/15 | 14/15 |
| Evidence-only control | 9/15 | 14/15 |
| Quoted | 8/15 | 12/15 |
| Role-bound | 9/15 | 11/15 |
| Extractive | 9/15 | 10/15 |

The source-supported labels are coordinator judgments after reading every
answer and checking material claims, policy scope, and recommended actions;
they are not independently adjudicated expert labels or population accuracy
estimates. The cases are new synthetic counterfactuals from previously studied
topic families, not independent unused historical-topic holdouts.

No candidate met the frozen baseline adequacy floor, so none advanced.
All 30 reserved validation cases remain unexposed. Role-bound was selected only
for regression and CI integration probes, not as a finalist or accepted change.
All 75 development answers were independently reconciled with their actual
provider inputs, rendered outputs, returned evidence, and grounding-grader
inputs. This establishes provenance, not correctness.

The negative result distinguishes two limitations: genuine quotations can be
applied to the wrong object, workflow, or authority; stricter filtering can also
withhold useful conclusions or ask for facts already supplied. Passing quote
and role checks therefore does not establish current policy applicability.
These modes remain opt-in research prototypes and are not recommended for
production.
