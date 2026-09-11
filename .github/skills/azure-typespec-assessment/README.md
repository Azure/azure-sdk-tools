# Azure TypeSpec Assessment

Review TypeSpec changes locally before opening a PR. The skill combines source
analysis, compiled REST/SDK evidence, and Agent judgment into a browsable report.
It also works on an existing PR when run against an isolated checkout of that
PR's changes.

This is an opt-in, read-only assessment, not an API approval or a code-fixing
workflow. It does not invoke or run automatically from `azure-typespec-author`.

## What it assesses

| Dimension | Purpose |
| --- | --- |
| Semantic understanding | Explain the intent of the changed TypeSpec declarations. |
| REST breaking changes | Identify incompatible REST contracts using AutoRest output. |
| Downstream SDK breaking changes | Identify caller-visible SDK incompatibilities using language-neutral TCGC evidence and explicit candidate rules. |
| Azure Guidelines | Compare each semantic intent with relevant, fetched official guidance. |
| Document Quality and Agent Friendliness | Check eligible `@doc` descriptions for correctness against their declarations and meaningful explanations. |

The overall safety result covers **REST and downstream SDK compatibility only**.
Guidelines and documentation have separate findings and coverage. Missing
evidence is reported as a limitation or `not-assessed`, not silently treated as
a pass. SDK findings do not claim that every language generates the same API.

## Prerequisites

- A coding agent that can read skills, run local commands, and fetch official
  documentation from the web.
- Git, Node.js (22 recommended), and npm.
- A local TypeSpec repository with the baseline Git ref available and the
  project's `package.json`, `package-lock.json`, and `tspconfig.yaml`.
- Access to the project's dependencies and enough disk space for isolated
  worktrees, compiler output, and a dependency cache.

Make the **complete skill directory** available to the agent, including
`SKILL.md`, `references`, and `scripts`. It can live under the assessed
repository's `.github\skills` directory, or in a separate Azure SDK Tools
checkout referenced explicitly in your prompt. Do not copy only `SKILL.md`.

Preparation reuses a compatible dependency installation when possible;
otherwise it installs the locked dependencies in the assessment work directory.
It does not require you to install a separate global TypeSpec compiler.

## Recommended: assess local changes

Work in your TypeSpec repository and ask the agent to use the skill. Provide:

1. The repository path.
2. The baseline ref, usually `origin/main` when that is the intended target.
3. The TypeSpec project or specification directory to assess.
4. A separate output/work directory, outside the assessed source scope.

Example prompt; replace the paths and baseline for your change:

```text
Use azure-typespec-assessment from
C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-assessment.

Assess the TypeSpec changes in C:\workspace\azure-rest-api-specs,
scoped to specification\cognitiveservices\CognitiveServices.Management,
against origin/main.
Write the assessment under C:\temp\typespec-assessment-local.
Do not change my source files, branch, or index.
Complete the Agent judgment and produce assessment.json and assessment.html.
```

The comparison includes changes from the baseline's merge base through `HEAD`,
plus staged, unstaged, and relevant untracked TypeSpec changes. You do not need
to commit or open a PR first. Make sure the baseline ref is current before
starting; a different baseline can change the result.

The agent prepares and compiles the affected projects, reviews the bounded
evidence, fetches Azure Guidelines, and assembles the final report. Preparation
can take longer on the first run or for large projects.

## Assess an existing PR

Local development is the primary experience, but you can supply a PR URL:

```text
Use azure-typespec-assessment to assess
https://github.com/Azure/azure-rest-api-specs/pull/42435.

Use a clean, isolated checkout of the PR head and its actual target baseline.
Determine the affected TypeSpec project scope from the PR.
Keep my current checkout unchanged.
Write the work artifacts and final JSON/HTML report under
C:\temp\typespec-assessment-pr-42435.
```

The agent needs GitHub/source access and must establish the correct head,
baseline, and project scope. A PR URL does not bypass compilation or Agent
judgment. Do not assess a PR from an unrelated or dirty checkout whose local
changes would contaminate the comparison.

## Read the results

Open `<work-directory>\assessment.html` in a browser. The report shows semantic
intents, linked findings, before/after contracts, source evidence, and limitations.
Downstream cards group findings by affected SDK method, with nested type changes
and short explanations of the caller impact. Finding counts, affected-method
counts, and changed-type counts are different measures.

Keep `assessment.json` for structured results. Retain the work directory when
you need the input evidence, judgments, provenance, or reproducible rendering.
For precise method-to-type paths, render with the matching explicit
`--downstream-input` artifact as described in the
[workflow](references/workflow.md).

Documentation checks assess existing, nonempty target `@doc`, including
descriptions that become stale when their declarations change. They do not
audit missing documentation, unchanged unrelated declarations, or actual agent
execution.

## Advanced usage and historical reports

The [complete workflow](references/workflow.md) documents the preparation,
assembly, validation, and rendering commands. **Running the Node.js analysis
script alone is not a complete assessment:** it does not call an LLM. The agent
must supply the required judgments and guidance evidence before final assembly.

The [12 published historical PR reports](https://wonderful-coast-0b5cc5a00.3.azurestaticapps.net)
illustrate the report experience; they are not live assessments of those PRs.
Older reports may show documentation as `not-assessed` because they predate
that dimension's implementation.

See the [evaluation README](evals/README.md) to replay the accepted 12 reports
or run the historical pilot. Replay validates and renders existing assessments;
it does not perform fresh compilation, guidance searches, or Agent judgment.

## Design and implementation

- [Design proposal in Azure SDK Tools](https://github.com/Azure/azure-sdk-tools/blob/main/tools/azsdk-cli/docs/specs/typespec-assessment.spec.md)
- [Current implementation design](design.md)
- [Skill implementation PR](https://github.com/Azure/azure-sdk-tools/pull/16751)
- [Downstream candidate rules](references/downstream-candidate-rules.md)
