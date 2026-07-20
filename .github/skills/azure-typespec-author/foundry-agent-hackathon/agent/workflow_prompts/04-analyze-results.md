# Step 4 — Analyze the code-quality eval results

You are analyzing **Vally code-quality (forced)** eval results for the
`azure-typespec-author` skill, produced by the ADO benchmark pipeline (id 8178).

## Input

The pipeline artifact content is provided below (concatenated `results.jsonl` / summary
files). Each JSON line is one graded stimulus except the final `type: run-summary` line
(skip it). Per record: `gradeResult.stimulusName` (case name), `gradeResult.passed`,
`gradeResult.details[]` (each grader: `name`, `kind`, `passed`, `score`, `evidence`;
LLM graders also carry `metadata.rubric_scores[]`), `trajectory.output`,
`trajectory.stimulus.tags.suite`.

## Task

1. Compute a **per-case pass rate** across all runs (passes / total), plus a per-suite roll-up.
2. For **every failing case**, give the concrete failure reason from the failing grader(s):
   which grader kind failed, the evidence, and (for LLM graders) which rubric line lost points.
   Distinguish **systemic** (recurs across runs → likely a real gap) from **flaky/one-off**
   (failed in only 1 run → eval noise).
3. For each systemic failure, decide whether the root cause is a **documentation-coverage
   gap** (the skill's `references`/`SKILL.md` don't tell the agent how to do this) versus a
   skill-logic or eval-scoring issue. Justify with evidence.

## Output

Return a structured markdown analysis: a per-case pass-rate table (ascending) + per-suite
roll-up; for each failing case its reason, systemic-vs-flaky, and doc-gap (yes/no + why);
and a short list of candidate documentation gaps tied to the cases they explain.
