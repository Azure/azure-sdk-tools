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
