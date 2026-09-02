# Spec: 8-Operations - Codeowners Management from YAML Ownership Files

## Table of Contents

- [Overview](#overview)
- [Definitions](#definitions)
- [Background / Problem Statement](#background--problem-statement)
- [Goals and Exceptions/Limitations](#goals-and-exceptionslimitations)
- [Design Proposal](#design-proposal)
- [Alternatives Considered](#alternatives-considered)
- [Open Questions](#open-questions)
- [Success Criteria](#success-criteria)
- [Agent Prompts](#agent-prompts)
- [CLI Commands](#cli-commands)
- [Implementation Plan](#implementation-plan)
- [Testing Strategy](#testing-strategy)
- [Metrics/Telemetry](#metricstelemetry)
- [Documentation Updates](#documentation-updates)

---

## Overview

This spec replaces the Azure DevOps work-item-backed CODEOWNERS architecture with an
**in-repo YAML source of truth**.

Ownership is declared in two kinds of file that live in the SDK repository itself:

| File | Who edits it | What it declares |
|------|--------------|------------------|
| `.github/owners.config.yaml` | Repo maintainers / EngSys | Section order, repo-wide guardrail entries, static ownership that is not service-scoped, and which sections are populated from fragments |
| `sdk/<service>/owners.yaml` | Service teams | Ownership of that service directory and the labels that service owns |

`azsdk config codeowners generate` renders those files into `.github/CODEOWNERS`.
`.github/CODEOWNERS` becomes a fully generated artifact that **no human commits**: a regeneration job
renders it from `main` and opens a pull request with the result, and a pull request that modifies it
by hand is rejected. The rendered file is **sparse** — it carries only the data `CodeownersParser`
consumes, plus a fixed banner pointing readers back at `.github/owners.config.yaml`. All prose and
rationale live as YAML comments next to the entries they explain.

The Azure DevOps `Owner`, `Label`, and `Label Owner` work items, the
`config codeowners add-*` / `remove-*` command family, `config codeowners view`, and
`config github-label sync-ado` are removed. The command surface that remains (`generate`, `audit`,
`check-package`, `export-section`, `update-cache`) keeps its name and shape so downstream pipelines
and agent skills change as little as possible.

Reference artifacts for this spec:

- [`assets/codeowners/owners.config.yaml`](./assets/codeowners/owners.config.yaml) — a
  `.github/owners.config.yaml` derived from the real
  [azure-sdk-for-net CODEOWNERS](https://github.com/Azure/azure-sdk-for-net/blob/main/.github/CODEOWNERS)
- [`assets/codeowners/sdk-ai-owners.yaml`](./assets/codeowners/sdk-ai-owners.yaml) — an `sdk/ai/owners.yaml` fragment
- [`assets/codeowners/sdk-openai-owners.yaml`](./assets/codeowners/sdk-openai-owners.yaml) — an `sdk/openai/owners.yaml` fragment that shares a label set with `sdk/ai`
- [`assets/codeowners/CODEOWNERS.rendered`](./assets/codeowners/CODEOWNERS.rendered) — the exact
  file the three inputs above render to

**Owner aliases in the assets and in every example below are synthetic** (`test-user-NN`) and do not
correspond to real GitHub accounts. They preserve the structure of the source file — which entries
share owners, how many owners each entry has, and their authored order — so every ordering,
minimum-owner, and union example stays faithful. Team handles (`Azure/*`, `armleads-azure`) are left
as-is because they name groups rather than people.

---

## Definitions

- **Owners config**: The repo-level file `.github/owners.config.yaml`. It is the blueprint for the
  whole rendered CODEOWNERS file.
- **Owners fragment** (or **fragment**): A `sdk/<service>/owners.yaml` file contributed by a service
  team. Fragments cannot define sections and cannot address paths outside their own directory.
- **Fragment directory**: The directory containing a fragment. `sdk/ai/owners.yaml` has fragment
  directory `sdk/ai/`.
- **Section**: A named, ordered region of the rendered CODEOWNERS file, delimited by a `#` banner
  that `CodeownersSectionFinder` can locate.
- **Static entry**: A path entry or label-owner entry declared directly in the owners config.
- **Fragment-populated section**: A section declared with `defined-in-files: true`. Fragment entries
  are routed into it and merged with any static entries it declares.
- **Sorted section**: A section declared with `sort: true`. Every entry it contains — fragment or
  static — is ordered by `CodeownersEntrySorter.SortEntries` rather than rendered in authored order.
  Independent of `defined-in-files`.
- **Ownership inversion**: A rendered ordering in which a catch-all path renders *after* a path it
  contains, so under GitHub's last-match-wins resolution the catch-all owns the narrower path and
  the narrower entry has no effect.
- **Path entry**: A declaration of `path` + `owners` + `pr-labels`. Renders to a CODEOWNERS
  source-path line preceded by a `# PRLabel:` moniker. `pr-labels` is required in fragments and
  optional in the owners config; see [Component 2](#component-2-owners-fragment-schema-sdkserviceownersyaml).
- **Label-owner entry**: A declaration of `labels` + `service-owners` and/or `azure-sdk-owners`.
  Renders to a pathless block in the order `# AzureSdkOwners:` / `# ServiceLabel:` /
  `# ServiceOwners:`, which is the emission order `CodeownersEntry.FormatCodeownersEntry` produces.
- **Label set**: The normalized, order-insensitive, case-insensitive set of labels on a label-owner
  entry. The label set is the merge key for label-owner union.
- **Label-owner union**: The merge of every label-owner entry that shares an identical label set into
  a single rendered block, with unioned owners and a `# Sources:` provenance comment.
- **Rendered CODEOWNERS**: `.github/CODEOWNERS`, produced by `generate`. A generated artifact.
- **Sparse rendering**: The rendered file contains only what `CodeownersParser` consumes — section
  banners, monikers, source-path lines, and the `# Sources:` provenance comment. Prose, rationale,
  and warnings live as YAML comments in the source files and are never emitted.

---

## Background / Problem Statement

### Current State

The previous design moved ownership into Azure DevOps work items (`Owner`, `Label`,
`Label Owner`, `Package`) and rendered `.github/CODEOWNERS` from those work items. Service teams
mutated ownership through `azsdk config codeowners add-package-owner`,
`add-label-owner`, `remove-package-label`, and siblings, each of which created or unlinked work item
relations.

That approach has held up poorly in practice:

1. **Agents inserted invalid information.** Agents were observed inserting invalid information on behalf of users. since the user was unblocked, nobody checked on the validity of the information entered until the `CODEOWNERS` file itself was updated in an automated PR. Contributors or the language lead had to correct the data in Azure DevOps work items. 
1. **Ownership is invisible where the code is.** A contributor reading `sdk/ai/` cannot see or
   change who owns it without leaving the repo and querying an Azure DevOps project most partner
   teams cannot access.
2. **No review gate.** Work item edits bypass pull request review. An ownership change never gets
   reviewed by the people who currently own the code.
3. **No history that matters.** Work item revision history is not co-located with the code history,
   so `git log`/`git blame` cannot answer "when did this team take ownership and in which PR?". Information was auditable in DevOps Work Items but that process was manual. 
4. **High-friction mutation surface.** Six add/remove commands plus six MCP tools exist purely to
   perform CRUD against relations. The find-or-create semantics for `Label Owner` are fragile —
   `RemoveOwnersFromLabelsAndPath` uses `Single(...)` over `(repo, path, section, ownerType, labelSet)`
   and throws when duplicates exist.

Cross-language: every Azure SDK language repo has the same CODEOWNERS shape and the same problem.
Nothing in this design is language-specific.

### Why This Matters

Ownership data drives PR review assignment, issue triage routing, and release gating. When it is
hard to change, it goes stale; when it is stale, PRs sit unreviewed and issues route to the wrong
team. Moving the source of truth into the repository makes ownership a reviewable, greppable,
diff-able part of the codebase, and it removes an entire authentication and service dependency from
the tooling.

---

## Goals and Exceptions/Limitations

### Goals

- [ ] Ownership is declared in YAML files in the SDK repo and changed by pull request.
- [ ] `.github/CODEOWNERS` is fully generated and drift is detected in CI.
- [ ] Service teams edit exactly one small file: `sdk/<service>/owners.yaml`.
- [ ] Repo-wide guardrails and section ordering are explicit and author-controlled.
- [ ] Label owners contributed by multiple services are unioned into one block with provenance.
- [ ] Ownership cannot be declared for another team's directory from a fragment.
- [ ] The rendered file carries only parseable data, so prose lives with the source YAML and cannot
      drift from it.
- [ ] The `azsdk-cli` command surface shrinks; no command is renamed.
- [ ] The Azure DevOps `Owner` / `Label` / `Label Owner` work items and every code path that reads
      or writes them are deleted.

### Exceptions and Limitations

* Language leads must merge updated CODEOWNERS PRs and resolve conflicting data if issues arise.

---

## Design Proposal

### Design Overview

### Component 1: Owners config schema (`.github/owners.config.yaml`)

```yaml
version: 1

configs:
  allowed-owner-yaml-paths: ["sdk/*/owners.yaml", "sdk/*/owners.yml"]
  default-section: Client Libraries
  output: .github/CODEOWNERS

  # Thresholds must be met for PR or Release
  minimum-path-owners: 2
  minimum-label-owners: 2

sections:
  # Comments like this one are YAML comments for the humans editing this file.
  # They are never rendered into .github/CODEOWNERS.
  - name: Core Libraries
    defined-in-files: false
    sort: false            # authored order is the render order
    paths:
      - path: /sdk/core/
        owners: [test-user-15, Azure/azure-sdk-write-net-core]
        pr-labels: [Azure.Core]
    label-owners:
      - labels: [Azure.Core]
        service-owners: [test-user-15, Azure/azure-sdk-write-net-core]
        azure-sdk-owners: [test-user-15]

  - name: Client Libraries
    defined-in-files: true
    sort: true             # entries ordered by CodeownersEntrySorter.SortEntries
```

| Key | Type | Required | Meaning |
|-----|------|----------|---------|
| `version` | int | yes | Schema version. `1` for this spec. Unknown versions are a hard error. |
| `configs.allowed-owner-yaml-paths` | string[] | yes | Repo-root-relative globs. The complete set of locations a fragment may occupy. |
| `configs.default-section` | string | yes | Section that receives fragment entries with no explicit `section`. Must exist and be `defined-in-files: true`. |
| `configs.output` | string | no | Rendered file path. Default `.github/CODEOWNERS`. |
| `configs.minimum-path-owners` | int | no | Minimum individual (non-team) owners on a fragment path entry. Default `2`. Fails `check-package` if not met |
| `configs.minimum-label-owners` | int | no | Minimum individual (non-team) service owners on a label-owner block, evaluated **after union**. Default `2`. Fails `check-package` if not met |
| `sections[]` | list | yes | Ordered. Render order equals declaration order. |
| `sections[].name` | string | yes | Unique. Also the key used by `export-section` and `CodeownersSectionFinder`. |
| `sections[].defined-in-files` | bool | no | Default `false`. Marks the section as a target for fragment entries. |
| `sections[].sort` | bool | no | Default `false`. `true` orders the section's entries with `CodeownersEntrySorter.SortEntries`; `false` renders them in authored order. Independent of `defined-in-files`. |
| `sections[].paths[]` | list | no | Static path entries. Ordered by `sections[].sort`. |
| `sections[].label-owners[]` | list | no | Static label-owner entries. Ordered by `sections[].sort`. |

#### The owner minimums never fail generation

`minimum-path-owners` and `minimum-label-owners` are evaluated by the audit as `AUD-OWN-004` and
`AUD-OWN-005`, both **Report only**. `generate` does not enforce them and a fragment that falls below
them still renders.

This is deliberate. The minimums express what good ownership looks like — at least two humans who can
approve a change — but a service with one maintainer is a staffing problem, not a syntax error, and
failing generation would take CODEOWNERS offline for the whole repository over it. The reference
assets in `docs/specs/assets/codeowners/` include two single-owner path entries for exactly this
reason: they are realistic, they are reported, and they render.

There is no key for prose. Rationale, warnings, and the "matching is bottom-to-top" reminder are
YAML comments in this file, where they sit next to the entries they describe. The rendered
CODEOWNERS carries only a fixed banner (see [Component 7](#component-7-generated-file-banner-and-drift-detection))
and parseable data.

A section may set `defined-in-files: true` **and** declare `paths` / `label-owners`. Static and
fragment entries in such a section are **merged into one set** and then ordered according to the
section's `sort` setting — a static entry gets no positional privilege over a fragment entry. This
is how `Management Libraries` keeps hand-curated partner-team entries while accepting fragment
contributions, and it is what makes the choice between declaring an entry statically or in a
fragment a question of *where the data lives* rather than *where it lands in the output*.

### Component 2: Owners fragment schema (`sdk/<service>/owners.yaml`)

```yaml
version: 1

# Optional. Routes every entry in this file to a non-default section.
section: Client Libraries

paths:
  - path: .
    owners: [test-user-07, test-user-09, test-user-18, test-user-23, test-user-24]
    pr-labels: ["AI Model Inference", "AI Projects"]
  - path: Azure.AI.Inference/
    owners: [test-user-07, test-user-09, test-user-23]
    pr-labels: ["AI Model Inference"]
  - path: Azure.ResourceManager.AI
    section: Management Libraries
    owners: [test-user-07, test-user-09]
    pr-labels: ["AI Model Inference", "Mgmt"]

label-owners:
  - labels: [AI Projects]
    service-owners: [test-user-07, test-user-18, test-user-23]
    azure-sdk-owners: [test-user-07]

  - labels: ["AI Model Inference", "Mgmt"]
    section: Management Libraries
    service-owners: [test-user-07, test-user-09]
```

| Key | Type | Required | Meaning |
|-----|------|----------|---------|
| `version` | int | yes | Must match the owners config version. |
| `paths[].path` | string | yes | **Relative to the fragment directory.** `.` means the fragment directory itself. Directories must end in `/`; see [Normalization](#normalization). |
| `paths[].owners` | string[] | yes | At least one. |
| `paths[].pr-labels` | string[] | **yes** | At least one. Renders as `# PRLabel:`. |
| `paths[].section` | string | no | Entry-level section override. |
| `label-owners[].labels` | string[] | yes | At least one. The merge key. |
| `label-owners[].service-owners` | string[] | conditional | Renders as `# ServiceOwners:`. |
| `label-owners[].azure-sdk-owners` | string[] | conditional | Renders as `# AzureSdkOwners:`. |
| `label-owners[].section` | string | no | Entry-level section override. |

At least one of `service-owners` / `azure-sdk-owners` is required on a label-owner entry. 

**Declare `service-owners` whenever the label describes a service that owns code.** Service
ownership is not inherited from a path entry in this schema the way it can be in hand-written
CODEOWNERS, so a label-owner entry with only `azure-sdk-owners` renders a block with no
`# ServiceOwners:` line and re-parses with zero service owners, which fails `check-package` with
`InsufficientServiceOwners` and trips `AUD-OWN-005`. Some labels legitimately have only an Azure SDK
owner — `%Tables` and `%Azure.Identity` are like that in azure-sdk-for-net today — so this is a
schema warning rather than an error. See [Component 12](#component-12-migration-utilities).

#### `pr-labels` is required in fragments and optional in the owners config

A path entry with no PR label produces a CODEOWNERS block that assigns reviewers but never labels
the pull request, so the change lands outside the label-driven triage and release tooling that the
rest of this system depends on. This is already treated as a defect downstream: `check-package`
reports `missing_pr_label` when a resolved package has zero PR labels
(`CheckPackageHelper.cs:120-124`). Requiring `pr-labels` in the fragment schema moves that existing
check from "reported after the fact" to "cannot be authored," which is the only place it can be
enforced cheaply. A fragment path entry with a missing or empty `pr-labels` fails with
`CFG-LBL-001`.

The requirement applies to fragments only. Static path entries in `.github/owners.config.yaml` stay
valid as authored, for the same reason the duplicate-definition rules are fragment-scoped: the
config carries repository-wide guardrails that are deliberately label-less. In azure-sdk-for-net
today, 48 of 201 path lines carry no `# PRLabel:` moniker; 12 of the 17 under `/sdk/` are glob
guardrails (`/sdk/**/ci*.yml`, `/sdk/**/global.json`, `/sdk/**/*.tsp`, and similar) that are static
config entries by construction and are unaffected by this rule.

**Migration consequence.** That leaves five service-directory entries in azure-sdk-for-net that a
fragment cannot express as written: `/sdk/` itself, the three `/sdk/agentserver/Azure.AI.AgentServer.*`
entries, and the two `/sdk/ai/Azure.AI.Extensions.OpenAI/` and `/sdk/ai/Azure.AI.Projects.Agents/`
entries. `convert` does not fix these. It transcribes them faithfully into fragments without labels
and reports them, so migration is a two-step operation: convert, then work through what
`generate --check` rejects. Each entry has two non-equivalent resolutions — assign a label and keep
it in the fragment, or move it to a static owners config entry — and they place the entry on
opposite sides of its service catch-all under last-match-wins. The reference assets take the second
option for the two `/sdk/ai/` entries, which preserves what the hand-written file resolves to today.
See [`tools/codeowners-migration/README.md`](../../../codeowners-migration/README.md).

#### Schema normalization decisions

The prototype examples used `pr-label:` in the owners config and `pr-labels:` in the fragment, and
used `label-owners:` in the config against `labels:` in the fragment. This spec unifies on a single
vocabulary in both file types:

- `pr-labels` (plural), never `pr-label`
- `label-owners` for the top-level list, with `labels` as the key inside each entry

The loader rejects the non-canonical spellings with a message naming the correct key, rather than
silently accepting aliases.

There is deliberately no key for comments in either file type. YAML `#` comments are the mechanism,
and they stay in the source file.

### Component 3: Path containment rules

Fragments may only own their own subtree. Containment is enforced for **paths**, and is enforced in
two independent steps:

1. **Literal `..` rejection.** If a fragment `path` value contains a `..` path segment anywhere, the
   load fails immediately with `CFG-PATH-001` before any resolution or normalization is attempted.
   `../some-other-service`, `Azure.AI.Inference/../../storage`, and `./..` are all rejected on sight.
   This check is textual and runs first so that no clever expression can reach the resolver.
2. **Resolved-path containment.** After the path is resolved against the fragment directory and
   normalized, the result must be the fragment directory itself or a descendant of it. A path that
   resolves outside the fragment directory for any other reason (absolute path, symlink-style
   trickery, drive-qualified path) fails with `CFG-PATH-002`.

Additional path rules:

- A leading `/` in a fragment `path` is an error (`CFG-PATH-003`). Repo-absolute expressions belong in
  the owners config. This removes the ambiguity of whether `/sdk/ai` means "repo root" or "fragment
  directory".
- Glob metacharacters `*` and `**` are allowed within the fragment subtree — `Azure.AI.*` is valid
  in `sdk/ai/owners.yaml` and resolves to `/sdk/ai/Azure.AI.*`. `?` is **not** allowed: the
  CodeownersUtils matcher rejects any expression containing it
  (`ErrorMessageConstants.ContainsQuestionMarkPartial`, "contains ?. Please use * instead."), and
  the globber treats `?` as a literal rather than a wildcard.
- Owners config `path` values must be repo-absolute (leading `/`) — `CFG-PATH-004` otherwise.
- A glob-free fragment path that names a directory on disk must be authored with a trailing `/`.
  `CFG-PATH-005` otherwise. See [Normalization](#normalization).

##### Path validation applies to fragments only

Every `CFG-PATH-*` rule, and the expression-validity gate in
[Component 6](#component-6-rendering-algorithm), applies **only to paths contributed by
`owners.yaml` fragments**. Path expressions written directly in the owners config are accepted as
authored.

The reason is that the owners config is seeded from each repository's existing CODEOWNERS file, and
those expressions are valid today by definition — they are what GitHub is already enforcing. Some of
them are not expressible under the fragment rules, and some are not even accepted by our own
matcher. The live .NET file contains three:

```text
/sdk/**/NuGet.*    @Azure/azure-sdk-write-net-core @test-user-13
/sdk/**/Nuget.*    @Azure/azure-sdk-write-net-core @test-user-13
/sdk/**/nuget.*    @Azure/azure-sdk-write-net-core @test-user-13
```

`DirectoryUtils.IsValidCodeownersPathExpression` rejects any glob ending in a bare `*` that is not
`/*` (`GlobCannotEndInWildCardPartial`), and `PathExpressionMatchesTargetPath` early-returns `false`
for any expression it considers invalid. GitHub, which evaluates CODEOWNERS with gitignore
semantics, **does** honour these expressions; our matcher does not. Validating config entries would
therefore reject a file that GitHub is enforcing correctly.

Fragments are new authoring surface with no such legacy, so they are held to the stricter rule. This
keeps every *newly authored* expression matchable by the audit and resolution tooling while leaving
the migrated corpus untouched.

#### Normalization

Path normalization is deliberately minimal. The renderer **never infers whether an expression names
a file or a directory**. A fragment path is resolved against the fragment's directory, and a config
path is used as written. Nothing else is added or removed.

```text
sdk/ai/owners.yaml  path: .                      ->  /sdk/ai/
sdk/ai/owners.yaml  path: Azure.AI.Inference/    ->  /sdk/ai/Azure.AI.Inference/
sdk/ai/owners.yaml  path: Azure.AI.*             ->  /sdk/ai/Azure.AI.*
sdk/ai/owners.yaml  path: ci.yml                 ->  /sdk/ai/ci.yml
sdk/ai/owners.yaml  path: ../storage             ->  ERROR CFG-PATH-001
sdk/ai/owners.yaml  path: /sdk/storage/          ->  ERROR CFG-PATH-003
sdk/ai/owners.yaml  path: Azure.AI.Inference     ->  ERROR CFG-PATH-005
```

The single special token is `.`, meaning the fragment's own directory, which resolves with a
trailing `/`.

##### Why trailing slashes are authored, not inferred

`CodeownersEntrySorter.NormalizePath` appends a trailing `/` only when the path does not already end
in `/`, `Path.HasExtension(normalized)` is false, and the path contains no `*`. The
`HasExtension` test asks only whether the final segment contains a `.`, so for .NET it is wrong
almost everywhere: `Path.HasExtension("/sdk/ai/Azure.AI.Inference")` returns `true`, and the path
renders without its trailing slash. In gitignore semantics — which
CODEOWNERS follows — `foo/` matches a directory while `foo` matches a file *or* a directory, so
dropping the slash silently broadens the expression. Nearly every Azure SDK package directory is
dotted (`Azure.AI.Inference`, `Azure.ResourceManager.ApiCenter`, `System.ClientModel`), so the
heuristic would misnormalize the majority of entries in the repository.

Because this design owns the input format, the ambiguity is removed at the source rather than
guessed at: authors write `Azure.AI.Inference/` for a directory and `ci.yml` for a file.

`CFG-PATH-005` decides which of those an author meant by **resolving the path on disk**, not by
inspecting the string:

| Fragment path resolves to | Requirement |
|---------------------------|-------------|
| A directory in the working tree | Must be authored with a trailing `/` — `CFG-PATH-005` otherwise |
| A file in the working tree | Accepted as written; a trailing `/` is `CFG-PATH-005` |
| Nothing in the working tree | `CFG-PATH-006`, an orphaned path (see [Component 10](#component-10-audit-rules)) |
| An expression containing `*` or `**` | Not checked; globs cannot be resolved by `stat` |

Resolution is performed against the checkout `generate` is already running in, using the same
`StringComparison.Ordinal` semantics as the rest of the path pipeline.

The alternative — deciding file-vs-directory from a list of recognized file extensions — was
rejected. It requires an allowlist that must be kept current, and any implementation that reached
for `Path.HasExtension` instead would classify `Azure.AI.Inference` as a file and render it without
its trailing slash, which is precisely the broadening bug this rule exists to prevent. Asking the
filesystem is unambiguous and needs no list.

`CodeownersEntrySorter.NormalizePath` is therefore **not** reused for rendering. It remains in use
only where it already is, on parsed entries.

Owners are normalized by trimming, stripping a leading `@`, and de-duplicating. They render with an
`@` prefix.

Labels are normalized by trimming, stripping a leading `%`, and de-duplicating. They render with a
`%` prefix.

#### Owners and labels are never sorted

**Authored order within an entry is preserved verbatim.** The work-item system sorted owners and
labels alphabetically because work items have no ordinal field — there was no other way to render
them deterministically. YAML sequences *are* ordinal, so the authored order is available and is
authoritative. Teams routinely list a primary contact first; alphabetizing destroys that signal for
no benefit.

De-duplication keeps the **first** occurrence and discards later ones, so the surviving spelling is
always the earliest authored one.

#### Two comparers, chosen by what GitHub does

| Data | Comparer | Why |
|------|----------|-----|
| Owners (users and teams) | `OrdinalIgnoreCase` | GitHub resolves logins and team slugs case-insensitively; `@test-user-13` and `@test-user-13` are the same account. |
| Labels | `OrdinalIgnoreCase` | GitHub label matching in the triage automation is case-insensitive. |
| Path expressions | `Ordinal` | **GitHub evaluates CODEOWNERS against its own case-sensitive filesystem.** |

The path rule is not a preference. GitHub's documentation states that "CODEOWNERS paths are case
sensitive, because GitHub uses a case sensitive file system. Since CODEOWNERS are evaluated by
GitHub, even systems that are case insensitive (for example, macOS) must use paths and files that
are cased correctly in the CODEOWNERS file." Matching happens server-side, so the case-insensitivity
of a contributor's local checkout is irrelevant.

`DirectoryUtils` already encodes this — both of its matchers are constructed with
`new Matcher(StringComparison.Ordinal)` under the comment *"Don't use OrginalIgnoreCase. GitHub is
case sensitive for directories."* Rendering must agree with matching, or the renderer and the
resolver would disagree about which entries are distinct.

Consequently `/sdk/Tables/` and `/sdk/tables/` are **different expressions**, not a duplicate pair.
A miscased path is caught by `AUD-PATH-001` (the expression matches nothing in the working tree),
which is the correct diagnosis; folding them together with a case-insensitive comparer would hide
the real bug behind a spurious duplicate error.

These are the only two comparers in the design, and each is pinned to a data type rather than to a
call site, so an implementation cannot drift between them.

### Component 4: Label-owner union

Label-owner entries **contributed by fragments** are grouped by their **label set**: the normalized
set of labels, compared order-insensitively and case-insensitively. Sets that differ by even one
label are distinct blocks, because a `ServiceLabel` block's label set is what triage automation
matches on.

Label-owner entries written **directly in the owners config are never grouped**. Each renders as its
own block, even if another static entry declares the same label set. This is a rule about *merging*,
not about ordering — where the block lands in its section is governed by `sections[].sort`. See
[Static entries are never merged](#static-entries-are-never-merged).

For each fragment group:

1. **Provenance** is the ordered list of contributing fragments, by repo-relative path,
   `StringComparer.Ordinal` ascending.
2. `service-owners` is the union of all contributors' `service-owners`, concatenated **in provenance
   order**, each contributor's owners in their authored order, de-duplicated with first occurrence
   winning. The result is not sorted.
3. `azure-sdk-owners` is unioned the same way.
4. The rendered `# ServiceLabel:` line uses the labels of the **first contributor in provenance
   order**, in that contributor's authored order and casing. Later contributors' spellings of the
   same label set are discarded. Without this rule, two fragments spelling a label `AI Projects` and
   `ai projects` would render nondeterministically.
5. The target section is the section of the first contributor in provenance order.

A `# Sources:` comment is emitted as the first line of any block that has **at least one fragment
contributor** — including single-contributor blocks, where it answers "which file put this here?".
Blocks rendered from static config entries never carry one; the config is its own provenance.

Its format is fixed: the literal `# Sources: `, then the contributing fragments' repo-relative paths
in provenance order, separated by `, `. It sits above the block's first moniker line, where
`CodeownersParser` treats it as an ordinary comment — `MonikerUtils.ParseMonikerFromLine` does not
recognize it, so `IsMonikerOrSourceLine` returns false and it neither starts nor terminates a block.

`CodeownersEntry.FormatCodeownersEntry()` has no notion of provenance, so the renderer prepends this
line itself rather than delegating it.

Given `sdk/ai/owners.yaml` and `sdk/openai/owners.yaml` both declaring `labels: [AI Projects]`:

```text
# Sources: sdk/ai/owners.yaml, sdk/openai/owners.yaml
# AzureSdkOwners: @test-user-07
# ServiceLabel: %AI Projects
# ServiceOwners: @test-user-07 @test-user-18 @test-user-23 @test-user-02 @test-user-24
```

The `ServiceOwners` line is in provenance order — `sdk/ai`'s three owners, then `sdk/openai`'s two —
not alphabetical order.

#### Static entries are never merged

Grouping applies to fragments because the union is the feature fragments exist to provide: two
service teams can each claim a share of a shared triage label without coordinating an edit to a
single shared line.

Static config entries get the opposite treatment for the same reason path entries do — they are
carried over from an existing CODEOWNERS file and their owner lists must render as they were
written. Merging them would silently change ownership during migration. The live .NET file makes
this concrete:

```text
644: # ServiceLabel: %ARM %Mgmt
645: # ServiceOwners:                                                   @Azure/arm-sdk-owners
646:
647: # ServiceLabel: %ARM %Mgmt
648: # ServiceOwners:                                                   @armleads-azure
```

Two blocks, the same label set, deliberately different owners. Under a grouping rule they would
collapse into one block owned by both, changing who GitHub notifies. Keeping static entries as
distinct blocks preserves both.

### Component 5: Duplicate-definition validation

Ownership must be declared in exactly one place, except where overlap is deliberate.

| Rule | Condition | Severity |
|------|-----------|----------|
| `CFG-DUP-001` | A normalized path expression declared in an owners config section is also produced by a fragment | Error |
| `CFG-DUP-002` | The same normalized path expression is produced by two different fragments | Error |
| `CFG-DUP-003` | The same normalized path expression is declared twice within one owners config section | Error |
| `CFG-DUP-004` | A label set declared in an owners config section is also declared by a fragment | Error |

`CFG-DUP-004` is what keeps label-owner union meaningful: union is the sanctioned way for **two or
more service teams** to co-own a label set. A repo-level static declaration competing with a
fragment declaration is an authoring mistake, not a merge.

**All four rules apply uniformly to every section. There are no exemptions.**

An earlier draft had a `protected: true` section flag that suppressed `CFG-DUP-001`, `CFG-DUP-003`,
and `CFG-DUP-004`, on the reasoning that guardrail entries are *supposed* to sit on top of
fragment-owned paths. The flag was removed because it never did anything. These rules match on
**exact normalized path expressions**, and a guardrail overlaps a fragment path by being broader —
`/sdk/**/ci*.yml` against `sdk/ai/` — not by being identical. Broad-versus-narrow overlap is invisible
to exact matching, so the guardrails were never at risk and the exemption suppressed nothing.

Confirmed against the reference assets: the six sections that carried `protected: true` produce zero
violations of any of the four rules with the exemption removed.

#### What is deliberately not checked

Two rules a reader might expect are absent by design:

- **The same path declared in two different config sections.** `CFG-DUP-003` is scoped to a single
  section.
- **The same label set declared by two static config entries.** The live .NET file contains exactly
  this (`%ARM %Mgmt`, twice, with different owners).

Both are consequences of the rule in
[Path validation applies to fragments only](#path-validation-applies-to-fragments-only): the config
is seeded from an existing CODEOWNERS file and is valid as written. Flagging constructs already
present in a working file would block migration on findings that are not defects. Fragments — the
new authoring surface — carry the full rule set.

#### Why `CFG-DUP-001` is an error rather than a warning

The harm is silent shadowing, and it is a direct consequence of section ordering.

Sections render in the order they are declared in the owners config, and GitHub applies
last-match-wins. A statically declared path therefore only takes effect if nothing later in the file
matches the same path. When a fragment reproduces that exact path into a section that renders
further down, the fragment silently wins and the static declaration becomes dead text that still
reads as though it were in force.

Consider `/sdk/tables/` declared statically in `Core Libraries`, and a `sdk/tables/owners.yaml`
fragment that declares `.` while targeting `Client Libraries`. `Core Libraries` renders first, so the
rendered file contains both entries and the fragment's owners are the ones GitHub uses. Nobody
reviewing `owners.config.yaml` would expect that. The repo-level declaration is the deliberate one
and must not be overridable from a service directory, so this is rejected at generation time rather
than resolved by precedence.

#### Matching is on the exact normalized path only

Duplicate detection compares **normalized path expressions for exact string equality**. It does not
attempt to decide whether one glob is a subset or superset of another.

`/sdk/tables/` and `/sdk/tables/Azure.Data.Tables/` are different paths and do not collide.
`/sdk/**/ci.yml` and `/sdk/tables/ci.yml` are different paths and do not collide, even though every
target matched by the second is also matched by the first.

This is a deliberate limitation, for two reasons:

1. **Glob containment is not reliably decidable** for the expression language CODEOWNERS accepts, and
   an approximate implementation would produce false errors that authors cannot act on.
2. **Overlap is normal and wanted.** The guardrail sections exist precisely to layer broad patterns
   over narrow ones. A rule that flagged subset relationships would fire constantly on correct files.

Normalization before comparison is limited to the rules in Component 3: a leading `/` is added, and
the authored trailing `/` is preserved as written. Comparison uses `StringComparer.Ordinal`, because
GitHub evaluates path expressions case-sensitively.

`/sdk/Tables/` and `/sdk/tables/` are therefore distinct expressions and do not collide. That is
correct rather than permissive: one of them matches nothing on GitHub, and `AUD-PATH-001` reports it
as an expression that matches no file in the working tree, which names the actual defect.

Genuine ordering mistakes that exact matching cannot see are caught by resolution testing instead;
see Component 12.

Error messages name every contributing file and line so the fix is obvious:

```text
CFG-DUP-001: Path '/sdk/tables/' is defined in more than one place.
  .github/owners.config.yaml:78   (section 'Core Libraries', renders 4th)
  sdk/tables/owners.yaml:6        (section 'Client Libraries', renders 5th)
  The fragment renders after the config section, so it would silently take over
  ownership of this path.
  Remove one of the two definitions.
```

### Component 6: Rendering algorithm

Deterministic. The same inputs always produce a byte-identical file.

1. **Load.** Parse the owners config. Enumerate every file matching
   `configs.allowed-owner-yaml-paths`. Separately, scan the repo for any `owners.yaml` / `owners.yml`
   that does *not* match those globs and fail with `CFG-LOC-001` if one is found.
2. **Schema validate.** Reject unknown keys, missing required keys, non-canonical key spellings, and
   version mismatches.
3. **Normalize.** Apply the path, owner, and label normalization rules above. Fragment path
   containment (`CFG-PATH-001` / `CFG-PATH-002`) is enforced here.
4. **Bind sections.** Resolve each entry's target section: entry `section` → file `section` →
   `configs.default-section` for fragments; the declaring section for static entries. A fragment
   entry targeting a missing section, or one without `defined-in-files: true`, fails with
   `CFG-SEC-001`.
5. **Union label owners.** As described in Component 4.
6. **Validate.** Run the `CFG-*` rules. Any error aborts generation; nothing is written.
7. **Order.** Sections render in declaration order. What happens *within* a section is controlled by
   `sections[].sort`, and by nothing else. `sort` and `defined-in-files` are independent: a section
   may be fragment-populated and unsorted, or static-only and sorted.

   Whichever branch applies, static and fragment entries are merged into one set first and nothing
   distinguishes them afterwards. Provenance affects only the `# Sources:` comment.

   **`sort: true` — apply the repository's existing entry sort.** The renderer calls
   `CodeownersEntrySorter.SortEntries`, unchanged. Its semantics, which the spec depends on and
   therefore states explicitly:

   - Entries are partitioned into *pathed* entries and *pathless* label-owner blocks.
   - Pathed entries sort by **primary label** (`OrdinalIgnoreCase`), then normalized path
     (`Ordinal`), then the formatted line (`Ordinal`). An entry's primary label is its first
     `ServiceLabel`, else its first `PRLabel`, else the empty string — so **an entry with no label
     sorts before every labelled entry**.
   - A pathless block whose labels appear on any pathed entry is inserted immediately after the
     **last** pathed entry carrying any of those labels, which keeps a service's PR-label and
     service-label data adjacent. Blocks resolving to the same anchor sort by joined label string
     and then by formatted line. Blocks matching no pathed entry are appended at the end.

   **`sort: false` (default) — render in authored order.** Static entries in the order they are
   declared in `.github/owners.config.yaml`, then fragment entries in provenance order (fragment
   repo-relative path ascending, then declaration order within each fragment), then label-owner
   blocks the same way. This is the branch to use when the section's order encodes deliberate
   last-match-wins intent that no mechanical sort reproduces.

   In both branches the section banner renders first, and **owners and labels within an entry are
   never reordered.** Only *entries* move.

#### Why `sort: true` reuses `CodeownersEntrySorter`

The sort is not re-derived for this system. `CodeownersEntrySorter.SortEntries` is the ordering the
repository already applies to CODEOWNERS entries, and reusing it verbatim is what keeps the
generated file consistent with the hand-maintained files it replaces.

That choice was validated against the real azure-sdk-for-net CODEOWNERS rather than assumed. The
measurement counts *ownership inversions* — cases where a catch-all such as `/sdk/ai/` renders
**after** one of its own subdirectories, so under last-match-wins the catch-all silently wins and
the narrower entry has no effect:

| Ordering applied to the real `/sdk/` entries | Ownership inversions |
| --- | --- |
| The file as it exists today | 7 |
| `CodeownersEntrySorter.SortEntries` | 6 |
| Sorting by path expression alone | 0 |

`SortEntries` introduces **no** inversion that the file does not already have, and removes one
(`/sdk/deviceupdate/`). Adopting it is therefore close to behavior-preserving, which is the property
that matters during migration. Sorting by path alone is superficially more attractive — a prefix
always sorts before the paths it contains, so it cannot produce an inversion at all — but it would
silently change seven ownership resolutions in a single commit. Ordering is not the place to make
those changes; each one is a decision for the owning team.

#### Inversions are a data condition, not a sorting defect

An inversion arises whenever a descendant entry's primary label sorts strictly before its
ancestor's. The empty-label case is the extreme: `/sdk/ai/Azure.AI.Extensions.OpenAI/` carries no
label, so its primary label is `""` and it sorts above the labelled `/sdk/ai/` catch-all that
contains it. Both reference-asset inversions are of this kind.

Requiring `pr-labels` does not eliminate the condition, because a labelled descendant can still sort
first. In the real file, `/sdk/servicebus/Microsoft.Azure.WebJobs.Extensions.ServiceBus/` is labelled
`%Functions %Service Bus`, giving it the primary label `Functions`, which sorts before the
`Service Bus` label on `/sdk/servicebus/`. The descendant renders first and the catch-all wins.

The correct response is to report the condition, not to change the sort. See
[`AUD-ORD-001`](#aud-ord-001-reports-ownership-inversions) — `audit` detects inversions in the
rendered output and reports them for the owning team to resolve by adjusting labels or by moving the
section to `sort: false`. `generate` never rewrites ownership to avoid one.

8. **Emit.** Render the fixed generated-file banner, then each section. Each entry is formatted by
   the existing `CodeownersEntry.FormatCodeownersEntry()`. Whitespace is exact:
   - one blank line after the five-line generated-file banner;
   - one blank line before each section banner and one after it;
   - one blank line between consecutive entries within a section;
   - no trailing whitespace on any line, and exactly one `\n` terminating the final line.

   No YAML comment is ever copied into the output.

Section banners use a `#` rule of `max(20, len("# " + name))` characters, which satisfies
`CodeownersSectionFinder.IsSectionBorder` and preserves the existing 20-character banners for
short names:

```text
####################
# Client Libraries
####################
```

The `sort` flag resolves the standing open question about `/**/*Management*/` fallback entries
sorting into the middle of the Management Libraries section: fallback entries now live in a
`Management Fallback` section that is declared before `Management Libraries` and left at the default
`sort: false`, so their authored order is preserved exactly.

### Component 7: Generated-file banner and drift detection

The rendered file is **sparse**: it contains only what `CodeownersParser` consumes. The single
exception is a fixed five-line banner at the top, which is identical in every repo and is not
configurable:

```text
# ------------------------------------------------------------------------------
# GENERATED FILE - DO NOT EDIT
# Ownership is defined in .github/owners.config.yaml and sdk/*/owners.yaml
# Regenerate with: azsdk config codeowners generate
# ------------------------------------------------------------------------------
```

Everything a reader needs beyond that — why a section exists, why matching order matters, why a
particular catch-all is placed where it is — lives as a YAML comment next to the entry it explains
in `.github/owners.config.yaml`. Keeping prose out of the rendered file means it cannot drift from
the data, and it removes the temptation to hand-edit the generated artifact to fix a comment.

#### Who writes the rendered file

**No human commits `.github/CODEOWNERS`.** A regeneration job owns the file end to end: it renders
from the YAML on `main` and opens a pull request carrying the result. Contributor pull requests
change ownership YAML only and never contain the rendered file.

That gives three enforcement points with distinct jobs.

**Gate 1 — hand-edit rejection.** Any pull request whose diff includes `.github/CODEOWNERS` and that
did not come from the regeneration job fails immediately, with a message pointing at
<https://aka.ms/azsdk/codeowners>. This is a check on the changed-file list and the PR head branch;
it needs no rendering and runs before anything else, so a contributor who edits the generated file
by hand gets one unambiguous error instead of a byte-level diff they cannot act on.

**Gate 2 — YAML validity.** Runs on any PR touching `.github/owners.config.yaml` or an
`owners.yaml`, and is the whole of what a contributor has to satisfy. It is checks 1 and 2 below.

**The regeneration job.** Runs on `main` after merge. Renders, compares, and opens or updates a PR
when the committed file is stale.

`generate --check` serves gate 2 and the regeneration job from one code path, distinguished by exit
code:

| Exit | Meaning | Gate 2 | Regeneration job |
|------|---------|--------|------------------|
| 0 | YAML is valid and the committed file is in sync | pass | nothing to do |
| 1 | A `CFG-*` validation error; nothing could be rendered | **fail** | alert; do not open a PR |
| 2 | YAML is valid but the committed file is out of sync | **pass** | open or update the regeneration PR |

Exit 2 is the *expected* state of a contributor's PR — they changed YAML and, correctly, did not
regenerate. Gate 2 treating it as a pass is what makes the workflow coherent; treating it as a
failure is the trap this design exists to avoid.

`generate --check` performs two checks, in this order.

**Check 1 — the YAML renders.** Load, normalize, and validate every ownership file in the checkout
and render in memory, then compare against the committed file. Rendering is a pure function of
repository contents (see [Component 8](#component-8-invalid-owner-handling)), so this needs no
network, no cache, and no knowledge of when it runs. A `CFG-*` failure is exit 1; a clean render
that does not match the committed bytes is exit 2.

**Check 2 — owner validity, scoped to the paths the PR touches.** For each ownership file the PR
changes, collect the owners declared there and validate them against the publicly downloadable owner
cache. Fail on any owner the cache reports as invalid, and fail if the cache itself is unusable
(see [Cache availability](#cache-availability)).

Check 2 runs on the **same trigger as gate 2** — only on PRs that touch
`.github/owners.config.yaml` or an `owners.yaml`. A pull request that changes no ownership file does
not run it and cannot be blocked by it.

#### The regeneration job

After a PR merges to `main`, the job checks out `main`, runs `generate --check`, and on exit 2
renders the file and opens a pull request titled *Regenerate CODEOWNERS*.

- **Fixed head branch `codeowners/regenerate`.** If a PR from that branch is already open, the job
  force-updates the branch and lets the existing PR pick it up rather than opening a second one.
  Several ownership merges landing in quick succession therefore collapse into one regeneration PR
  carrying the union of their effects. This is the same mechanism `audit --fix` uses for its removal
  PR ([Component 8](#component-8-invalid-owner-handling)), and the two jobs deliberately use
  different branches so neither clobbers the other.
- **On exit 1 it does not open a PR.** A validation error on `main` means the tree is in a state
  that cannot render. Opening a PR is impossible and silently skipping is worse, so the job alerts.
  Gate 2 makes this nearly unreachable: a `CFG-*` error cannot merge in the first place.
- **The regeneration PR is reviewed like any other.** It modifies `.github/CODEOWNERS`, which the
  repository-root section assigns to repository maintainers, so it lands in front of the people
  responsible for the file. It is exempt from gate 1 by virtue of its head branch, and it satisfies
  gate 2 trivially because it changes no YAML.

**The staleness window is real and is accepted.** Between an ownership PR merging and the
regeneration PR merging, `main` holds YAML that says one thing and a rendered file that says
another. GitHub routes review requests from the rendered file, so a newly added owner is not
actually assigned reviews until the regeneration PR lands. Two consequences worth stating plainly:
the YAML is authoritative for *intent* but the rendered file is authoritative for *behavior*, and
anyone debugging "why wasn't I asked to review this" should check whether a regeneration PR is open.
The window is bounded by review latency on a mechanical, generated diff, and closing it entirely
would mean letting a bot push straight to `main` — which these repositories do not allow, and which
would remove the only human checkpoint on the file that GitHub actually enforces.

#### Why contributors do not run `generate` themselves

The alternative is that a human runs `generate` and commits both the YAML and the rendered file,
with gate 2 enforcing that they match. It is less infrastructure, and it was the earlier draft.

It was rejected because it makes every ownership PR carry a large generated diff that reviewers
learn to skim, and skimming a generated CODEOWNERS diff is exactly how an unintended ownership
change gets approved. It also gives the contributor a way to be wrong that the tool cannot
distinguish from being right: a stale or hand-adjusted rendered file that happens to render
consistently on their machine but not on someone else's. Separating the two makes the contributor's
diff small and entirely human-readable — a few lines of YAML — and makes the generated file's diff
reviewable on its own, without a behavior change hiding next to it.

#### The unit of validation is the file, not the diff

When a PR touches an ownership file, **every entry in that file is validated**, and every failure is
reported — including failures in entries the PR did not modify. There is no diff-scoped or
"pre-existing failure" suppression. A PR that adds one package to `sdk/ai/owners.yaml` fails if an
unrelated entry elsewhere in that same file is missing `pr-labels`, declares a `..` segment, points
at a path that no longer exists, or names an owner the cache rejects.

This is a deliberate choice with a real cost, so the reasoning matters:

- **Diff-scoped validation is not implementable from the data we have.** The validator's inputs are
  parsed YAML documents, not hunks. Attributing a violation to a diff range means mapping every
  error back to a source line and intersecting it with the patch — and violations like
  `CFG-DUP-002` (two fragments producing the same path) and label union have no single owning line.
  Suppression logic would have to be written per rule, and would be wrong for the relational rules.
- **The file is the unit of review.** The people reviewing a change to `sdk/ai/owners.yaml` are the
  people who own `sdk/ai/`. They are the correct audience for a defect anywhere in that file, and
  they are the only audience that will ever be assembled for it.
- **It is the mechanism that finishes the migration.** `convert` will leave entries that need human
  judgment (a service directory with no PR label, an orphaned path). Nothing forces those to be
  resolved on a schedule. Whole-file validation resolves them on contact: the next person to touch
  the file fixes them, and the file is permanently clean afterward.

The cost is that a contributor can be blocked by a defect they did not introduce. Three things keep
that bounded. Errors carry the file, line, and rule code, and the remediation is local to the file
being edited. The scheduled audit removes invalid owners repository-wide on its own cadence
([Component 8](#component-8-invalid-owner-handling)), so the most common class of unrelated
failure is cleaned up in a separate reviewed PR rather than landing on an unrelated author. And the
condition is self-extinguishing per file — it can only fire once.

This applies to fragments and to `.github/owners.config.yaml` alike. It does not extend across
files: editing `sdk/ai/owners.yaml` does not validate `sdk/storage/owners.yaml`. The exceptions are
the cross-file rules that have no single-file meaning — `CFG-DUP-001`, `CFG-DUP-002`, `CFG-DUP-004`,
and `CFG-LOC-001` — which are evaluated across the whole repository because check 1 re-renders the
whole repository anyway.

#### Why validity checking is scoped to ownership changes

Two independent reasons, and the second only became decisive once check 2 was made to fail closed.

First, relevance: an author can act on a validity problem in ownership they are editing. They cannot
act on one in a service they have never touched, and asking them to would mean committing someone
else's access removal inside an unrelated change — the reviewed-removal flow this design avoids.

Second, blast radius. Check 2 fails closed on an unusable cache, so its trigger decides what an
outage costs. Scoped to ownership files, a cache outage blocks ownership PRs — a small set, and
exactly the set that must not proceed on unverified owner data. Run on every PR, the same outage
would halt all development in every migrated repository. Fail-closed is only a proportionate policy
because the trigger is narrow; the two decisions have to be read together.

Repository-wide validity drift is not check 2's job. The scheduled audit job finds it, removes the
invalid owners from the `owners.yaml` files, and opens a PR for review
([Component 8](#component-8-invalid-owner-handling)).

#### Why check 1 ignores owner validity

Owner validity never reaches the rendered file in the first place
([Component 8](#component-8-invalid-owner-handling)), so check 1 has nothing to exclude. This is what
makes it safe to run on every PR: it is a pure function of repository contents, and no change in
remote cache state can make it fail. Had rendering been validity-filtered, the moment any owner
anywhere lost access **every open PR in the repository** would have failed check 1 — including PRs
that touched no ownership file at all.

#### Concurrent merges

This is the problem the regeneration job removes, and it is worth recording because the earlier
draft had to solve it the hard way.

When contributors committed the rendered file themselves, two PRs each adding a fragment each
carried their own render. If their edits landed in different regions, git merged both cleanly and
`main` ended up with a `.github/CODEOWNERS` matching neither PR's render — and the drift surfaced on
an innocent third pull request, which is the hardest place to diagnose it. Avoiding that required
"Require branches to be up to date before merging" or a merge queue on every ownership PR.

Because contributor PRs no longer contain the rendered file, there is nothing to conflict. Two
ownership PRs touching different fragments merge independently, and the regeneration job renders
from the merged `main`, so its output reflects both. Ordering does not matter and no branch
protection is needed for this.

Two residual races remain, both handled by the fixed head branch:

- **Two regeneration runs overlapping.** The second force-updates `codeowners/regenerate` to the
  render of the newer `main` and updates the open PR in place. The branch always holds the render of
  the most recent `main` it has seen, so a stale render cannot win.
- **A regeneration PR going stale while open.** More ownership merges after the job ran leave the
  open PR rendering an older `main`. The next run supersedes it by the same mechanism. The job must
  therefore render from `main` at run time and force-update — never rebase or merge into its own
  branch, which would combine two renders instead of replacing one.

If two contributors edit the *same* fragment concurrently, they conflict in that fragment, which is
an ordinary source conflict resolved by the people who own that file.

#### Cache availability

`generate` does not consult the owner-validity caches at all — rendering is a pure function of the
YAML. Only two operations read them, and **both fail closed.** There is one rule and no per-operation
exceptions.

A cache is unusable if it is unreachable, empty, older than `AuditRuleCacheSettings.CacheMaxAge`
(6 hours), or **does not parse**. Any of those conditions produces a non-zero exit that names the
cache and the specific failure. Neither operation proceeds on a partial or assumed-empty cache, and
neither downgrades a cache failure to a warning.

| Operation | Unusable cache | Consequence of the alternative |
|-----------|----------------|--------------------------------|
| `generate --check` check 2 (gate 2) | **Fail the check.** | A gate that silently skips is a gate nobody can rely on |
| `audit --fix` (editing YAML) | **Fail. Refuse to fix.** | Opens a pull request deleting owners from source files |

Check 1 is unaffected in all cases; it never consults the cache and stays a pure function of
repository contents.

##### Why the PR gate fails closed too

An earlier draft let check 2 skip and pass on a cache problem, reasoning that a shared blob outage
should not block pull requests. That reasoning is wrong, and it is worth writing down why.

A check that passes when it could not run reports the same result as a check that ran and found
nothing. The signal is indistinguishable from success, so a degraded cache produces green builds
indefinitely and nobody is prompted to fix it. Silent-skip converts an infrastructure outage into a
quiet, open-ended reduction in enforcement — which is precisely the failure mode this design exists
to eliminate, since the whole point of moving ownership into the repository was to stop trusting
artifacts that might be stale.

Failing closed makes the outage loud and short. Someone is blocked, the cache gets fixed, and
enforcement resumes at full strength. A cache refresh is a pipeline run, not a multi-day repair.

##### Why `generate` no longer appears in this table

An earlier draft made owner validity part of the render: an owner the cache rejected was omitted
from the output. That coupled the generated file to a remote, time-varying input, and it needed an
elaborate guard — because "empty cache" and "nobody is a valid owner" are indistinguishable to a
renderer, a cold cache would have produced a syntactically valid CODEOWNERS with almost every
individual owner silently missing.

[Component 8](#component-8-invalid-owner-handling) removes that coupling instead of guarding it.
`generate` reads no caches, so it has no cache failure mode, and the entire class of problem is gone
rather than mitigated.

##### The safety threshold survives, on the one path that mutates

`InvalidOwnerRule` and `TeamNotWriteRule` each carry `SafetyThreshold = 5`. That guard is
**retained**, and it belongs to `audit --fix` — the only operation that deletes owners from the YAML.
`audit --fix` is correspondingly the only command that accepts `--force`.

`generate` does **not** accept `--force`. It reads YAML and writes a derived file; the YAML is
untouched no matter what any cache says. Giving it an override would imply it had something to
override.

###### What the threshold is actually protecting against

`InvalidOwnerRule.Evaluate` already carries four cache defenses: it throws when the team cache has no
members for `azure-sdk-write` (`InvalidOwnerRule.cs:57`), when the org-visibility cache is empty
(`:64`), when the two caches *disagree* about an alias in either direction (`:82`, `:88`), and when
either blob is older than six hours (`:201`).

Those leave exactly one gap: **correlated truncation.** An alias missing from *both* caches produces
`hasWritePermission = false` and `hasAzureOrgEntry = false`, so neither disagreement check fires —
each requires exactly one of the two to be true — and `isValidCodeOwner` evaluates to `false`. A
partially written team blob paired with a similarly short org blob therefore marks every missing
person invalid, silently and without tripping any of the four guards.

The threshold is the only thing that catches that case. It is a circuit breaker for cache defects,
not a policy about people, which is why it counts only *new* removals and leaves recoveries
unbounded — restoring access is the safe direction.

###### Counting is per alias, and "new" means new relative to the checkout

Two things change from the work-item implementation, because the storage shape changed underneath.

**Count distinct aliases, not removal sites.** A work item was a single record referenced by many
packages, so a departing owner contributed exactly 1 to the count. YAML is denormalized: the same
alias can appear in dozens of entries across many files. Counting edit sites would make one ordinary
departure trip a threshold of 5 immediately, and `--force` would become reflexive. Removing an alias
from forty entries counts as **one**.

**"New" is measured against the current state of the repository** — `main` in the scheduled pipeline
— not against a stored history. This is why the `Custom.InvalidSince` ledger can be deleted rather
than replaced. The work-item fix *marked* the owner and left them in place, so every later run
re-detected the same people and needed a field to avoid counting them forever. The YAML fix
*removes* them: once the pull request merges, the alias is no longer in the checkout and cannot be
counted again. The removal is itself the state transition.

Between opening a removal pull request and merging it, re-runs re-detect the same owners. The count
is stable rather than cumulative, so the threshold behaves correctly, but `audit --fix` must
**update its existing open removal pull request instead of opening a second one.** The run
identifies its own prior PR by a fixed head branch name (`codeowners/remove-invalid-owners`) and
force-updates that branch when the set of removals changes.

###### Exceeding the threshold skips fixes; it does not abort the run

Today the threshold throws from `GetFixes`, and `CodeownersAuditHelper` has no `try`/`catch` around
its rule loop (`CodeownersAuditHelper.cs:46-80`). A trip in priority-10 `AUD-OWN-001` therefore kills
the run before `AUD-OWN-002`, `AUD-OWN-003`, and both `AUD-LBL-*` rules evaluate, and the violations
already collected in `response.Violations` are discarded along with the exception — so the error's
claim that "all invalid owners have been logged for review" holds only through `ILogger` warnings,
not through the response.

The new behavior: exceeding the threshold **skips that rule's fixes, continues evaluating every
remaining rule, and exits non-zero** with a message naming the count and the affected aliases. The
full violation report survives, which matters most in precisely the situation that trips the
threshold. The two rules keep independent counters, so a defect confined to the team cache still
trips `AUD-OWN-003` on its own.

The absolute value of 5 is unchanged, and there is no second, un-overridable ceiling. In the
work-item model the threshold *was* the review step, because `UpdateWorkItemAsync` mutated a remote
system immediately, one owner at a time, with no diff and no rollback. In the YAML model the fix
produces a pull request, so a truncated cache yields a reviewable diff that a human closes rather
than a silent mass mutation. A hard ceiling would guard a case review already guards, while leaving
a legitimate reorganization no way through.

`--force` remains the deliberate override for the legitimate case where a large number of owners
really did become invalid at once, such as a team reorganization.

### Component 8: Invalid Owner Handling

**Rendering is a pure function of the YAML in the checkout.** `generate` reads no caches, contacts no
network, and makes no judgment about whether an owner still has access. The same commit renders the
same bytes on any machine at any time. Owner validity is enforced by `audit`, which changes the
repository, and the change is then rendered like any other.

This is a separation of concerns: **`audit` decides what the state of the repository should be;
`generate` renders the state as it is.**

1. `audit` resolves every owner in the YAML against the GitHub caches
   (`azure-sdk-write-teams-blob`, `user-org-visibility-blob`) and reports failures as `AUD-OWN-001` /
   `AUD-OWN-003`.
2. `audit --fix` removes those owners from the `owners.yaml` or `owners.config.yaml` file that
   declared them, re-renders `.github/CODEOWNERS`, and opens a pull request containing both changes.
   Removals are capped by `SafetyThreshold`
   ([above](#the-safety-threshold-survives-on-the-one-path-that-mutates)).
3. The remaining owners review that pull request. Merging it is what removes the owner from
   CODEOWNERS.

The invalid owner therefore stays in the rendered file until the removal PR merges. That is
deliberate, and the window is bounded by the audit schedule.

#### Why filtering at render time was rejected

The alternative — omit invalid owners during rendering, leaving them in the YAML — looks like it
ejects faster. It does not buy what it appears to, and it costs three things.

It buys little because GitHub already enforces this. Per GitHub's documentation, *"The people you
choose as code owners must have write permissions for the repository"*; an entry naming someone
without write access does not produce a review request. Functional ejection happens on GitHub's side
the moment access is revoked, whether or not our file still lists the alias.

The costs are concrete:

- **The generated file stops being verifiable.** Its content would depend on cache state at the
  moment of the run, which no later check can reconstruct. `generate --check` would have to either
  report drift forever or re-query the cache, making structural drift detection network-dependent
  and time-dependent — so a cache outage would block every CODEOWNERS pull request.
- **Ownership could transfer silently.** If filtering emptied an entry's owner list, the renderer
  would have to drop the entry, and under last-match-wins the path would fall through to the
  previous matching entry — usually a broad catch-all. A change in remote cache state would reassign
  a directory's owners with no diff in the YAML and no human in the loop.
- **Intent would become invisible.** The YAML would say one thing, the rendered file another, and
  nothing in the repository would explain the gap.

Under the audit-driven model each of those becomes a reviewed commit in Git history, with the owner
and the rejecting cache named in the pull request description.

### Component 9: Command surface

#### Kept, unchanged name

| Command | Change |
|---------|--------|
| `azsdk config codeowners generate` | Reads YAML instead of Azure DevOps. New `--check`. `--package-types`, `--section`, and `--invalid-owner-lookback-days` removed (sections come from the config; the grace period is gone). |
| `azsdk config codeowners audit` | Rules rebased onto YAML. New `--repo-root`. `--fix` edits YAML instead of work items; `--force` keeps its `SafetyThreshold` override meaning. |
| `azsdk config codeowners check-package` | Resolves ownership from the owning `owners.yaml` fragment instead of a rendered CODEOWNERS artifact. `--codeowners-cache` and the blob fallback are removed. Validation rules unchanged. |
| `azsdk config codeowners export-section` | Unchanged. Operates on the rendered file; its remaining caller is `Test-CodeownersSections.ps1`. |
| `azsdk config codeowners update-cache` | Unchanged trigger. The pipeline it starts now refreshes only the org- and team-membership caches. |
| `azsdk config github-label check` | Unchanged. |
| `azsdk config github-label create` | Unchanged. |

#### Deleted

| Command | MCP tool |
|---------|----------|
| `azsdk config codeowners add-package-owner` | `azsdk_engsys_codeowner_add_package_owner` |
| `azsdk config codeowners add-package-label` | `azsdk_engsys_codeowner_add_package_label` |
| `azsdk config codeowners add-label-owner` | `azsdk_engsys_codeowner_add_label_owner` |
| `azsdk config codeowners remove-package-owner` | `azsdk_engsys_codeowner_remove_package_owner` |
| `azsdk config codeowners remove-package-label` | `azsdk_engsys_codeowner_remove_package_label` |
| `azsdk config codeowners remove-label-owner` | `azsdk_engsys_codeowner_remove_label_owner` |
| `azsdk config codeowners view` | `azsdk_engsys_codeowner_view` |
| `azsdk config github-label sync-ado` | (none) |

Ownership mutation is now a file edit in a pull request. An agent edits `sdk/<service>/owners.yaml`
directly, then runs `generate --check` and `audit`. It does not run `generate` and does not stage
`.github/CODEOWNERS`.

#### `view` is deleted, not reimplemented

`view` answered "who owns this path / what does this user own / who owns this label / who owns this
package" out of Azure DevOps. Every one of those questions is now answered by reading a file in the
repository.

Deleting it rather than porting it is the cheaper answer in both directions. Reimplementing meant a
new `ICodeownersViewHelper`, a `CodeownersViewResponse` reshaped off the deleted work-item types, and
a rewritten test mock — none of which survives from today's implementation anyway, since
`CodeownersManagementHelper` holds all four `GetViewBy*` methods and is deleted in full. And for an
agent, a tool call that returns a filtered projection costs more context than opening
`sdk/<service>/owners.yaml`, which is short, human-readable, and answers the follow-up questions the
projection would have dropped.

Discovery is a `glob` for `owners.yaml` plus a `grep` for an alias or label. That is the same way an
agent already navigates the rest of the repository, so it needs no tool-specific knowledge.

The one thing `view` offered that the file does not is repo-wide "what does this alias own?" across
~150 fragments. `grep -rn '<alias>' --include=owners.yaml` answers it, and answers it with the
declaring file and line — which is strictly better than the work item ID `view` used to return.

#### MCP surface after this change

- `azsdk_engsys_codeowner_check_package`
- `azsdk_engsys_codeowner_update_cache`
- `azsdk_engsys_codeowner_generate` *(new — agents now drive generation after editing YAML)*
- `azsdk_engsys_codeowner_audit` *(new — agents need the validation result to self-correct)*
- `azsdk_check_service_label`
- `azsdk_create_service_label`

Net change: eleven MCP tools become seven, and six write tools become zero. The eleven today are the
nine on `CodeownersTool` plus `azsdk_check_service_label` and `azsdk_create_service_label` on
`GitHubLabelsTool`; six add/remove tools are deleted and two are added.

### Component 10: Audit rules

Audit keeps its purpose — validate ownership against external truth — and rebases its data source
from work items to YAML. Cache-backed rules keep the existing six-hour freshness policy, the
fail-fast behavior, and the `update-cache` remediation path documented in
[`8-operations-codeowners-ownership-audit.spec.md`](./8-operations-codeowners-ownership-audit.spec.md).

| Rule ID | Description | Data dependency | Fix behavior |
|---------|-------------|-----------------|--------------|
| `AUD-OWN-001` | Individual owner fails cached owner validation | `azure-sdk-write-teams-blob`, `user-org-visibility-blob` | Remove owner from YAML; open PR |
| `AUD-OWN-002` | Team alias does not match `Azure/<team>` | YAML only | Report only |
| `AUD-OWN-003` | Team does not descend from `azure-sdk-write` | `azure-sdk-write-teams-blob` | Remove team from YAML; open PR |
| `AUD-OWN-004` | Fragment path entry has fewer than `configs.minimum-path-owners` individual owners | YAML only | Report only |
| `AUD-OWN-005` | Unioned label-owner block has fewer than `configs.minimum-label-owners` individual service owners | YAML only | Report only |
| `AUD-LBL-001` | Label is not present in cached repo label data | `repository-labels-blob` | Report only |
| `AUD-LBL-002` | `Service Attention` used as a PR label or as a sole service label | YAML only | Report only |
| `AUD-PATH-001` | Path expression matches nothing in the repo working tree | Repo checkout | Report only |
| `AUD-ORD-001` | A literal path expression renders after a literal path expression that is a proper prefix of it (ownership inversion) | Rendered output | Report only |

`AUD-PATH-001` is newly implementable. Generation now runs inside a repo checkout, so the legacy
linter's `PATH-001` / `PATH-003` gap closes.

The previous `AUD-STR-001` (label owner with zero owners) and `AUD-STR-002` (label owner with zero
labels) are retired as audit rules. They become schema violations that fail at load, before anything
can be rendered.

##### `AUD-ORD-001` reports ownership inversions

`sort: true` orders entries by primary label before path (see
[Component 6](#why-sort-true-reuses-codeownersentrysorter)), so a descendant whose primary label
sorts before its ancestor's renders above that ancestor. Under last-match-wins the ancestor
catch-all then owns the descendant path and the narrower entry has no effect. The condition also
occurs in `sort: false` sections when entries are authored in that order.

The rule is decidable and cheap, which is what separates it from the rejected `AUD-SEC-001` below.
It compares only **literal** path expressions — any expression containing a glob metacharacter is
skipped entirely — and asks a single question: does an earlier-rendered expression `A` satisfy
`B.StartsWith(A, Ordinal)` for some later-rendered `B`? That is string prefixing on concrete paths,
not the glob-containment analysis this document rejects.

It is **Report only**, and it has no `--fix`. There are two valid resolutions and the tool cannot
choose between them: relabel the descendant so it sorts after its ancestor, or move the section to
`sort: false` and author the order deliberately. Both are ownership decisions.

The rule is expected to fire on existing data. The current azure-sdk-for-net file contains seven
inversions and would report six after conversion; that count is a migration backlog for the owning
teams, not a release blocker, which is why the rule cannot be an error.

##### There is no section-shadowing rule

An earlier draft proposed `AUD-SEC-001`: warn when a guardrail section renders before a
fragment-populated section whose paths it overlaps. It is not specified, because it cannot be
implemented without the glob-containment analysis this document rejects in
[Component 5](#what-is-deliberately-not-checked). Deciding that one section's expressions "overlap"
another's is the same undecidable question, and any approximation would either miss real shadowing
or fire constantly on correct files.

Section ordering stays an authoring decision, reviewed by humans. `AUD-PATH-001` provides the
adjacent signal that is actually decidable: an expression matching nothing in the working tree.

### Component 11: `check-package` source resolution

`check-package` keeps its four validation rules and its output contract. What changes is where it
reads ownership from: **the `owners.yaml` fragment that governs the package directory.**

Given `--directory-path sdk/ai/Azure.AI.Inference`:

1. **Find the governing fragment.** Walk up from the directory until an `owners.yaml` is found —
   `sdk/ai/owners.yaml` here. If none exists, report `no_matching_path`.
2. **Find the path entry.** Match the remainder of the directory path (`Azure.AI.Inference/`) against
   that fragment's `paths` entries. Fall back to the fragment's own-directory entry (`path: .`) when
   nothing more specific matches. If neither matches, report `no_matching_path`.
3. **Read owners and PR labels off that entry.** `owners` supplies the source owners;
   `pr-labels` supplies the PR labels.
4. **Find the service owners in the same file.** Select the `label-owners` block whose `labels` are
   fully contained in the entry's `pr-labels` — the containment rule `CheckPackageHelper` already
   implements (`CheckPackageHelper.cs:215-222`) — and read its `service-owners`.
5. **Validate against the caches.** Individual owners are checked against the org- and
   team-membership caches, subject to [Cache availability](#cache-availability).
6. **Report the same `CheckPackageIssue.Codes` as today.**

Everything the command needs is in one file. The example `sdk/ai/owners.yaml` in
[the assets](./assets/codeowners/sdk-ai-owners.yaml) resolves `Azure.AI.Inference` to owners
`test-user-07, test-user-09, test-user-23`, PR label `AI Model Inference`, and service owners
`test-user-07, test-user-09, test-user-23` — without consulting the config, any other fragment, or the rendered
file.

#### Why not resolve through the renderer

Because `check-package` does not need to. Its question is "does this package declare enough owners to
release?", which is a question about what a team wrote down, and a team writes it down in exactly one
file. The renderer's ordering exists to decide which of several *competing* declarations GitHub will
honor; that is a different question, and the release gate does not ask it.

Resolving through the renderer would mean loading the config, ordering every section, sorting every
fragment, and applying last-match-wins — reproducing the renderer's ordering in a second place that
then has to stay in sync with it forever. The single-file lookup has no ordering to reproduce, so
there is nothing to drift.

#### The accepted limitation

A package whose owners are declared **only** in a static `owners.config.yaml` section, with no
fragment covering its directory, resolves as `no_matching_path`. That is correct in the sense that
the package has no fragment, and it is the intended migration signal: packages are expected to be
owned by fragments.

If a case appears where a static config entry legitimately governs a package directory,
`check-package` gains a second pass that renders the sections and resolves through them. It is
deliberately not built now, because nothing today requires it and it would import the renderer's full
ordering into the release gate for a case that may never occur.

#### The rendered-CODEOWNERS cache is deleted

The `azuresdkartifacts` blob at `cache/azure/<repo>/CODEOWNERS.cache` and everything that produces or
consumes it are removed:

| Artifact | Location | Action |
|----------|----------|--------|
| `Export Client Libraries section` task | `eng/pipelines/pipeline-owners-extraction.yml` | Delete |
| `Upload CODEOWNERS cache` task | `eng/pipelines/pipeline-owners-extraction.yml` | Delete |
| `CacheBaseUrl` constant | `Tools/Config/CodeownersTool.cs` | Delete |
| `--codeowners-cache` option and its blob-download path | `Tools/Config/CodeownersTool.cs` | Delete |
| `cache/azure/<repo>/CODEOWNERS.cache` blobs | `azuresdkartifacts` storage account | Delete after Phase 5 |

The storage account keeps serving the caches that remain genuinely external: organization membership
and team membership. Those describe GitHub state that no repository can derive locally, which is
exactly why they belong in a cache. A rendered CODEOWNERS section never met that test — it was
derived from data this system already owns.

`export-section` is **not** deleted. The pipeline was one of its two callers; the other is
`eng/common/scripts/Test-CodeownersSections.ps1`, which exports a section from two revisions of a
CODEOWNERS file to diff them. That use is unrelated to the cache and continues to work against the
rendered file.

`eng/common/scripts/Test-CodeownersForArtifacts.ps1` calls `check-package --directory-path --repo
--output json` and needs **no change**. `--directory-path` already locates the package inside the
checkout the script runs in, and `--repo` is still required for repo-label validation. It simply
stops reaching for a blob.

Because matching no longer runs against an exported section, there is no `--section` option on
`check-package` and no scope question to settle: resolution happens over the whole loaded model, the
same way GitHub resolves the whole file.

### Component 12: Migration utilities

Migration needs two capabilities that the steady-state command surface deliberately does not have:
producing the first draft of the YAML files, and proving that the rendered output did not change who
owns anything. Both are one-time aids with no role after a repository has converted.

They live in **[`tools/codeowners-migration/`](../../../codeowners-migration/README.md)** as a
standalone .NET tool with two verbs, `convert` and `verify`. They are **not** part of `azsdk-cli`,
and their design is documented with the tool rather than here.

Keeping them out of `azsdk-cli` is a deliberate boundary. The whole point of this design is that
`azsdk-cli` renders and validates but never authors ownership; a `convert` verb inside it would
reintroduce a tool-writes-ownership path that [Component 9](#component-9-command-surface) removes.
`verify` compares two CODEOWNERS files, which is meaningless once the old file is gone. Both
reference `Azure.Sdk.Tools.CodeownersUtils` directly, so neither needs anything from `azsdk-cli`.

Two things the migration tooling depends on are decided by this document and are restated here
because they constrain the migration schedule:

- The three single-line sub-headings in `azure-sdk-for-net/.github/CODEOWNERS` (`Core Libraries`,
  `Eng Sys`, `Code Generation`) are rewritten as ordinary three-line banners in a **prerequisite pull
  request**, before any conversion runs. `CodeownersSectionFinder` is then used unmodified.
- `verify` must prove resolution equivalence, not just declaration equivalence, because the sorting
  this design applies to fragments can reorder entries relative to a catch-all
  ([Component 6](#component-6-rendering-algorithm)).


### Cross-Language Considerations

| Language | Approach | Status |
|----------|----------|--------|
| .NET | `allowed-owner-yaml-paths: ["sdk/*/owners.yaml"]` | **Surveyed.** Reference implementation and first repo migrated |
| Java | Expected same | **Not surveyed.** Multiple artifacts per service directory are ordinary path entries |
| JavaScript | Expected same | **Not surveyed** |
| Python | Expected same | **Not surveyed** |
| Go | `allowed-owner-yaml-paths` expected to include `sdk/resourcemanager/*/owners.yaml` | **Not surveyed.** Deeper nesting is a config value, not a code change |

The renderer, schema, and validation rules are language-agnostic. Everything language-specific lives
in `.github/owners.config.yaml`.

Only .NET has had the heading-style and path-shape analysis that this design was built against. The
four "Expected same" rows are a hypothesis, not a finding: each repo may use a different heading
convention, a layout other than `sdk/<service>/`, or a granularity that makes one fragment per
service wrong. Phase 5 gates each repo's migration on running the same survey first.

### User Experience

Adding an owner to a service:

```bash
$EDITOR sdk/ai/owners.yaml                 # add the alias under the right path entry
azsdk config codeowners generate --check   # confirms the YAML is valid (exit 2 = valid, not yet rendered)
git commit -am "Add @alice as an owner of sdk/ai"
```

That is the whole contributor workflow. **Do not run `generate` and do not commit
`.github/CODEOWNERS`** — a PR containing the rendered file fails gate 1. After the YAML PR merges,
the regeneration job opens a separate PR with the rendered result, and @alice starts receiving
review requests once that PR lands.

The YAML PR is reviewed by the current owners of `sdk/ai/`, because `.github/CODEOWNERS` already
assigns them to that directory.

---

## Alternatives Considered

### Alternative 1: Keep Azure DevOps work items, add a YAML export

**Description:** Keep work items authoritative and export a YAML mirror into the repo for
visibility.

**Pros:** No migration; existing add/remove commands keep working.

**Cons:** Two sources of truth that drift; the mirror is read-only so it does not fix the review-gate
or history problems; the full ADO dependency remains in the tooling.

**Why not chosen:** It addresses only the visibility complaint, which is the least important of the
seven problems listed above.

### Alternative 2: A single monolithic `.github/owners.yaml`

**Description:** Put everything in one repo-level file, no fragments.

**Pros:** Simplest possible renderer; no containment rules; no union logic.

**Cons:** Every ownership change touches one file owned by repo maintainers, producing constant merge
conflicts across ~150 services and forcing maintainer review on routine team changes.

**Why not chosen:** Fragments are what let service teams self-serve, and the fragment directory is
what makes containment enforceable.

### Alternative 3: Named owner aliases in the owners config

**Description:** Allow `owner-aliases: { net-core: [test-user-13, Azure/azure-sdk-write-net-core] }` and
reference them as `owners: [$net-core]`.

**Pros:** The azure-sdk-for-net config repeats `[test-user-13, Azure/azure-sdk-write-net-core]` dozens of
times.

**Cons:** Adds indirection to a file whose main virtue is being greppable for an alias.

**Why not chosen for v1:** Deferred to [Open Questions](#open-questions). It is additive and can
land in schema `version: 2` without breaking anything.

---

## Open Questions

- [ ] **Owner aliases**: Should `configs.owner-aliases` be added in v1?
  - Context: The .NET config repeats the same owner pair heavily. Aliases reduce that, but break
    `grep test-user-13 .github/owners.config.yaml`.
  - Options: (a) ship v1 without aliases; (b) ship aliases and add `view --github-user` as the
    supported way to answer "what do I own"; (c) ship aliases and have `generate` emit an expanded
    lockfile.

- [ ] **Fragment granularity for large services**: Should a fragment be permitted at
      `sdk/<service>/<package>/owners.yaml` as well as `sdk/<service>/owners.yaml`?
  - Context: `allowed-owner-yaml-paths` already permits it, but two fragments in the same subtree
    make `CFG-DUP-002` and containment reporting more confusing.
  - Options: (a) allow it and rely on `CFG-DUP-002`; (b) require exactly one fragment per matched
    glob depth; (c) allow it only when the deeper glob is listed explicitly.

- [ ] **Orphaned paths reporting**: The .NET CODEOWNERS carries a hand-maintained comment block
      listing unowned paths. Sparse rendering means it cannot survive as a comment in the generated
      file. Where should it go?
  - Context: `AUD-PATH-001` computes the inverse (declared paths that do not exist). The orphan list
    is (paths that exist) minus (paths declared), which is more expensive and noisier.
  - Options: (a) drop it from the file and emit it only in the audit report; (b) keep it as a YAML
    comment in the owners config, refreshed by `audit --fix`; (c) do not track it at all.

- [ ] **Migration cutover per repo**: Should the two systems run in parallel for a period, with the
      YAML renderer writing only into a subset of sections?
  - Context: `generate` already targets specific sections today. A partial cutover is possible but
    means `generate --check` cannot gate the whole file until the last section migrates.
  - Options: (a) big-bang per repo; (b) section-by-section with the drift gate enabled last.

---

## Success Criteria

This feature is complete when:

- [ ] `.github/owners.config.yaml` and `sdk/*/owners.yaml` in `Azure/azure-sdk-for-net` render, via
      `azsdk config codeowners generate`, to a `.github/CODEOWNERS` that is semantically equivalent
      to the pre-migration file (same owners resolve for every path in the repo).
- [ ] A pull request that hand-edits `.github/CODEOWNERS` fails gate 1 with a message pointing at
      <https://aka.ms/azsdk/codeowners>, regardless of whether the edit happens to match what
      `generate` would produce.
- [ ] A pull request that changes only ownership YAML passes gate 2 without regenerating
      `.github/CODEOWNERS` (`generate --check` exit 2), and the regeneration job opens a PR carrying
      the rendered result after it merges.
- [ ] Two ownership PRs merging in quick succession produce one regeneration PR, not two.
- [ ] A service team can add or remove an owner by editing only `sdk/<service>/owners.yaml`, and the
      resulting PR is routed to the existing owners for review.
- [ ] A fragment that declares `../some-other-service` fails immediately with `CFG-PATH-001`.
- [ ] A fragment path entry with no `pr-labels` fails with `CFG-LBL-001`, while a static path entry
      in the owners config with no `pr-labels` renders unchanged.
- [ ] A PR that edits one entry in `sdk/ai/owners.yaml` fails on a validation error in a different,
      unmodified entry in that same file.
- [ ] Two fragments declaring the same label set render one `# ServiceLabel:` block with unioned
      owners and a `# Sources:` comment naming both files.
- [ ] The rendered file contains no comment other than the fixed banner, the monikers, and
      `# Sources:` lines; `CodeownersParser` re-parses it with zero block errors.
- [ ] `audit` reports `AUD-OWN-004` / `AUD-OWN-005` for path entries and unioned label-owner blocks
      that fall below `configs.minimum-path-owners` / `configs.minimum-label-owners`.
- [ ] An owners config section that duplicates a fragment path or label set fails with
      `CFG-DUP-001` / `CFG-DUP-004`.
- [ ] `check-package` passes and fails identically before and after migration for a sampled set of
      packages across all five language repos.
- [ ] `CodeownersTool` has no Azure DevOps work item dependency, and the `Owner`, `Label`, and
      `Label Owner` work item types are retired.
- [ ] All five language repos are migrated.

---

## Agent Prompts

### Add an owner to a service

**Prompt:**

```text
Add @alice as an owner of sdk/ai in azure-sdk-for-net.
```

**Expected Agent Activity:**

1. Locate `sdk/ai/owners.yaml`.
2. Add `alice` to the `owners` list of the `path: .` entry.
3. Run `azsdk config codeowners generate --check` to confirm the file still validates. Exit 2 is the
   expected result and means success.
4. Run `azsdk config codeowners audit --repo Azure/azure-sdk-for-net` and report any
   `AUD-OWN-*` finding for `alice` (for example, not a member of an `azure-sdk-write` team).
5. Remind the user to open a PR containing **only** the YAML change, and that `.github/CODEOWNERS`
   is regenerated by a follow-up bot PR after theirs merges. Do not run `generate` and do not stage
   `.github/CODEOWNERS`.

### Onboard a new service

**Prompt:**

```text
Create ownership for the new sdk/contoso service. Owners are @alice and @bob, PR label "Contoso".
```

**Expected Agent Activity:**

1. Confirm `sdk/*/owners.yaml` is an allowed fragment path in `.github/owners.config.yaml`.
2. Create `sdk/contoso/owners.yaml` with a `path: .` entry (with `pr-labels: [Contoso]`, which is
   required) and a `label-owners` entry for `Contoso`.
3. Run `azsdk config github-label check contoso` and offer `create` if the service label is missing.
4. Run `azsdk config codeowners generate --check` and `azsdk config codeowners audit`.
5. Report the block that *will* land in the `Client Libraries` section once the regeneration job
   runs, and remind the user not to commit `.github/CODEOWNERS`.

### Diagnose a duplicate-definition failure

**Prompt:**

```text
generate is failing with CFG-DUP-001 for /sdk/ai/Azure.AI.Inference/. What do I do?
```

**Expected Agent Activity:**

1. Read the error, which names every contributing file and line.
2. Determine which of the two declarations is authoritative.
3. Recommend removing the other, and explain the consequence of leaving it: the later-rendering
   declaration silently wins under last-match-wins.
4. Apply the fix and re-run `generate`.

### Explain who owns a path

**Prompt:**

```text
Who owns sdk/storage/Azure.Storage.Blobs and where is that defined?
```

**Expected Agent Activity:**

1. Read `sdk/storage/owners.yaml` and locate the path entry matching
   `sdk/storage/Azure.Storage.Blobs`.
2. Report the matched owners, PR labels, and the service owners from the `label-owners` block
   carrying those PR labels, citing the file and line that declares each.

### Check release readiness

**Prompt:**

```text
Check whether sdk/storage/Azure.Storage.Blobs has enough CODEOWNERS coverage to unblock release.
```

**Expected Agent Activity:**

1. Call `azsdk_engsys_codeowner_check_package` with `directoryPath` and optional `repo`.
2. Report the matched owners, PR labels, and service owners, or the validation failure and the
   `owners.yaml` edit that would fix it.

---

## CLI Commands

### Generate CODEOWNERS

**Command:**

```bash
azsdk config codeowners generate --repo-root .
```

**Options:**

- `--repo-root <path>`: Repository root. Defaults to the current git repo root.
- `--config <path>`: Owners config path. Default `<repo-root>/.github/owners.config.yaml`.
- `--output <path>`: Override `configs.output`.
- `--check`: Do not write. Validate all ownership YAML, render in memory, and compare against the
  committed file. Exit **1** on a `CFG-*` validation error, **2** when the YAML is valid but the
  committed file is stale, **0** when it is valid and in sync. Only exit 1 fails the PR gate; exit 2
  is the expected state of a contributor's PR and is what triggers the regeneration job.

**Expected Output:**

```text
Loaded .github/owners.config.yaml (12 sections)
Loaded 147 owners.yaml fragments
Rendered 1,004 lines to .github/CODEOWNERS

✓ 0 validation errors
✓ 3 label sets unioned across multiple fragments
```

**Error Cases:**

```text
✗ CFG-PATH-001: sdk/ai/owners.yaml:14 - path '../storage' contains a '..' segment.
  Fragments may only declare paths at or below their own directory (sdk/ai/).

✗ CFG-DUP-004: Label set [AI Projects] is declared in both
    .github/owners.config.yaml:88 (section 'Core Libraries')
    sdk/ai/owners.yaml:22
  Label-owner union is only supported between owners.yaml fragments.

Generation aborted. .github/CODEOWNERS was not modified.
```

### Check for generation drift

**Command:**

```bash
azsdk config codeowners generate --check
```

**Expected Output:**

```text
✓ .github/CODEOWNERS is up to date.
```

**Drift (exit 2 — not a failure on a contributor's PR):**

```text
● .github/CODEOWNERS is out of date (exit 2).

  --- committed
  +++ rendered
  @@ -412,7 +412,7 @@
   # PRLabel: %AI Projects
  -/sdk/ai/Azure.AI.Projects/    @test-user-07 @test-user-18 @test-user-23
  +/sdk/ai/Azure.AI.Projects/    @test-user-07 @test-user-18 @test-user-22 @test-user-23 @test-user-24

  The YAML is valid. Do not regenerate or commit .github/CODEOWNERS — the
  regeneration job opens a pull request with this diff after your change merges.
  See https://aka.ms/azsdk/codeowners
```

**Error Cases (exit 1):**

```text
✗ CFG-LBL-001: sdk/ai/owners.yaml:23 - path entry 'Azure.AI.Projects.Agents/' has no 'pr-labels'.
  pr-labels is required on fragment path entries. Every entry in this file is
  validated, including ones your change did not touch.

Nothing was rendered.
```

**Gate 1 (a hand edit to the generated file):**

```text
✗ This pull request modifies .github/CODEOWNERS, which is a generated file.

  Revert the change and edit the owners YAML instead:
    .github/owners.config.yaml   repository-wide sections and static entries
    sdk/<service>/owners.yaml    per-service ownership

  See https://aka.ms/azsdk/codeowners
```

### View ownership

There is no `view` command. Ownership is read by opening the files:

```bash
cat sdk/ai/owners.yaml                       # who owns this service
grep -rn test-user-07 --include=owners.yaml .    # what does this alias own
grep -rn "AI Projects" --include=owners.yaml . --include=owners.config.yaml
```

Results carry the declaring file and line, which is what `view` reported and more besides — the
surrounding entries stay visible. See
[`view` is deleted](#view-is-deleted-not-reimplemented).

### Audit ownership

```bash
azsdk config codeowners audit --repo Azure/azure-sdk-for-net
azsdk config codeowners audit --repo Azure/azure-sdk-for-net --fix
```

**Options:**

- `--repo <owner/name>`: Repository identity used for repo label validation.
- `--repo-root <path>`: Repository root.
- `--fix`: Remove invalid owners from the YAML files and open a pull request with the change.
- `--force`: Overrides the `SafetyThreshold` of 5 distinct aliases proposed for removal in a single
  run, for the legitimate case where many owners became invalid at once. Does **not** bypass the
  empty-cache or cache-freshness guards — see [Cache availability](#cache-availability).

### Check package ownership

```bash
azsdk config codeowners check-package --directory-path sdk/storage/Azure.Storage.Blobs --repo Azure/azure-sdk-for-net
```

**Options:**

- `--directory-path <relative-path>`: Required. The package directory to resolve.
- `--repo <owner/name>`: Repository identity, used for repo label validation.
- `--repo-root <path>`: Repository root. Defaults to the enclosing git checkout.

`check-package` resolves ownership from `.github/owners.config.yaml` and the `owners.yaml`
fragments, not from a rendered CODEOWNERS file. It therefore has **no** `--codeowners-path`,
`--codeowners-cache`, or `--section` option — there is no rendered artifact to point it at and no
section to scope it to.

Output shape, issue codes, and `--output json` are unchanged from the previous design.

### Export a section

```bash
azsdk config codeowners export-section --codeowners-path .github/CODEOWNERS --section "Client Libraries" --output-file out/CODEOWNERS.client
```

Operates on a rendered CODEOWNERS file. Retained for `Test-CodeownersSections.ps1`, which exports the
same section from two revisions to diff them.

### Refresh caches

```bash
azsdk config codeowners update-cache
```

Refreshes the organization-membership and team-membership caches. It no longer produces a rendered
`CODEOWNERS.cache` artifact; see
[The rendered-CODEOWNERS cache is deleted](#the-rendered-codeowners-cache-is-deleted).

### Service labels

```bash
azsdk config github-label check azure-openai
azsdk config github-label create azure-openai --link https://learn.microsoft.com/azure/ai-services/openai/
```

---

## Implementation Plan

### Phase 1: Schema, loader, and renderer

- Milestone: `azsdk config codeowners generate` renders `.github/CODEOWNERS` from YAML with full
  `CFG-*` validation, behind a new code path that does not disturb the existing one.
- New code:
  - `Models/Codeowners/OwnersConfig.cs`, `OwnersFragment.cs`, `OwnersPathEntry.cs`,
    `OwnersLabelOwnerEntry.cs`
  - `Helpers/Codeowners/IOwnersConfigLoader.cs` / `OwnersConfigLoader.cs` (YamlDotNet 16.3.0, already
    referenced by `Azure.Sdk.Tools.Cli.csproj`)
  - `Helpers/Codeowners/ICodeownersRenderHelper.cs` / `CodeownersRenderHelper.cs`
  - `Helpers/Codeowners/Rules/Config/*.cs` for the `CFG-*` rules
- Reused unchanged: `CodeownersEntry`, `CodeownersEntry.FormatCodeownersEntry`,
  `CodeownersSectionFinder`, `CodeownersParser`, `CodeownersEntrySorter.NormalizeLabel`, and
  `CodeownersEntrySorter.SortEntries` (the ordering applied to `sort: true` sections).
- **Not** reused *directly*: `CodeownersEntrySorter.NormalizePath`. The renderer defines its own
  lexical normalization for duplicate detection and path containment, for the reason given in
  [Component 6](#component-6-rendering-algorithm). `NormalizePath` is left exactly as it is —
  `SortEntries` calls it internally at `CodeownersEntrySorter.cs:155`, so it is neither dead nor safe
  to change, and rendering a `sort: true` section does depend on its current behavior transitively.
- `CodeownersEntrySorter.SortOwnersInPlace` and `SortLabelsInPlace` are **not** called. Owners and
  labels render in authored order; see
  [Owners and labels are never sorted](#owners-and-labels-are-never-sorted).
- Dependencies: none.

### Phase 2: Migration of azure-sdk-for-net

- Milestone: `.github/owners.config.yaml` plus `sdk/*/owners.yaml` produce a CODEOWNERS that is
  semantically equivalent to the current file.
- Build the two migration utilities under `tools/codeowners-migration/`, per
  [their README](../../../codeowners-migration/README.md). They are standalone; they reference
  `Azure.Sdk.Tools.CodeownersUtils` and are not part of `azsdk-cli`.
- **Prerequisite pull request against `azure-sdk-for-net`**: rewrite the three single-line
  sub-headings — `Core Libraries`, `Eng Sys`, and `Code Generation` — as ordinary three-line
  banners. This is a comment-only change with no effect on ownership resolution, and it lets the
  converter use `CodeownersSectionFinder` unmodified.
- Run `convert` to produce the first draft, then hand-review it. Conversion is expected to leave a
  small number of glob-headed paths static and to report them; those stay in the config.
- Prove equivalence with `verify`, supplying `--repo-root` so that path resolution actually runs. A
  run that resolved zero paths proves nothing.
- Order of operations: self-compare the current file first to establish the comparer is clean, then
  compare current against rendered.
- Dependencies: Phase 1.

### Phase 3: Enforcement

- Milestone: gate 1 rejects hand edits to `.github/CODEOWNERS`; gate 2 validates every touched
  ownership file in full; the regeneration job opens PRs from `main`; `check-package` resolves from
  the YAML.
- Pipeline changes in `eng/pipelines/pipeline-owners-extraction.yml`:
  - delete the `Export Client Libraries section` and `Upload CODEOWNERS cache` tasks, and the
    `generate` invocation that fed them, **gated per repo** — these steps run per target repository,
    so they may only be removed for a repo that has completed Phase 2. Removing them while only .NET
    has migrated breaks `check-package` for the other four.
  - keep `BuildTeamCache`. It produces the org- and team-membership caches, which remain the only
    things the storage account serves.
  - the `GenerateCodeowners` stage is **repurposed, not deleted**. It already contains the only
    clone and `create-pull-request.yml` invocation in the file. Strip the removed `--package-types`
    and `--section` options from its `generate` call, point it at the YAML, and reuse its
    clone/create-PR machinery for the scheduled audit job below rather than building new
    infrastructure.
- **The audit job needs a new home.** `audit --fix` is specified to rewrite owners YAML and open a
  pull request, and `AUD-PATH-001` needs a working tree to resolve path expressions against. Today
  the equivalent step runs inside `BuildTeamCache` with `workingDirectory:
  tools/azsdk-cli/Azure.Sdk.Tools.Cli` — there is no checkout of the target repository and no
  create-PR step, so neither behavior can execute there. Phase 3 must add a scheduled job that
  checks out each target repo, runs `audit --fix` against it, and opens the PR. Until that job
  exists, `audit` is report-only.
- Dependencies: Phase 2.

### Phase 4: Deletion

- Milestone: Azure DevOps work-item code paths are gone from `azsdk-cli`.
- Delete:
  - `Helpers/Codeowners/ICodeownersManagementHelper.cs`, `CodeownersManagementHelper.cs`
  - `Helpers/Codeowners/ICodeownersGenerateHelper.cs`, `CodeownersGenerateHelper.cs`
  - the six add/remove commands and their MCP tools in `Tools/Config/CodeownersTool.cs`
  - the `sync-ado` command in `Tools/Config/GitHubLabelsTool.cs`
  - `IDevOpsService.GetGitHubLableWorkItemsAsync` / `CreateGitHubLableWorkItemAsync` and their
    implementations (this also retires the legacy `GitHubLableWorkItem` misspelling)
  - `Models/AzureDevOps/OwnerWorkItem.cs`, `LabelWorkItem.cs`, `LabelOwnerWorkItem.cs`,
    `PackageWorkItem.cs`
  - `Models/Codeowners/WorkItemData.cs`, `Models/Codeowners/WorkItemMappers.cs`
  - `Models/GitHubLabelWorkItem.cs` and `Models/Responses/GitHubLabelSyncResponse.cs`
    (`sync-ado` is the only consumer)
  - `Models/Responses/Codeowners/CodeownersModifyResponse.cs` (orphaned with the six write commands)
  - the `view` command, its `CodeownerViewToolName` MCP tool, and
    `Models/Responses/Codeowners/CodeownersViewResponse.cs`. `ViewCodeowners` delegates entirely to
    `CodeownersManagementHelper.GetViewBy*`, so it has no implementation once that helper is
    deleted — see [`view` is deleted](#view-is-deleted-not-reimplemented).
  - the `CacheBaseUrl` constant, the `--codeowners-cache` option, and the blob-download path in
    `CheckPackage` (`Tools/Config/CodeownersTool.cs`) — `check-package` no longer reads a rendered
    CODEOWNERS artifact from storage. `export-section` itself is **kept**;
    `Test-CodeownersSections.ps1` still calls it.
  - `Helpers/Codeowners/Rules/LabelOwnerMissingOwnersRule.cs`,
    `LabelOwnerMissingLabelsRule.cs` (replaced by schema validation)
  - `Azure.Sdk.Tools.Cli.Tests/Helpers/CodeownersManagementHelperTests.cs`,
    `CodeownersGenerateHelperTests.cs`, `Tests/Models/Codeowners/WorkItemMappersTests.cs`,
    `Tests/TestHelpers/WorkItemDataBuilder.cs`
  - the corresponding DI registrations in `Services/ServiceRegistrations.cs`

#### Nothing in the work-item chain is shared

An earlier draft of this plan kept `WorkItemData.cs` and `WorkItemMappers.cs` on the grounds that
release planning uses `MapToPackageWorkItem` and `GetLatestPackageVersions`. **That is incorrect and
the plan has been corrected.** Verified against the tree:

- `MapToPackageWorkItem` and `GetLatestPackageVersions` are called only from
  `CodeownersAuditHelper`, `CodeownersManagementHelper`, `CodeownersGenerateHelper`, and codeowners
  tests. There are no release-planning callers.
- Release planning uses a different model entirely — `DevOpsService.MapPackageWorkItemToModel`
  produces `PackageWorkitemResponse`. The type `PackageWorkItem` has no references outside the
  codeowners code.
- The only apparent external users of `WorkItemMappers.GetFieldValue` and `ExtractRelatedIds` are in
  `Tests/TestHelpers/WorkItemDataBuilder.cs`, which is itself a codeowners test helper being
  deleted — and its `GetFieldValue` is a private local copy, not the mapper's.

So the whole chain deletes outright. `PackageWorkItem.cs` in particular **must** be deleted or
edited, because it declares `List<OwnerWorkItem> Owners`, `List<LabelWorkItem> Labels`, and
`List<LabelOwnerWorkItem> LabelOwners`; leaving it in place while deleting those three types is a
guaranteed compile error.

- **Rewrite every kept audit rule.** All five are typed against the work-item model and none survives
  unchanged:
  - `AuditContext.WorkItemData` is typed `WorkItemData`; it becomes the loaded YAML model.
    `AuditViolation` is keyed on `int? WorkItemId` and serializes `work_item_id`; it gains source
    file and line instead. Both live in `Models/Codeowners/AuditModels.cs`, which must be reshaped —
    note that changing `work_item_id` is a JSON contract change for any consumer of audit output.
  - `InvalidOwnerRule` (AUD-OWN-001) iterates `context.WorkItemData.Owners.Values` keyed on
    `WorkItemId`; it must iterate owners across the config and fragments, keyed on source file and
    line. Its `GetFixes` calls `devOpsService.UpdateWorkItemAsync(ownerId, …)` and must edit YAML
    instead.
  - `TeamNotWriteRule` (AUD-OWN-003) has the identical `UpdateWorkItemAsync` coupling in `GetFixes`
    and needs the same treatment.
  - `MalformedTeamRule` (AUD-OWN-002) iterates `context.WorkItemData.Owners.Values`.
  - `LabelNotInRepoLabelsRule` (AUD-LBL-001) derives which repos use a label from
    `LabelOwner.Repository` and `Package.Language` via `BuildLabelToReposMap`. That derivation has no
    analogue in a per-repo YAML model and must be redesigned around the single repo under audit.
  - `ServiceAttentionMisuseRule` (AUD-LBL-002) branches on `LabelOwner.LabelType`
    (`PR Label` / `Service Owner` / `Azure SDK Owner`) and `Package.Labels`. In the YAML model the
    label's role is implied by which key declares it (`pr-labels`, `service-owners`,
    `azure-sdk-owners`), so the branch is rewritten against that.
- **Add and register the new audit rules.** `AUD-OWN-004`, `AUD-OWN-005`, and `AUD-PATH-001` are
  described in [Component 10](#component-10-audit-rules) but no earlier phase creates them. They are
  implemented here and registered in `Services/ServiceRegistrations.cs` alongside the removal of the
  two retired rules' registrations.
- **Plumb `--repo-root`.** The `audit` command currently takes only `--fix`, `--force`, and `--repo`.
  YAML-editing fixes need a checkout to operate on.
- **Drop the now-unused `IDevOpsService` injection** from `GitHubLabelsTool`'s constructor once
  `sync-ado` is gone.
- **Update the test project** in the same change, or the build breaks:
  - `Tests/Helpers/Codeowners/Rules/AuditRuleTests.cs` instantiates the deleted rules.
  - `Tests/Tools/Config/CodeownersToolsTests.cs` constructs `CodeownersTool` with
    `Mock<ICodeownersManagementHelper>` and `Mock<ICodeownersGenerateHelper>`, tests the six deleted
    commands against `OwnerWorkItem` / `LabelWorkItem`, and calls `CheckPackage` with the renamed
    `codeownersCachePath` option. It needs a full rewrite, not an edit.
  - `Tests/Mocks/Services/MockDevOpsService.cs` carries explicit interface implementations
    `IDevOpsService.GetGitHubLableWorkItemsAsync` and `CreateGitHubLableWorkItemAsync`. Removing
    those interface members without removing these produces CS0539.
  - `Azure.Sdk.Tools.Mock/Handlers/Config/ConfigHandlers.cs` has mock handlers for the deleted
    commands that become orphaned, including `CodeownerViewHandler`; delete them.
  - `ToolPromptCoverageTests.ExemptTools` still lists the six deleted tool names; trim it.
  - `ToolPromptCoverageTests.AllToolsHaveTestPrompts` fails until `TestPrompts.json` gains entries
    for the new MCP tools.
- `IDevOpsService`, `IGitHubService`, `IGitHelper`, `IPowershellHelper`, `ITeamUserCache`,
  `LabelHelper`, `RepoLabelCache`, `UserOrgVisibilityCache`, and `CacheValidator` are shared and
  are **kept**.
- **Dependencies: Phase 5 complete in every language repo.**

#### Why deletion waits for Phase 5

An earlier ordering ran Phase 4 before Phase 5. That was wrong. Phase 4 deletes
`CodeownersGenerateHelper` — the only code that can read ownership out of Azure DevOps — while
Phase 5 is what migrates Java, JavaScript, Python, and Go. Between the two, four of the five repos
would still depend on work-item data that nothing could read.

It also defeated the point of deferring Phase 4b. Phase 4b preserves the work items as a rollback
fallback, but a fallback whose reader has already been deleted is not a fallback.

The cost of waiting is carrying dead code through Phase 5, which is cheap and reversible. The cost
of not waiting is an unmigrated repo with no working generator, which is neither.

### Phase 4b: Retire the Azure DevOps work item types

- Milestone: the `Owner`, `Label`, and `Label Owner` work item types are retired. `Package` work
  items stay; they are used by release planning, not by ownership.
- **Dependencies: Phase 4 complete.**
- This is the only irreversible step in the plan and is ordered last. Once Phase 4 has removed the
  reading code and every repo is migrated, the data itself can go.

### Phase 5: Remaining language repos

- Milestone: Java, JavaScript, Python, and Go repos migrated using the Component 12 utilities.
- Go passes a deeper `--fragment-glob` (`sdk/resourcemanager/*/owners.yaml`); this is a command-line
  value, not a code change.
- Each repo needs the same survey `azure-sdk-for-net` received before it starts: heading style,
  path shape, and whether `sdk/<service>/` is the right fragment granularity. The
  `Cross-Language Considerations` table records the answers. Do not assume the .NET layout carries
  over — that table's "Expected same" rows are a hypothesis, not a finding.
- Once the last repository has migrated and its `verify` run is clean, `tools/codeowners-migration/`
  is deleted along with its pipeline. Nothing in the steady state depends on it.
- Delete the `cache/azure/<repo>/CODEOWNERS.cache` blobs from the `azuresdkartifacts` storage
  account. This waits until every repo has migrated because the blob is per-repo and unmigrated repos
  still read it.
- Dependencies: Phase 3 complete in `azure-sdk-for-net`.

#### Execution order

The phase numbers are not the execution order. Phase 5 runs **before** Phase 4:

```text
Phase 1 -> Phase 2 -> Phase 3 (net) -> Phase 5 (java, js, python, go) -> Phase 4 -> Phase 4b
```

Phase 4 is the code deletion and Phase 4b is the data retirement; both wait until every repository is
migrated.

### Code Dependency Summary

| Dependency | Type | Used by | Purpose |
|------------|------|---------|---------|
| `YamlDotNet` 16.3.0 | External library | `OwnersConfigLoader` | Deserialize owners config and fragments. Already referenced. |
| `System.CommandLine` | External library | `CodeownersTool`, `GitHubLabelsTool` | Command definitions. |
| `ModelContextProtocol.Server` | External library | `CodeownersTool`, `GitHubLabelsTool` | MCP tool registration. |
| `Octokit` | External library | `GitHubService` | GitHub API for label check/create. |
| `Azure.Sdk.Tools.CodeownersUtils` | Internal project | `CodeownersRenderHelper`, `CheckPackageHelper`, `CodeownersTool` | Entry model, formatting, parsing, section finding, caches. |
| `IGitHubService` | Internal service | `GitHubLabelsTool`, audit rules | GitHub identity and label operations. |
| `IDevOpsService` | Internal service | `CodeownersTool.update-cache` | Starts the cache refresh pipeline only. |
| `IGitHelper` | Internal helper | `CodeownersTool` | Repo root and repo full-name discovery. |
| `ICacheValidator`, `ITeamUserCache`, `RepoLabelCache`, `UserOrgVisibilityCache` | Internal | Audit rules | Cache freshness and cached GitHub truth. |
| `IPowershellHelper` | Internal helper | — | No longer needed by generation; package discovery is not part of rendering. |

---

## Testing Strategy

### Unit Tests

- **Loader**: valid schema; unknown keys; missing required keys; non-canonical key spellings
  (`pr-label`, top-level `labels`, `notes`, `header`); version mismatch.
- **Path containment**: `..` in every position (`../x`, `a/../../b`, `./..`, `a/..`); leading `/` in a
  fragment; absolute and drive-qualified paths; glob expressions inside the fragment subtree; `.`
  resolution.
- **Normalization**: trailing-slash rules for directories, globs, and files; `@` and `%` stripping;
  case-insensitive de-duplication of owners and labels keeping the first occurrence; owners and
  labels are **not** reordered; owners and labels compare `OrdinalIgnoreCase` while path expressions
  compare `Ordinal`.
- **Path case sensitivity**: `/sdk/Tables/` and `/sdk/tables/` do not collide under `CFG-DUP-001`
  and both render; `@test-user-13` and `@test-user-13` de-duplicate to the first-authored spelling.
- **Union**: identical label sets in different orders and cases merge; sets differing by one label do
  not; owner union concatenates in provenance order with first-occurrence-wins de-duplication and is
  not sorted; the rendered `# ServiceLabel:` casing comes from the first contributor; `# Sources:`
  provenance content and ordering; single-fragment blocks still get provenance; config-only blocks
  do not.
- **Duplicate validation**: each of `CFG-DUP-001` through `CFG-DUP-004`, applied uniformly with no
  section-level exemption; a broad guardrail glob overlapping a narrower fragment path does **not**
  fire, because matching is exact.
- **Exact-match duplicate semantics**: `/sdk/tables/` collides with `/sdk/tables/` but not with
  `/sdk/tables/Azure.Data.Tables/`; `/sdk/**/ci.yml` does not collide with `/sdk/tables/ci.yml`;
  collision is detected regardless of which section each declaration sits in.
- **Ordering — `sort: false`**: static entries render in config declaration order; fragment entries
  follow in provenance order (fragment path `Ordinal` ascending, then declaration order within each
  fragment); adding a fragment does not disturb the relative order of the others.
- **Ordering — `sort: true`**: the section is ordered by `CodeownersEntrySorter.SortEntries`;
  a static entry and a fragment entry with the same primary label and path sort together with no
  positional privilege for either; a label-less entry sorts above every labelled entry; label-owner
  blocks anchor after the last path entry carrying any of their labels, tie-broken by joined label
  string; unmatched blocks append.
- **Ordering — invariants**: owners and labels within an entry are never reordered under either
  setting; `sort` and `defined-in-files` are honored independently in all four combinations; section
  banner widths.
- **`AUD-ORD-001`**: fires when a literal ancestor path renders after a literal descendant; does not
  fire when the ancestor renders first; never fires on an expression containing a glob
  metacharacter; reports and does not offer a fix.
- **Whitespace**: one blank line after the file banner, one before and after each section banner,
  one between entries, no trailing whitespace, exactly one terminating newline.
- **Sparse rendering**: YAML comments in the owners config and in fragments never appear in the
  output; the banner is byte-identical across repos; the only comments emitted are the banner,
  `# Sources:`, and monikers.
- **Required PR labels**: a fragment path entry with `pr-labels` absent, `null`, or `[]` fails with
  `CFG-LBL-001`; the same shapes on a static owners config path entry render without a `# PRLabel:`
  moniker and produce no diagnostic.
- **Exit codes**: `generate --check` returns 0 on a valid, in-sync tree; 1 on any `CFG-*` error; and
  2 on valid YAML whose render differs from the committed file. Exit 1 takes precedence over exit 2
  when a tree is both invalid and stale.
- **Whole-file validation**: a file containing one valid and one invalid entry reports the invalid
  one regardless of which entry a simulated diff touched; multiple independent violations in one
  file are all reported in a single run rather than stopping at the first; a violation in a
  *different* ownership file that the PR did not touch is not reported by check 2, while the
  cross-file rules (`CFG-DUP-001`, `CFG-DUP-002`, `CFG-DUP-004`, `CFG-LOC-001`) still are.
- **Minimums**: `AUD-OWN-004` and `AUD-OWN-005` fire below threshold and stay silent at or above it;
  `AUD-OWN-005` is evaluated after union, so two fragments contributing one service owner each
  satisfy `minimum-label-owners: 2`; team aliases do not count toward either minimum.
- **Determinism**: rendering the same inputs twice is byte-identical; rendering with fragments
  enumerated in reverse order is byte-identical.
- **Round trip**: the rendered asset re-parses through `CodeownersParser` with zero block errors, and
  `# Sources:` comments neither start nor terminate a block.

### Integration Tests

- Render the spec assets (`owners.config.yaml` + the two fragments) and assert byte equality with
  `assets/codeowners/CODEOWNERS.rendered`. This is the executable contract for the whole design.
  The asset is generated mechanically by the renderer, never hand-edited; it currently parses to 52
  entries (38 with paths, 14 label-only) across 12 discoverable sections with zero block errors.
- `generate --check` returns 0 on a clean, in-sync tree; 1 on a `CFG-*` error; and 2 when valid YAML
  has not yet been rendered. Gate 1 rejects a hand edit to `.github/CODEOWNERS` on the changed-file
  list alone, including one that renders identically.
- `check-package` against the **YAML assets** (not the rendered file) for a package that passes, and
  one that fails each of the four package-validation codes in `CheckPackageIssue.Codes` —
  `no_matching_path`, `insufficient_owners`, `missing_pr_label`, and `insufficient_service_owners`.
  The remaining codes (`invalid_directory_path`, `invalid_repo`, `invalid_cache_source`,
  `unexpected_error`) report input or operational failures rather than ownership defects and are
  covered separately.
- `check-package` resolution parity: for a sampled set of package directories, the owners it reports
  from the YAML match the owners `CodeownersParser` resolves from the rendered asset. This is what
  proves the two resolution paths agree.
- Audit rules against fixture caches, including the fail-fast behavior on stale, empty, and
  inconsistent caches.

### Migration Utility Tests

The `convert` and `verify` utilities are tested in their own project. The test matrix lives with the
tool, in [`tools/codeowners-migration/README.md`](../../../codeowners-migration/README.md#testing).


### Manual Testing

- Full azure-sdk-for-net migration verified with the Component 12 `verify` command.
- Confirm GitHub actually assigns the expected reviewers on a sample PR in each migrated repo.

### Cross-Language Validation

Run `verify` in all five language repos before enabling the drift gate, always with `--repo-root` so
that path resolution runs. It compares parsed entries and resolved owners per path, not file text, so
cosmetic reordering does not produce false failures.

---

## Metrics/Telemetry

| Metric | Description | Purpose |
|--------|-------------|---------|
| `codeowners.generate.duration` | Wall-clock time for `generate` | Confirm the local-file path is fast enough to run on every PR |
| `codeowners.generate.drift` | Count of gate 1 failures | Measure how often people try to hand-edit the generated file |
| `codeowners.validation.errors` | Count by rule ID | Find the rules that confuse authors most |
| `codeowners.union.blocks` | Count of label sets with more than one contributor | Understand how common shared label ownership is |
| `codeowners.fragments` | Fragment count per repo | Track migration progress |

Existing structured logging via `ILogger` is retained for diagnostics.

### Privacy Considerations

Ownership data is GitHub alias data that is already public in plain text in the repository. No
record of *why* an individual lost access is stored anywhere: invalid owners are dropped from the
YAML by a reviewed pull request, and no invalidity ledger is kept.

---

## Documentation Updates

- [ ] Rewrite the [owners agent skill](https://github.com/Azure/azure-sdk-for-net/blob/main/.github/skills/owners/SKILL.md)
      to describe editing `sdk/<service>/owners.yaml` instead of calling the removed add/remove MCP
      tools.
- [ ] Update [EngHub CODEOWNERS docs](https://aka.ms/azsdk/codeowners) with the YAML schema, the
      containment rule, and the union behavior.
- [ ] Add a `README.md` next to `.github/owners.config.yaml` in each migrated repo pointing at the
      schema reference.
- [ ] Update `tools/codeowners-utils/METADATA.md` to state that `# Sources:` is a recognized
      generated comment and is not a moniker.
- [ ] Supersede [`8-operations-codeowners-ownership-audit.spec.md`](./8-operations-codeowners-ownership-audit.spec.md):
      the legacy-linter mapping table remains accurate, but the "Azure DevOps work items" data source
      is replaced by the YAML sources described here, and `AUD-STR-001` / `AUD-STR-002` are retired in
      favor of schema validation.
- [ ] Update `tools/azsdk-cli/docs/mcp-tools.md` for the six removed and two added MCP tools, and add
      matching entries to `TestPrompts.json` so `ToolPromptCoverageTests` passes.
- [ ] Keep the CLI examples in this spec synchronized with option names in `CodeownersTool` and
      `GitHubLabelsTool`.
