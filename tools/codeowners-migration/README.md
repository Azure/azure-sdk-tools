# CODEOWNERS Migration Utilities

One-time tooling for migrating a repository from a hand-maintained `.github/CODEOWNERS` file to the
YAML ownership model described in
[8-operations-codeowners-management.spec.md](../azsdk-cli/docs/specs/8-operations-codeowners-management.spec.md).

> **Status:** design only. This document specifies the tool; no code exists yet.

## Purpose

Migration needs two capabilities that the steady-state `azsdk-cli` command surface deliberately does
not have:

- producing the first draft of the YAML ownership files from an existing CODEOWNERS file
- proving that the rendered output did not change who owns anything

Both are one-time aids with no role after a repository has converted.

### Why these are not in `azsdk-cli`

Keeping them out of `azsdk-cli` is a deliberate boundary.

The whole point of the ownership design is that `azsdk-cli` renders and validates but **never authors
ownership**. A `convert` verb inside it would reintroduce the tool-writes-ownership path that the new
command surface removes. `verify` compares two CODEOWNERS files, which is meaningless once the old
file is gone.

Both verbs reference `Azure.Sdk.Tools.CodeownersUtils` directly, so neither needs anything from
`azsdk-cli`. After the last repository migrates, this directory is deleted.

## Layout

A standalone .NET tool with two verbs:

```
tools/codeowners-migration/
├── README.md
├── CodeownersMigration.sln
├── Azure.Sdk.Tools.CodeownersMigration/
└── Azure.Sdk.Tools.CodeownersMigration.Tests/
```

## Prerequisites

- .NET SDK (same version as the rest of the repository's .NET tools)
- A checkout of the repository being migrated, for `verify --repo-root`

## Build and test

```bash
cd tools/codeowners-migration
dotnet build
dotnet test
```

---

## `convert`: CODEOWNERS to owners files

Reads an existing CODEOWNERS file and emits `.github/owners.config.yaml` plus the
`sdk/<service>/owners.yaml` fragments.

The operator chooses the section layout, because it is an editorial decision the tool cannot infer:

| Option | Meaning |
|--------|---------|
| `--codeowners` | Path to the existing CODEOWNERS file. Required. |
| `--output-root` | Repository root to write into. Required unless `--dry-run`. |
| `--fragment-section` | A section whose entries move out into fragments. Repeatable. Sections not listed stay static. |
| `--fragment-glob` | Where fragments live. Defaults to `sdk/*/owners.yaml`. The wildcard segment becomes the fragment directory. |
| `--default-section` | Value for `configs.default-section`. |
| `--dry-run` | Print the files instead of writing them. |

### Section discovery uses `CodeownersSectionFinder` unmodified

`CodeownersSectionFinder` recognizes only the three-line banner. `azure-sdk-for-net` also contains
three single-line sub-headings — `Core Libraries`, `Eng Sys`, and `Code Generation`:

```text
####################
# Client Libraries                       <- three-line banner
####################

# ######## Core Libraries ########       <- single-line sub-heading
```

**The converter does not handle this case.** Instead, the three sub-headings in
`azure-sdk-for-net/.github/CODEOWNERS` are rewritten as ordinary three-line banners in a small
manual pull request **before** migration begins. Nothing distinguishes those three sections from the
other nine; the single-line form is an accident of history, not a meaning-bearing distinction.

Section discovery is therefore just `CodeownersSectionFinder`, unchanged and unwrapped.

The rejected alternative was to teach the converter a regex such as
`^#\s*#{3,}\s*(.+?)\s*#{3,}\s*$`. That pattern matches every banner **border** line in the file —
`################` captures a section name of `#` — while failing to match banner **titles** like
`# Client Libraries`. A literal implementation would produce eighteen junk sections named `#` and
attribute every client-library path to one of them, which is worse than the problem it was meant to
solve. Correcting the source file once is cheaper and leaves no special-case code behind.

### Entries that cannot become fragments

A fragment has to live in a real directory, so a path whose leading segments contain a glob has no
home. `convert` leaves these static and reports them:

- `/sdk/iot*/` and `/sdk/azurestack*/` — the service segment itself is a glob
- `/sdk/**/ci.mgmt.yml`, `/**/*Management*/`, `/**/Azure.ResourceManager*/` — repo-wide guardrails

These stay in `.github/owners.config.yaml` as static entries, which is where the design wants them
anyway.

### Conversion is faithful, even when the result does not validate

`convert` transcribes what the CODEOWNERS file says. It does not repair, relocate, or invent data to
satisfy the fragment schema, and it will happily emit YAML that `generate` then rejects.

The case that makes this concrete is `pr-labels`, which is **required on a fragment path entry** but
absent from some legacy blocks. `convert` writes those entries into the fragment without labels and
reports them. It does not invent a label, because the label determines how pull requests touching
that path are triaged, and it does not silently leave the entry static, because that would move the
entry's position in the rendered file and change which block wins under last-match-wins.

The result is that migrating a repository is a two-step operation: run `convert`, then run
`generate --check` and work through what it rejects. That is the intended shape. The converter has
no basis for the judgment calls, the validator states them precisely with a file, line, and rule
code, and the fix is made once by someone who knows the service.

In azure-sdk-for-net the backlog this produces is small. Of the 17 `/sdk/` path lines with no PR
label, 12 are the glob guardrails above and never enter a fragment. The remaining five are `/sdk/`
itself and:

- `/sdk/agentserver/Azure.AI.AgentServer.Core/`, `.Invocations/`, `.Responses/`
- `/sdk/ai/Azure.AI.Extensions.OpenAI/`, `/sdk/ai/Azure.AI.Projects.Agents/`

Each has two valid resolutions, and they are not equivalent — but the difference is about **labels**,
not about where the entry lives. Under merged sorting a static entry gets no positional privilege
over a fragment entry, so moving an entry between the config and a fragment does not change where it
lands.

What decides the outcome is the entry's primary label. Leaving an entry unlabelled gives it the
primary label `""`, which sorts above every labelled entry — including its own service catch-all —
so the catch-all wins. That is what the current hand-written file already does, since
`/sdk/ai/Azure.AI.Extensions.OpenAI/` precedes `/sdk/ai/` in it today. Assigning a PR label whose
primary sorts at or after the catch-all's primary label moves the entry below the catch-all, so the
specific entry wins instead.

`verify` reports the difference as a resolution change either way, so the choice is made
deliberately rather than discovered later.

### Refusing to introduce a shadowing duplicate

`convert` will not move a path into a fragment when the exact normalized path is already declared in
a section that stays static. Doing so would author a file that immediately fails `CFG-DUP-001`, and
would silently transfer ownership because the fragment section renders later. The entry is left
static and reported. Matching is exact, consistent with the duplicate rules in the spec.

### Representation changes conversion makes deliberately

A legacy block can attach a service label directly to a path:

```text
# ServiceLabel: %Tables
/sdk/tables/    @alice @bob
```

The fragment schema expresses service ownership only through `label-owners`, so this becomes two
declarations — a `paths` entry and a `label-owners` entry carrying the same owners. This is a
representation change, not an ownership change, and `verify` is built to recognize it as equivalent.

This copy is **mandatory wherever the legacy block relied on inheritance**. `CodeownersParser` sets
`ServiceOwners = SourceOwners` when a `# ServiceLabel:` block ends in a source-path line. Once the
label is split onto its own block there is no path to inherit from, so a `label-owners` entry that
omits `service-owners` re-parses with **zero** service owners. That would silently regress
`check-package` into `InsufficientServiceOwners` and trip `AUD-OWN-005`, while passes 1 and 2 of
`verify` still reported the paths as equivalent.

The current azure-sdk-for-net file does not exercise this: all of its `# ServiceLabel:` blocks are
pathless, so nothing is inherited and the conversion is a pure representation change. Blocks such as
`%Tables` and `%Azure.Identity` genuinely carry only `AzureSdkOwners` today, and the reference
assets reproduce that faithfully rather than inventing service owners. The rule exists because the
other four language repos have not been surveyed, and because a future hand-edit could introduce the
combined form. `convert` must detect the inheriting shape and materialize the owners explicitly;
`verify` pass 2 catches it if `convert` does not.

Comments in the source CODEOWNERS are not carried across. Conversion output is a first draft for
human review, not a finished file.

---

## `verify`: semantic equivalence between two CODEOWNERS files

Parses both files with `CodeownersParser` and compares the resulting objects. Comments, blank lines,
block ordering within a section, section membership and owner ordering are all ignored — the rendered
file is sparse by design and will never match the original textually.

Exit codes: `0` equivalent **and** resolution testing ran, `1` differences found or resolution
testing did not run, `2` the command failed.

### Owner lists are compared unexpanded

`CodeownersParser.ParseCodeownersEntries` expands `@Azure/<team>` into individual members from the
team blob, so a naive text round-trip can never match and a naive object comparison depends on live
team membership. Comparison therefore uses the parser's `OriginalSourceOwners`,
`OriginalServiceOwners` and `OriginalAzureSdkOwners` properties, which preserve the alias as written.
A team's membership changing between two runs must not read as an ownership change.

### Implicit owner inheritance is re-applied

When a block ends in a source path line, the parser fills an empty `# AzureSdkOwners:` moniker and a
`# ServiceLabel:` moniker from the source owners. That is a semantic rule, not formatting, and it
does not appear in the `Original*` properties. Comparison re-applies it on both sides: when the
declared list is empty but the expanded list is populated, the source owners are substituted.
Without this, every block using the shorthand form reads as a false difference.

### Three comparison passes

No single pass is sufficient.

| Pass | Compares | Catches |
|------|----------|---------|
| 1. Path declarations | Each path expression with its owners and PR labels | Added, removed and re-owned paths |
| 2. Label ownership | Each service label set with its service owners and Azure SDK owners | Added, removed and re-owned labels |
| 3. Path resolution | The winning entry for concrete repo paths, via `GetMatchingCodeownersEntry` | Ordering regressions |

Pass 2 is **union-aware**: every block declaring the same normalized label set is merged before
comparison. This is required by the label-union rule in the spec. Two source blocks each naming one
owner of `%AI Projects` and one rendered block naming both must compare equal, or union would report
as data loss on every migration.

Pass 3 is the only pass that can prove behavioural equivalence. Passes 1 and 2 are set comparisons
and are structurally blind to ordering: a file whose entries are identical but reordered compares
clean under both, while the owners GitHub actually applies have changed. Moving a broad
`/sdk/**/ci*.yml` entry above a narrower `/sdk/**/ci.mgmt.yml` entry changes who owns
`/sdk/ai/ci.mgmt.yml` and is invisible to passes 1 and 2. Pass 3 reports the change and names the
winning entry on each side.

### Pass 3 target enumeration

`DirectoryUtils.PathExpressionMatchesTargetPath` matches a path expression against a **concrete
target string**, so the choice of targets determines what pass 3 can prove. Directory targets alone
are not sufficient: a directory such as `sdk/ai/` never matches a file-shaped expression, and the
real azure-sdk-for-net file has more than twenty of them — `/*`, `/sdk/**/ci*.yml`,
`/sdk/**/ci.mgmt.yml`, `/**/package-lock.json`, `/sdk/**/*.tsp`, `/sdk/**/NuGet.*`,
`/eng/**/*.props`. Those are the cross-cutting guardrail entries, they live in sections that
migration relocates, and the motivating example above is a file interaction. Verifying only
directories would leave exactly the highest-risk entries untested while reporting success.

Therefore:

1. `--repo-root` enumerates **tracked files**, via `git ls-files`, including files at the repository
   root. Directories are additionally included so that directory-shaped expressions are exercised.
2. `--paths-from <file>` supplies an explicit newline-delimited target list, used for unit tests and
   for verifying a repo that is not checked out.
3. `verify` asserts **expression coverage**: every path expression appearing in either file must
   match at least one target. Expressions that match nothing are listed as `Unverified` and do not
   count toward a passing result. This converts "we did not test it" into a visible outcome rather
   than a silent pass.
4. Expressions that `DirectoryUtils.IsValidCodeownersPathExpression` **rejects** are reported as
   `NotMatchable` and are excluded from the coverage denominator. They do not fail the run.

For repositories large enough that full enumeration is slow, targets may be sampled per expression —
but the coverage assertion in step 3 still applies, so every expression retains at least one target.

### `NotMatchable` expressions

Step 4 exists because our matcher is stricter than GitHub. `IsValidCodeownersPathExpression` rejects
any glob ending in a bare `*` that is not `/*`, and `PathExpressionMatchesTargetPath` early-returns
`false` for anything it considers invalid. The live .NET file contains three such expressions
(`/sdk/**/NuGet.*`, `/sdk/**/Nuget.*`, `/sdk/**/nuget.*`), which GitHub honours and our matcher will
not resolve for any target.

Without step 4, those three would be permanently `Unverified` and `verify` could never exit 0 on the
repository it was written for — including the self-comparison that is supposed to be its first
smoke test.

What step 4 costs is honest and worth stating: a `NotMatchable` expression gets **no** pass-3
resolution testing. It is still covered by passes 1 and 2, which compare it as a declaration and
will catch it being dropped, reworded, or reassigned. What cannot be checked is whether its position
relative to other entries changed the file's resolution.

For a `NotMatchable` expression landing in a `sort: false` section that position is preserved by
construction, because the expression is carried into the config verbatim and authored order is the
render order. Most of them qualify: the `/**/*Management*/` family, `/sdk/**/ci*.yml`,
`/**/Azure.Provisioning*/`, and `/*` all live in sections that stay unsorted.

The exception is real and must be reviewed by hand. `Management Libraries` is `sort: true`, so a
glob-headed expression routed there — `/sdk/resources/Azure.ResourceManager.*/` and
`/sdk/arizeaiobservabilityeval/Azure.ResourceManager.*/` in the reference assets — is repositioned
by the sort and is simultaneously exempt from the pass that would detect the consequence. `verify`
prints the `NotMatchable` list on every run, annotated with each expression's target section and
that section's `sort` setting, so this overlap is visible rather than implicit.

### Pass 3 is mandatory

`--repo-root` is required unless `--paths-from` is supplied. `verify` exits non-zero when pass 3
resolved zero paths and when any expression is `Unverified`. `NotMatchable` expressions do not fail
the run; they are reported and excluded from coverage.

`--allow-zero-paths` downgrades the zero-paths failure to a warning and allows exit 0, for the one
legitimate case: verifying a CODEOWNERS pair for a repository that is not checked out. It prints a
prominent notice that equivalence was established by passes 1 and 2 only and that resolution was
**not** tested. It is never appropriate for a migration sign-off run.

This is deliberate. Passes 1 and 2 are set comparisons that report "equivalent" for a purely
reordered file, so a `verify` run that skips pass 3 succeeds on precisely the defect class that
migration introduces — including the case where sorting a section inverts a catch-all against a more
specific entry. A tool whose default invocation cannot fail for its primary failure mode is worse
than no tool, because it manufactures confidence.

### Rejected blocks are reported, not ignored

The parser writes malformed blocks to stderr and then drops the entry. Left alone, a dropped block
appears as a missing entry, or as no difference at all when both files drop it. `verify` captures
that output and reports it as a `ParseError` difference so silent data loss cannot pass.

---

## Expected outcome for `azure-sdk-for-net`

The current file yields 201 distinct path expressions and 131 distinct service label sets across 12
sections once both heading styles are recognized. Comparing the file against itself must report zero
differences; that self-comparison is the first check to run, because any difference it reports is a
defect in the comparer rather than in the migration.

## Testing

A defect in `verify` is worse than no verification at all: it produces a false assurance that
ownership was preserved. The test matrix is therefore part of the tool's contract.

- **Section discovery**: three-line banners; a banner section terminating where the next begins;
  ordinary comments and monikers not mistaken for headings.
- **Fragment attribution**: paths at, below, and outside the fragment glob; `.` for the fragment's own
  directory; deeper globs such as `sdk/resourcemanager/*/owners.yaml`; glob-headed paths rejected.
- **Conversion**: fragment sections move out while other sections stay static; a `ServiceLabel` on a
  path block splits into a `label-owners` entry; a path already declared statically is refused and
  reported; generated YAML parses and quotes glob expressions.
- **Comparer, no false positives**: a file compared against itself is equivalent; comments, blank
  lines, owner order and owner case are ignored; the inherited-owners shorthand compares equal to its
  explicit form.
- **Comparer, no false negatives**: changed, added and removed owners and paths are reported; label
  sets differing by one label are distinct; blocks the parser rejected surface as `ParseError`.
- **Union**: two blocks each naming one owner of a label set compare equal to one block naming both.
- **Ordering**: two files with identical declarations in different order report
  `ResolvedOwnersChanged` once targets are supplied.
- **Pass 3 cannot be skipped silently**: invoking `verify` with neither `--repo-root` nor
  `--paths-from` exits non-zero; a run that resolves zero paths exits non-zero; `--allow-zero-paths`
  exits 0 but emits the not-established warning. This is the guard against running `verify` without
  resolution testing and believing the result.
- **Coverage assertion**: a target set containing only directories leaves `/**/package-lock.json`
  and `/sdk/**/ci*.yml` reported as `Unverified` and fails the run; enumerating tracked files
  clears them.
- **`NotMatchable` classification**: `/sdk/**/NuGet.*` is reported as `NotMatchable`, is excluded
  from the coverage denominator, and does **not** fail the run; a self-comparison of the live
  `azure-sdk-for-net` CODEOWNERS exits 0 with three `NotMatchable` entries listed.

## Where this is used

Run by hand during migration, per repository:

```bash
# 1. Draft the YAML from the existing file
dotnet run --project Azure.Sdk.Tools.CodeownersMigration -- convert \
  --codeowners <repo>/.github/CODEOWNERS \
  --output-root <repo> \
  --fragment-section "Client Libraries"

# 2. Render from the drafted YAML
azsdk config codeowners generate --repo-root <repo>

# 3. Prove nothing moved
dotnet run --project Azure.Sdk.Tools.CodeownersMigration -- verify \
  --expected <repo>/.github/CODEOWNERS.orig \
  --actual <repo>/.github/CODEOWNERS \
  --repo-root <repo>
```

Not wired into any pipeline. **Delete this directory once every repository has migrated.**
