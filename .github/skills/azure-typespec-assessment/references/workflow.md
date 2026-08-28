# Complete Read-Only Workflow

## Inputs

Obtain the baseline ref and specification/project root once. Default the repository to the current Git root and the baseline to `origin/main` only when appropriate. Use a work directory that is not assessed source.

The coordinator captures committed, staged, unstaged, and relevant untracked TypeSpec changes; creates service-scoped sparse base/current worktrees; selects one API version per side; compiles each affected project independently with AutoRest and TCGC using that same version pair; runs all three analyzers; and writes the bounded `model-input.json`.

For the head, select the newest newly added API version when one exists; otherwise select its latest API version. When the PR adds no version and that head version exists in base, compile both sides with that same version. When head adds a version, select base's latest stable version, or its latest preview when no stable version exists. Record the pair and selection reasons in the manifest and report.

## Deterministic analysis

Set concrete values, then run:

```powershell
$Repo = (git rev-parse --show-toplevel)
$Base = "origin/main"
$Specification = "<project-or-spec-root>"
$Work = "<work-directory>"
$Skill = Join-Path $Repo ".github\skills\azure-typespec-assessment"

node (Join-Path $Skill "scripts\run-assessment-analysis.mjs") `
  --repo $Repo `
  --base $Base `
  --specification $Specification `
  --output $Work
```

Do not replace this with a full checkout or run the dimension analyzers against different inputs. If there is no changed TypeSpec in scope, stop with the coordinator's no-change result. If every active dimension is blocked, skip Agent judgment and preserve the blocked, `not-assessed` result. If only some dimensions are blocked, judge only the ready items and retain all blocker reasons.

## One Agent judgment

Read only:

- `<work-directory>\model-input.json`;
- [classification guidance](classification.md), including [downstream cases](downstream-breaking-cases.md);
- `scripts\assessment-judgment.schema.json`.

Do not read raw AutoRest/TCGC output, compiler logs, unchanged source, or prior answers. Write `<work-directory>\assessment-judgment.json` with exact coverage of every supplied semantic review unit and candidate.

## Assemble, validate, and render

```powershell
node (Join-Path $Skill "scripts\assemble-assessment.mjs") `
  --work $Work `
  --judgment (Join-Path $Work "assessment-judgment.json") `
  --output (Join-Path $Work "assessment.json")

node (Join-Path $Skill "scripts\validate-assessment.mjs") `
  (Join-Path $Work "assessment.json")

node (Join-Path $Skill "scripts\render-assessment-html.mjs") `
  (Join-Path $Work "assessment.json") `
  (Join-Path $Work "assessment.html")
```

If assembly rejects schema or coverage, send only its compact errors to the same Agent for **one correction turn**. Correct `assessment-judgment.json` only, then rerun the three commands above; do not rerun preparation, compilation, analyzers, or create a second independent judgment. If correction still fails, stop and report the blocker.
