# Isolated candidate evaluation

These helpers exercise experimental reasoning/source profiles on a branch-local
backend. They do not update App Configuration, the deployed bot, official datasets,
or hosted-agent definitions. The research profiles pin hosted-agent version 267.
They are research prototypes, not production policy.

The existing `offline-evaluation.yml` keeps its normal behavior when
`researchProfile` is empty. To evaluate a candidate, queue that pipeline on the
candidate branch with `scenarios: [apispec]`, a selected `researchProfile`,
`researchSnapshot`, and `researchDataset` template parameters.

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
