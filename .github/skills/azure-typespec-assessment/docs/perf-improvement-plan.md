# Plan: Eliminate PR Assessment Setup Overhead

## Problem and approach

PR assessment currently requires the agent to resolve PR metadata, fetch missing
commits, create a detached full worktree, and manually search for the TypeSpec
project before it can invoke `run-assessment-analysis.mjs`. This duplicates the
coordinator's sparse-worktree behavior and caused approximately 18 minutes of
setup overhead in session `0e7d3e43-4386-4e6b-b330-5c5e995dc6f7`.

Add a PR-aware and explicit-commit mode directly to the production assessment
coordinator. The coordinator will resolve/fetch only the required refs, derive
the TypeSpec scope from the Git diff, and create its existing sparse analysis
worktrees without first materializing a separate full PR-head checkout. Preserve
the current local-working-tree mode unchanged.

## Implementation result

Implemented and validated on PR 43718:

- PR URL/number and explicit base/head coordinator modes;
- direct missing-ref fetch and merge-base resolution without changing checkout;
- automatic changed TypeSpec service-root discovery;
- explicit-head comparison without local working-tree overlays;
- direct-first workflow guidance for PR and local assessment;
- setup phase telemetry and regression coverage;
- 29.9-second setup excluding fetch, replacing the observed 18m 51s
  pre-coordinator delay.

## Required workflow behavior

For every assessment mode, the coordinator is the first operational command.
The agent must not perform its own repository inspection, metadata lookup,
changed-file discovery, dependency preflight, worktree creation, or project
search before invoking it.

For a PR assessment, loading the skill instructions is followed immediately by:

```powershell
node <skill>\scripts\run-assessment-analysis.mjs `
  --pr <pull-request-url-or-number> `
  --repo <local-repository> `
  --output <work-directory>
```

For a local assessment whose baseline and specification were supplied in the
request, loading the skill instructions is followed immediately by:

```powershell
node <skill>\scripts\run-assessment-analysis.mjs `
  --repo <local-repository> `
  --base <baseline-ref-or-commit> `
  --specification <project-or-spec-root> `
  --output <work-directory>
```

If a local request omits the baseline, the required baseline question is the
only action allowed before the command. Once the user confirms the baseline,
the coordinator invocation must be the next assessment action.

PR metadata resolution, missing-ref fetching, merge-base calculation, local Git
state capture, changed-TypeSpec discovery, dependency preflight, project scope
discovery, and sparse workspace creation belong to the coordinator.

The agent may run a separate diagnostic command only after the coordinator
returns an explicit blocker that cannot be resolved internally. Diagnostics
must address that blocker rather than repeat normal coordinator preparation.

## Todos

1. **Define the PR and commit input contract**
   - Extend `run-assessment-analysis.mjs` with mutually exclusive modes:
     - local mode: existing `--repo`, `--base`, and required
       `--specification`;
     - PR mode: `--pr <URL|owner/repo#number|number>` with optional
       `--specification`;
     - commit mode: `--base <sha-or-ref> --head <sha-or-ref>` with optional
       `--specification`.
   - Resolve a numeric PR against the local repository's `origin`.
   - Reject ambiguous or conflicting combinations with explicit errors.
   - Keep existing CLI invocations backward compatible.

2. **Generalize comparison logic beyond local `HEAD`**
   - Update `git-evidence.mjs` and `prepare-assessment.mjs` to accept an
     explicit head ref/commit instead of hardcoding `HEAD`.
   - Preserve committed/staged/unstaged/untracked overlays only in local mode.
   - In PR/commit mode, compare immutable base/head commits and report a clean
     working-tree provenance record.
   - Use the sparse head worktree, rather than the caller's current checkout,
     for project discovery and external-import validation.

3. **Implement sparse PR fetching in production code**
   - Implement filtered, shallow fetch/deepen behavior in a production module
     under `scripts/`; do not import, move, or promote code from `evals/` or
     test files.
   - For PR mode, use one authenticated `gh api` metadata request to obtain the
     canonical repository, PR URL, base SHA, and head SHA.
   - Check local object availability before fetching.
   - Fetch only the missing PR/base refs with `--filter=blob:none`,
     `--depth=1`, `--no-tags`, and `--no-write-fetch-head`; deepen only when
     required to calculate the merge base.
   - Store fetched refs under assessment-specific namespaces and avoid changing
     the user's branch, index, or working tree.
   - For explicit commit mode, reuse local objects first and fetch the named
     refs/commits only when missing.

4. **Derive assessment scope without recursive filesystem searches**
   - Compute changed TypeSpec paths from the resolved merge base and head.
   - Derive distinct `specification/<service>` sparse roots directly from those
     paths.
   - Find affected project roots by walking each changed path's ancestors and
     checking `tspconfig.yaml` in the Git tree or sparse head workspace.
   - If `--specification` is supplied, validate and restrict the derived scope;
     otherwise assess all changed TypeSpec projects in the PR/commit comparison.
   - Return the existing no-change result when no TypeSpec files are in scope.

5. **Wire one-command execution and performance telemetry**
   - Have the CLI resolve inputs and immediately call the existing deterministic
     analysis pipeline; do not create an outer full worktree.
   - Record separate timings for PR metadata, local object checks, network fetch,
     merge-base resolution, scope discovery, sparse workspace creation, and
     deterministic analysis.
   - Add a `setupExcludingFetchMs` metric and retain fetch time separately so
     the under-60-second target is measurable.
   - Record PR/commit provenance in the preparation manifest and final report
     without changing compatibility judgments.

6. **Update skill instructions and user documentation**
   - Change `SKILL.md` and `references/workflow.md` to require the coordinator
     as the first operational command for PR, explicit-commit, and local
     assessments.
   - Preserve the local baseline-confirmation gate: when the baseline is
     missing, asking for it is the only permitted pre-command action.
   - State that the coordinator owns PR metadata resolution, ref fetching,
     merge-base calculation, local Git state capture, changed-file discovery,
     dependency preflight, TypeSpec project discovery, and sparse workspace
     creation.
   - Explicitly prohibit manual `git status`, full worktree creation, recursive
     globbing, dependency checks, and duplicate PR metadata or changed-file
     discovery before invoking the coordinator.
   - Permit separate diagnostics only when the coordinator reports a concrete
     blocker, and require diagnostics to remain scoped to that blocker.
   - Update `README.md`, `docs/design.md`, and the performance findings with the
     new CLI examples, prerequisites, timing definitions, and expected setup
     behavior.
   - Integrate with the current uncommitted documentation and schema work in the
     skill directory rather than replacing it.

7. **Add correctness and performance regression coverage**
   - Add unit tests for PR identifier parsing, origin inference, argument
     conflicts, missing-object fetches, pull-ref fallback, shallow-history
     deepening, and explicit base/head mode.
   - Extend Git evidence tests to prove an explicit head is assessed while the
     current checkout and working tree remain untouched.
   - Add integration fixtures proving multi-service PR scope discovery creates
     only the required sparse roots and invokes analysis once.
   - Add a hermetic setup benchmark with mocked network execution asserting
     `setupExcludingFetchMs < 60000` and no full checkout or recursive search.
   - Add skill capability evals verifying both PR and local requests invoke the
     coordinator directly instead of issuing separate metadata, repository,
     worktree, dependency, or scope-discovery commands.
   - Add workflow-order assertions that fail if `gh pr`, `gh api`, `git
status`, `git diff`, `git fetch`, `git worktree`, `glob`, dependency
     preflight, or equivalent preparation occurs before the coordinator
     command. For a missing local baseline, allow only the baseline question
     before invocation.

8. **Validate the completed change**
   - Run the focused Node test files for CLI, Git evidence, PR resolution,
     coordinator analysis, and sparse workspace behavior.
   - Run the assessment skill's deterministic E2E tests and the new PR
     capability/performance eval.
   - Run formatting and `vally lint azure-typespec-assessment`.
   - Perform one real PR smoke assessment and confirm the manifest records
     under 60 seconds of setup excluding network fetch time.

## Notes and considerations

- Code under `evals/` and test files is never used by the production workflow.
  It may be consulted only to understand previously tested behavior; production
  implementation must live independently under `scripts/`.
- Tests should exercise the production modules through their public exports
  rather than supply runtime helpers or become production dependencies.
- `gh` is used only to resolve PR metadata and authenticated repository access;
  explicit base/head mode remains usable without GitHub API access.
- Network duration is excluded from the setup target but remains visible in
  telemetry. Compilation and Agent judgment are outside this plan's 18-minute
  setup-overhead target.
- No new npm dependency is expected; use Node built-ins plus existing `git` and
  `gh` prerequisites.
