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
`config codeowners add-*` / `remove-*` command family, `config codeowners view`,
`config codeowners audit`, `config codeowners export-section`, and `config github-label sync-ado` are
removed. Five commands remain — `generate`, `check-package`, `lint-fragments`, `update-cache`, and
`validate-owner` — and the three that downstream pipelines already call keep their name and shape.

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

  - name: SDK
    exclude-from-check-package: true   # a repo-wide guardrail, not per-package ownership
    paths:
      - path: /sdk/
        owners: [Azure/azure-sdk-write]
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
| `sections[].name` | string | yes | Unique. Fragments and entries route to a section by this name. |
| `sections[].defined-in-files` | bool | no | Default `false`. Marks the section as a target for fragment entries. |
| `sections[].sort` | bool | no | Default `false`. `true` orders the section's entries with `CodeownersEntrySorter.SortEntries`; `false` renders them in authored order. Independent of `defined-in-files`. |
| `sections[].exclude-from-check-package` | bool | no | Default `false`. Hides the section from `check-package` ownership resolution. Affects nothing about rendering. |
| `sections[].paths[]` | list | no | Static path entries. Ordered by `sections[].sort`. |
| `sections[].label-owners[]` | list | no | Static label-owner entries. Ordered by `sections[].sort`. |

#### The owner minimums never fail generation

`minimum-path-owners` and `minimum-label-owners` are evaluated by `lint` as `LNT-OWN-003` and
`LNT-OWN-004`, and by `check-package` at release time. `generate` does not enforce them and a fragment that falls below
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
`InsufficientServiceOwners` and trips `LNT-OWN-004`. Some labels legitimately have only an Azure SDK
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
`generate` rejects. Each entry has two non-equivalent resolutions — assign a label and keep
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

Fragments may only own their own subtree, and every expression they produce must be one the matcher
can actually evaluate.

1. **Literal `..` rejection.** If a fragment `path` value contains a `..` path segment anywhere, the
   entry is rejected with `CFG-PATH-001` before any resolution or normalization is attempted.
   `../some-other-service`, `Azure.AI.Inference/../../storage`, and `./..` are all rejected on sight.
   This check is textual and runs first so that no clever expression can reach the resolver. The
   entry never reaches the rendered file; `lint-fragments` turns the same condition into a failed
   build on the pull request that introduced it.
2. **Repo-absolute rejection.** A leading `/` in a fragment `path` is an error (`CFG-PATH-003`).
   Repo-absolute expressions belong in the owners config. This removes the ambiguity of whether
   `/sdk/ai` means "repo root" or "fragment directory".

Together these two rules are containment in full: a fragment path is joined to the fragment's own
directory, so once it can neither climb out with `..` nor start over at the root, the result is
always inside the fragment's subtree.

Additional path rules:

- A fragment path expression must be one `DirectoryUtils.IsValidCodeownersPathExpression` accepts —
  `CFG-PATH-002` otherwise. This rejects `?`, `[`, `]`, `!`, escaped `#`, a glob ending in a bare
  `*` that is not `/*`, and the redundant `/**` and `/**/` suffixes. Glob metacharacters `*` and
  `**` are otherwise allowed within the fragment subtree: `Azure.AI.*/` is valid in
  `sdk/ai/owners.yaml` and resolves to `/sdk/ai/Azure.AI.*/`.
- Owners config `path` values must be repo-absolute (leading `/`) — `CFG-PATH-004` otherwise.
- A glob-free fragment path that names a directory on disk must be authored with a trailing `/`.
  `CFG-PATH-005` otherwise. See [Normalization](#normalization).

##### Path validation applies to fragments only

Every `CFG-PATH-*` rule applies **only to paths contributed by `owners.yaml` fragments**. Path
expressions written directly in the owners config are accepted as authored.

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
keeps every *newly authored* expression matchable by the lint and resolution tooling while leaving
the migrated corpus untouched.

#### Normalization

Path normalization is deliberately minimal. The renderer **never infers whether an expression names
a file or a directory**. A fragment path is resolved against the fragment's directory, and a config
path is used as written. Nothing else is added or removed.

```text
sdk/ai/owners.yaml  path: .                      ->  /sdk/ai/
sdk/ai/owners.yaml  path: Azure.AI.Inference/    ->  /sdk/ai/Azure.AI.Inference/
sdk/ai/owners.yaml  path: Azure.AI.*/            ->  /sdk/ai/Azure.AI.*/
sdk/ai/owners.yaml  path: ci.yml                 ->  /sdk/ai/ci.yml
sdk/ai/owners.yaml  path: ../storage             ->  ERROR CFG-PATH-001
sdk/ai/owners.yaml  path: Azure.AI.*             ->  ERROR CFG-PATH-002
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
| Nothing in the working tree | Not an error here; see below |
| An expression containing `*` or `**` | Not checked; globs cannot be resolved by `stat` |

Resolution is performed against the checkout `generate` is already running in, using the same
`StringComparison.Ordinal` semantics as the rest of the path pipeline.

A path that resolves to nothing is deliberately *not* a `generate` error. A fragment can legitimately
name a directory that does not exist yet in the branch being rendered — a package about to land, or a
path deleted in a commit that has not updated ownership. Blocking rendering on it would make an
unrelated PR fail. Nothing else reports it either: an entry for a path that no longer exists is
inert, and the condition that does matter — a package with no usable owners — is what
`check-package` fails on at release time.

The alternative — deciding file-vs-directory from a list of recognized file extensions — was
rejected. It requires an allowlist that must be kept current, and any implementation that reached
for `Path.HasExtension` instead would classify `Azure.AI.Inference` as a file and render it without
its trailing slash, which is precisely the broadening bug this rule exists to prevent. Asking the
filesystem is unambiguous and needs no list.

`CodeownersEntrySorter.NormalizePath` is therefore **not** reused for rendering. It remains in use
only where it already is, on parsed entries.

#### Owner and label normalization

Owners are normalized by trimming, stripping a leading `@`, and de-duplicating. They render with an
`@` prefix.

Labels are normalized by trimming, stripping a leading `%`, and de-duplicating. They render with a
`%` prefix.

Both happen **once, as the YAML is loaded**, before any entry reaches the renderer, the validator,
`lint`, or `check-package`. Nothing downstream re-normalizes and nothing downstream sees an
un-normalized value, so `@alice` and `alice` are the same owner everywhere and `[AI Projects]` and
`[ai projects]` are the same label set everywhere — including in the union key, the duplicate
checks, and the `# Sources:` provenance. De-duplication is applied at the same moment, so an entry
that lists a label twice under two spellings carries it once.

This is the design's answer to a failure mode the previous implementation had: normalization scattered
across each consumer, each with its own idea of what a leading `@` meant, and two consumers reaching
different conclusions about whether two entries collide.

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
A miscased path simply matches nothing on GitHub, and `check-package` reports the package it was
meant to cover as unowned — which is the correct diagnosis. Folding the two together with a
case-insensitive comparer would hide the real bug behind a spurious duplicate error.

These are the only two comparers in the design, and each is pinned to a data type rather than to a
call site, so an implementation cannot drift between them.

### Component 4: Label-owner union

Label-owner entries **contributed by fragments** are grouped by **label set and target section**.
The label set is compared order-insensitively (labels are already normalized and case-folded when
the YAML loads). Sets that differ by even one label are distinct blocks, because a `ServiceLabel`
block's label set is what triage automation matches on.

Section is part of the key because two fragments claiming the same labels in different sections are
asking for the block to land in two different places, and no merge can satisfy both. See
[Contributors that disagree about the section](#contributors-that-disagree-about-the-section).

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
5. The target section is the group's own — every contributor to a group named it.

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

#### Contributors that disagree about the section

If `sdk/ai/owners.yaml` sends `[AI Projects]` to `Client Libraries` and `sdk/openai/owners.yaml`
sends the same label set to `Management Libraries`, two blocks render, one in each section, each
carrying only its own contributors.

That is almost certainly a mistake, but which fragment is wrong is a human judgement the renderer
cannot make. Picking a winner would silently discard one team's service owners; failing the render
would block an unrelated pull request over a disagreement between two other teams. Rendering both,
as written, leaves the evidence in the output where the owning teams can see it.

A fragment naming a section that does not exist, or one without `defined-in-files: true`, is a
different case: there is no output to render it into. The entry is **dropped** and `CFG-SEC-001` is
reported. Every other entry still renders, and `generate` exits non-zero, so the mistake cannot
merge.

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
| `CFG-DUP-002` | The same normalized path expression is produced by two fragment path entries | Error |
| `CFG-DUP-003` | The same normalized path expression is declared twice within one owners config section | Error |
| `CFG-DUP-004` | A label set declared in an owners config section is also declared by a fragment | Error |

`CFG-DUP-004` is what keeps label-owner union meaningful: union is the sanctioned way for **two or
more service teams** to co-own a label set. A repo-level static declaration competing with a
fragment declaration is an authoring mistake, not a merge.

`CFG-DUP-002` is written against fragment path *entries*, not fragment *files*, because two different
files cannot in fact collide: [path containment](#component-3-path-containment-rules) confines every
fragment to its own subtree, and the `allowed-owner-yaml-paths` globs do not nest. The reachable case
is one fragment declaring the same path twice.

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
correct rather than permissive: one of them matches nothing on GitHub, so the package it was meant
to cover reports as unowned, which names the actual defect.

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

Deterministic. The same inputs — the YAML plus the membership caches — always produce a
byte-identical file.

1. **Load.** Parse the owners config. Enumerate every file matching
   `configs.allowed-owner-yaml-paths`. Separately, scan the repo for any `owners.yaml` / `owners.yml`
   that does *not* match those globs and fail with `CFG-LOC-001` if one is found.
2. **Schema validate.** Reject unknown keys, missing required keys, non-canonical key spellings, and
   version mismatches.
3. **Normalize.** Apply the path, owner, and label normalization rules above. Fragment path
   containment and expression validity (`CFG-PATH-001` / `CFG-PATH-002` / `CFG-PATH-003`) are
   enforced here.
4. **Bind sections.** Resolve each entry's target section: entry `section` → file `section` →
   `configs.default-section` for fragments; the declaring section for static entries. A fragment
   entry targeting a missing section, or one without `defined-in-files: true`, fails with
   `CFG-SEC-001`.
5. **Union label owners.** As described in Component 4.
6. **Validate.** Run the `CFG-*` rules. An entry that cannot be bound — an unknown section, an
   invalid path — is **dropped and reported**; the remaining entries still render. Rendering
   continues so one bad entry does not hide the state of every other one, and so the report names
   every problem in the file rather than the first.
7. **Filter owners.** Drop the fragment owners the membership caches reject, then drop any entry
   left with nobody
   ([Component 8](#component-8-the-shared-build-pipeline-and-invalid-owner-handling)). Config
   entries are not filtered.
8. **Order.** Sections render in declaration order. What happens *within* a section is controlled by
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

The correct response is to leave the condition alone rather than change the sort. `generate` never
rewrites ownership to avoid an inversion. The migration tooling reports inversions where they are
actionable — as a resolution change between the old file and the new one (see
[Component 12](#component-12-migration-utilities)) — because that is the moment a team can decide
between relabelling the descendant and moving the section to `sort: false`.

9. **Emit.** Render the fixed generated-file banner, then each section. Each entry is formatted by
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

**The regeneration job.** Runs on `main` after merge. Renders, and opens or updates a PR when the
result differs from the committed file.

`generate` has one job — render the YAML and write the result — and it always does it. Whatever it
could not render is dropped and reported rather than raised
([Component 8](#component-8-the-shared-build-pipeline-and-invalid-owner-handling)), so it is not the
thing that fails a contributor's build. Whether the committed file happens to be in sync is not
`generate`'s question either: the regeneration job answers that with `git diff --exit-code`, and gate
2 does not care at all, because a contributor's PR is *expected* to leave the committed file stale.

Gate 2 is therefore a single check: **`azsdk config codeowners lint-fragments`**. It validates the
changed fragments against the publicly downloadable owner caches, checks that each entry resolves to
enough individuals, and checks that its labels are in the common label set
([Component 10](#component-10-lint-rules)). Fail on any violation, and fail if a cache is unusable
(see [Cache availability](#cache-availability)).

It runs only on PRs that touch `.github/owners.config.yaml` or an `owners.yaml`. A pull request that
changes no ownership file does not run it and cannot be blocked by it. When a fragment changes, only
that fragment is linted; when the config changes, every fragment is linted, because the config
carries the minimums and can invalidate a fragment nobody touched.
`eng/common/pipelines/templates/steps/lint-codeowners.yml` implements the trigger and the step; it is
a no-op in a repository that has no `.github/owners.config.yaml` yet.

The caches it reads are anonymously readable blobs, so the step needs no credential and works on
pull requests from forks.

#### The regeneration job

After a PR merges to `main`, the job checks out `main`, runs `generate`, and if `git diff
--exit-code` reports a change to `.github/CODEOWNERS`, opens a pull request titled
*Regenerate CODEOWNERS*.

- **Fixed head branch `codeowners/regenerate`.** If a PR from that branch is already open, the job
  force-updates the branch and lets the existing PR pick it up rather than opening a second one.
  Several ownership merges landing in quick succession therefore collapse into one regeneration PR
  carrying the union of their effects.
- **If `generate` fails it does not open a PR.** The only way it fails is an unusable membership
  cache, which means the render would drop owners that are actually fine. Opening a PR on that is
  worse than opening none, so the job alerts and leaves the committed file alone.
- **The regeneration PR is reviewed like any other.** It modifies `.github/CODEOWNERS`, which the
  repository-root section assigns to repository maintainers, so it lands in front of the people
  responsible for the file. It is exempt from gate 1 by virtue of its head branch, and it does not
  run gate 2 at all because it changes no YAML.

  Because the render depends on the caches, this PR is also where an ownership *removal* becomes
  visible: an owner the caches began rejecting shows up as a deletion in a reviewed diff, attributed
  by `generate`'s output to the fragment and line that declared them.

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
being edited. The most common class of unrelated failure — an owner who has since left — is also
reported by `check-package` at release time ([Component 8](#component-8-the-shared-build-pipeline-and-invalid-owner-handling)), so
it tends to be cleaned up by the team that owns the file rather than landing on an unrelated author.
And the condition is self-extinguishing per file — it can only fire once.

This applies to fragments and to `.github/owners.config.yaml` alike. It does not extend across
files: editing `sdk/ai/owners.yaml` does not lint `sdk/storage/owners.yaml`. The exception is a
change to `.github/owners.config.yaml`, which carries the owner minimums and can therefore invalidate
a fragment nobody touched; that change lints every fragment.

#### Why validity checking is scoped to ownership changes

Two independent reasons, and the second only became decisive once gate 2 was made to fail closed.

First, relevance: an author can act on a validity problem in ownership they are editing. They cannot
act on one in a service they have never touched, and asking them to would mean committing someone
else's access removal inside an unrelated change — the reviewed-removal flow this design avoids.

Second, blast radius. Gate 2 fails closed on an unusable cache, so its trigger decides what an outage
costs. Scoped to ownership files, a cache outage blocks ownership PRs — a small set, and exactly the
set that must not proceed on unverified owner data. Run on every PR, the same outage would halt all
development in every migrated repository. Fail-closed is only a proportionate policy because the
trigger is narrow; the two decisions have to be read together.

The same reasoning is why `generate` is not a pull request gate. It reads the caches too, so running
it on every PR would give a cache outage exactly the blast radius the narrow trigger exists to avoid.
`generate` runs on `main`, in the regeneration job, where a failed run delays a bot PR instead of
blocking contributors.

Repository-wide validity drift is not gate 2's job. `check-package` finds it at release time, in
front of the team that owns the package
([Component 8](#component-8-the-shared-build-pipeline-and-invalid-owner-handling)).

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

`generate`, `lint-fragments`, `check-package`, and `validate-owner` all read the owner-validity
caches, and **all four fail closed.** There is one rule and no per-operation exceptions.

A cache is unusable if it is unreachable, empty, older than six hours, or **does not parse**. Any of
those conditions produces a non-zero exit that names the cache and the specific failure. Neither
operation proceeds on a partial or assumed-empty cache, and neither downgrades a cache failure to a
warning.

All four share one implementation of the decision — `IOwnerValidator` — so "is this a valid owner"
has a single answer in the product rather than one per caller. Freshness is checked once per
execution and the result reused, so a command that asks about hundreds of owners pays for one round
trip.

##### Why the PR gate fails closed too

An earlier draft let the owner-validity check skip and pass on a cache problem, reasoning that a
shared blob outage should not block pull requests. That reasoning is wrong, and it is worth writing
down why.

A check that passes when it could not run reports the same result as a check that ran and found
nothing. The signal is indistinguishable from success, so a degraded cache produces green builds
indefinitely and nobody is prompted to fix it. Silent-skip converts an infrastructure outage into a
quiet, open-ended reduction in enforcement — which is precisely the failure mode this design exists
to eliminate, since the whole point of moving ownership into the repository was to stop trusting
artifacts that might be stale.

Failing closed makes the outage loud and short. Someone is blocked, the cache gets fixed, and
enforcement resumes at full strength. A cache refresh is a pipeline run, not a multi-day repair.

##### Why `generate` fails closed rather than rendering an empty file

"Empty cache" and "nobody is a valid owner" are indistinguishable to a renderer. A cold cache would
otherwise produce a syntactically valid CODEOWNERS with almost every individual owner silently
missing — a file that parses, passes drift detection, and routes nothing.

This is the one guard the design needs in exchange for filtering at render time. It is cheap: the
freshness and non-emptiness of both caches is checked once, before any owner is judged, and the run
stops there. A `generate` that cannot trust its caches writes nothing and alerts, which in the
regeneration job means the committed file simply stays as it is until the cache is refreshed.

##### The safety threshold is retired with `--fix`

`InvalidOwnerRule` and `TeamNotWriteRule` each carried `SafetyThreshold = 5`, and `--force` existed
to override it. Both are removed, because the thing they guarded is removed: no command deletes
owners from the YAML any more.

The threshold protected against one specific cache defect — **correlated truncation.** An alias
missing from *both* caches produces `hasWritePermission = false` and `hasAzureOrgEntry = false`, so
the disagreement checks do not fire (each requires exactly one of the two to be true) and the owner
reads as invalid. A partially written team blob paired with a similarly short org blob therefore
marks every missing person invalid without tripping the empty-cache or staleness guards.

That defect still exists. What changed is its consequence. Under `--fix` it deleted dozens of real
owners from source files and opened a pull request to commit the deletion, so it needed a circuit
breaker. Under `lint` and `check-package` it produces a wave of false violations on builds that a
human is already looking at — loud, self-evident, and fixed by refreshing the cache. A noisy report
does not need an override; a destructive edit did.

### Component 8: The shared build pipeline and invalid owner handling

Three of the five commands need the same thing: the repository's ownership YAML, loaded, filtered,
and rendered. That work lives in one place, `ICodeownersModelBuilder`, and every caller gets the same
answer from it.

```
load(.github/owners.config.yaml + sdk/*/owners.yaml)
  -> drop what the membership caches reject   (fragments only)
  -> optionally drop the guardrail sections   (check-package only)
  -> render
  -> CodeownersModel { Content, Entries, Settings, Dropped }
```

`Dropped` is the part that makes the rest legible: every entry and every owner that did not survive,
with the rule that rejected it and the file and line it was authored on.

#### Fragments are filtered; the config is not

An owner named in `sdk/<service>/owners.yaml` is checked against the org- and team-membership caches
and removed if the caches reject it. An owner named in `.github/owners.config.yaml` is rendered
exactly as written and never checked.

The two files decay differently. Fragments are authored by service teams, number in the hundreds, and
go stale quietly as people change teams. The config is maintained by repository maintainers, is
reviewed as a unit, and carries entries carried over from a CODEOWNERS file GitHub has been enforcing
all along. Filtering it would mean this tool silently editing entries its maintainers deliberately
wrote, on the strength of a cache — so it does not.

#### An emptied entry is dropped, not rendered ownerless

When every owner on a path entry is rejected, the entry is removed rather than rendered with an empty
owner list. In CODEOWNERS an ownerless path means *nobody* owns this, and because matching is
last-match-wins it also **stops** the path from falling through to the broader entry that would
otherwise catch it. Rendering the empty entry would therefore be strictly worse than rendering
nothing: a decayed service entry would disown its own directory instead of deferring to the
repository backstop.

The same applies to a label-owner block whose owners are all rejected (`GEN-DROP-002`).

This is a real transfer of ownership driven by remote cache state rather than by a commit, and it is
accepted deliberately. What makes it acceptable is that the backstop is the destination, the transfer
is reported in `generate`'s output and in the regeneration pull request's diff, and `check-package`
refuses to count the backstop — so a package whose owners have all decayed fails its release gate
rather than passing on the strength of `/sdk/ @Azure/azure-sdk-write`.

#### Generate always produces a file

Structural problems — a path containing `..`, a path outside the fragment's own subtree, a duplicate
definition — are treated the same way as a rejected owner: the offending entry is dropped, the reason
is reported, and the rest of the file renders. `generate` exits 0.

A repository of this size always contains some decayed ownership. A `generate` that refused to render
until every fragment was clean would leave `.github/CODEOWNERS` frozen at whatever it happened to say
on the day the first fragment went stale, and every unrelated ownership change would queue up behind
someone else's problem. Reporting and continuing keeps the rendered file current; `lint-fragments`
is what turns those same conditions into a red build, on the pull request that introduced them.

#### Cache availability

Because rendering now depends on the caches, `generate` **fails closed** when they are unavailable,
stale, or empty rather than rendering as though every owner were invalid. An empty cache would reject
every owner in the repository and produce a CODEOWNERS file containing almost nothing. The same rule
covers the common label set for `lint-fragments`. Freshness is checked once per execution and the
result is reused, so a command that asks several times pays for one round trip.

#### What this costs

Rendering is no longer a pure function of the checkout: the same commit can render differently as
membership changes. Drift detection therefore compares the checked-in file against a **fresh**
render rather than against a replay of an older one, and the regeneration job is the only writer of
`.github/CODEOWNERS` ([Who writes the rendered file](#who-writes-the-rendered-file)). A run against
an unavailable cache is an error, not a diff.

### Component 9: Command surface

Five commands.

| Command | What it does | Exit code |
|---------|--------------|-----------|
| `generate` | Builds the model and writes `.github/CODEOWNERS`. Reports everything it dropped. | Always 0 unless the caches are unavailable |
| `check-package` | Builds the model without the guardrail sections, resolves one package directory against it, and counts owners. | Non-zero when the package is under-owned |
| `lint-fragments` | Validates one fragment, or every fragment, in isolation. | Non-zero on any violation |
| `update-cache` | Starts the membership-cache refresh pipeline. | Non-zero if the pipeline cannot be queued |
| `validate-owner` | Answers whether one alias or team can own code, and what to change if not. | Non-zero when the owner is invalid |

Commands deserialize their inputs and serialize their results. The rules live in helpers:
`ICodeownersModelBuilder`, `ICheckPackageHelper`, `ICodeownersLintHelper`, and `IOwnerValidator`.

#### Changes to the commands that already existed

| Command | Change |
|---------|--------|
| `generate` | Reads YAML instead of Azure DevOps. `--package-types`, `--section`, and `--invalid-owner-lookback-days` are removed; `--omit-fallback-sections` and `--output-file` are added. |
| `check-package` | Resolves ownership by building the model from the checkout instead of downloading a rendered CODEOWNERS artifact. `--codeowners-cache` and the blob fallback are removed. |
| `update-cache` | Unchanged trigger. The pipeline it starts now refreshes only the org- and team-membership caches. |
| `config github-label check` / `create` | Unchanged. |

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
| `azsdk config codeowners audit` | (none) |
| `azsdk config codeowners export-section` | (none) |
| `azsdk config github-label sync-ado` | (none) |

Ownership mutation is now a file edit in a pull request. An agent edits `sdk/<service>/owners.yaml`
directly, then runs `lint-fragments` to confirm the result is valid. It does not stage
`.github/CODEOWNERS`; the regeneration job owns that file.

#### `audit` is deleted, not renamed

`audit` fixed things: it deleted owners from source files, which forced a safety threshold, a
`--force` override, a YAML editor, and a job to commit the result and open a pull request. Removing
the write path removes all of it.

What remains of its value is split between two places that already had to exist. `generate` drops
invalid owners from the rendered file, so the decay never reaches GitHub. `lint-fragments` reports
those same owners against the file that declares them, on the pull request that introduces them,
where a human is present and the change is one line. Nothing else in `audit`'s rule set survived
contact with the YAML model ([Component 10](#component-10-lint-rules)).

#### `export-section` is deleted

`export-section` existed to slice a section out of a rendered CODEOWNERS file. Its two callers were
the ownership-extraction pipeline — which is deleted along with the rendered-CODEOWNERS cache — and
`Test-CodeownersSections.ps1`, which diffs a section across two revisions to detect drift.

That second caller keeps its job during migration: until a repository is generating its CODEOWNERS,
gate 1 does not apply to it and the section comparison is the only thing protecting the file.
Rather than hold the command alive for it, the script slices sections itself. A section is a
three-line `###` / `# <Name>` / `###` fence running to the next `###`, which is a dozen lines of
PowerShell and costs the check its dependency on the CLI — the template no longer installs one.
Once every repository has migrated, gate 1 supersedes the script and both retire together.

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
- `azsdk_engsys_codeowner_validate_owner`
- `azsdk_check_service_label`
- `azsdk_create_service_label`

Net change: eleven MCP tools become five, and six write tools become zero.

`validate-owner` is the one addition. It exists because "why was my owner rejected?" is the question
an agent actually gets asked, and answering it from `lint-fragments` output means running a whole
file's rules to learn one thing. The two reasons an alias fails — no write access, or private Azure
org membership — are fixed by different people in different places, so the answer has to name which
one applies.

`generate` and `lint-fragments` stay CLI-only. Both are pipeline steps whose callers are `eng/`
scripts, not agents: the regeneration job runs `generate` on `main`, and the pull request build runs
`lint-fragments`. Exposing `generate` over MCP would add an agent-reachable path that writes
`.github/CODEOWNERS` — the one file this design says no agent and no human should write.

### Component 10: Lint rules

`lint-fragments` answers one question — **is this fragment valid on its own?** — and reports the
answer. It never edits the repository and never renders `.github/CODEOWNERS`.

| Rule ID | Description | Data dependency |
|---------|-------------|-----------------|
| `LNT-SCHEMA-001` | The file does not parse, or violates the fragment schema | — |
| `LNT-OWN-001` | Individual owner is not in `azure-sdk-write`, or their Azure org membership is not public | `azure-sdk-write-teams-blob`, `user-org-visibility-blob` |
| `LNT-OWN-002` | Team alias is malformed, or the team does not descend from `azure-sdk-write` | `azure-sdk-write-teams-blob` |
| `LNT-OWN-003` | Path entry resolves to fewer than `configs.minimum-path-owners` individuals | `azure-sdk-write-teams-blob` |
| `LNT-OWN-004` | Label-owner block resolves to fewer than `configs.minimum-label-owners` individual service owners | `azure-sdk-write-teams-blob` |
| `LNT-LBL-001` | Label is not in the common label set | `common-labels.csv` |
| `LNT-LBL-002` | Path entry declares no `pr-labels` | — |
| `LNT-LBL-003` | A `pr-label` is not claimed by any `label-owners` block in the same file | — |
| `CFG-PATH-*` | Path escapes the fragment's directory, contains `..`, or is otherwise unresolvable | Repository tree |

#### Each fragment is judged alone

Linting `sdk/ai/owners.yaml` loads `sdk/ai/owners.yaml`. It does not load the other fragments, and it
does not build the rendered model.

That is what makes the result actionable. A fragment that only passes because a *different* service's
file supplies the missing owners is not a fragment its team can maintain, and a violation whose cause
lives in a file the author did not touch is a violation the author cannot fix. Judging in isolation
keeps every reported problem inside the diff under review.

`LNT-LBL-003` is the rule that enforces this directly: a path may declare a PR label only if the same
file also says who owns that label. Without it a fragment could route pull requests to a label whose
service owners are declared elsewhere — or nowhere.

The one thing lint reads outside the fragment is `.github/owners.config.yaml`, and only for the
`configs` block that carries the minimums. When there is no config, the model defaults apply.

#### Minimums count individuals, and teams expand

`LNT-OWN-003` and `LNT-OWN-004` count the distinct individuals the declared owners resolve to. A team
contributes its cached membership; an alias contributes itself; someone named both directly and
through a team counts once.

Counting teams as zero — the previous behavior — told a service that had correctly delegated ownership
to `Azure/<their-team>` that it had no owners. Counting a team as one would let a two-owner minimum be
satisfied by a team of one. Expanding is the only count that matches what GitHub will actually do when
it requests reviews.

A team with no cached membership expands to nobody, which is the same answer `LNT-OWN-002` gives for
it, so an unresolvable team is never quietly credited with members.

#### Lint reads fragments, not the owners config

Every rule above evaluates `owners.yaml` fragments. `.github/owners.config.yaml` is not linted.

The config is maintained by repository maintainers, is small, and changes rarely; the fragments are
where the churn is and where an owner goes stale unnoticed. The label rule in particular would
misfire on it: the config carries the management-library and end-to-end-sample sections, whose labels
are repo-specific by design and are not in the common set. Extending the rules to cover the config
later is a change of scope, not of structure — the loader already produces the same entry shape for
both sources.

#### Why labels come from the common label set

`LNT-LBL-001` reads
[`tools/github/data/common-labels.csv`](https://github.com/Azure/azure-sdk-tools/blob/main/tools/github/data/common-labels.csv)
rather than the labels that happen to exist on one repository.

Fragments describe services, and a service spans language repos. A label that exists only in
`azure-sdk-for-net` routes issues nowhere in the other five. Holding fragments to the sanctioned
common set is what keeps a service's triage identical across repositories, and it makes lint's answer
independent of which repo it is run in.

If the CSV is unreachable or parses to zero labels, lint fails rather than passing. Treating an
unreachable list as "no labels are known" would report every label in the repository as invalid;
treating it as "all labels are known" would silently stop checking. Neither is a result worth
returning, so it raises.

#### Lint also reports who owns what

Alongside the violations, each fragment's result carries the directories it governs and the owners
each one resolves to, walking one level below the fragment's own directory. The reviewer of an
ownership change wants to see the effect of the change, not only that it was legal, and one level is
where packages live in every repository this targets. Walking the whole tree would bury that in
output.

#### Rules that were not carried over

| Retired rule | Why |
|---|---|
| `AUD-PATH-001` (path matches nothing on disk) | A path can be legitimately absent between a deletion and the next ownership edit. The condition it detected is now a `check-package` failure at release time, where it blocks something. |
| `AUD-ORD-001` (ownership inversion) | Report-only, expected to fire on existing data, and not resolvable by the tool. It measured the rendered file rather than the YAML, so it belongs with the migration tooling that produced the ordering. |
| `AUD-LBL-002` (`Service Attention` misuse) | Subsumed by `LNT-LBL-001` plus schema validation. |
| `AUD-STR-001` / `AUD-STR-002` (empty label owner blocks) | Schema violations that fail at load, reported as `LNT-SCHEMA-001`. |

### Component 11: `check-package` source resolution

`check-package` keeps its four validation rules and its output contract. What changes is where it
reads ownership from: **the CODEOWNERS file the checkout's ownership YAML renders to**, computed in
memory rather than downloaded.

Given `--directory-path sdk/ai/Azure.AI.Inference`:

1. **Build the model** with `omitFallbackSections: true`
   ([Component 8](#component-8-the-shared-build-pipeline-and-invalid-owner-handling)). Invalid owners
   are already gone; the guardrail sections are already excluded. Nothing is written to disk.
2. **Resolve the directory** against the result, last-match-wins, the same way GitHub resolves the
   whole file. If nothing matches, report `no_matching_path`.
3. **Read owners, PR labels, and service owners** off the matched entry and off the label-owner block
   whose `labels` are fully contained in the entry's `pr-labels` — the containment rule
   `CheckPackageHelper` already implements.
4. **Expand teams to individuals** and count the distinct people, then compare against
   `minimum-path-owners` and `minimum-label-owners`.
5. **Report the remaining `CheckPackageIssue.Codes` as today.**

The example assets resolve `Azure.AI.Inference` to owners `test-user-07, test-user-09,
test-user-23`, PR label `AI Model Inference`, and service owners `test-user-07, test-user-09,
test-user-23`.

#### Why resolve through the shared builder

Because the rendered file is what GitHub enforces, and the release gate should ask its question of
the thing that decides the answer. Reading a single fragment directly is simpler, but it answers a
different question — "what did this team write down" rather than "who owns this package" — and the
two diverge exactly where it matters: when a static config entry, a parent fragment, or another
section also claims the path. The builder already resolves that competition; reproducing any part of
it in the release gate would create a second ordering implementation to keep in sync forever.

The cost is that `check-package` loads the whole model instead of one file. That is the same work
`generate` does, on a checkout that is already on disk.

#### Owner validity is inherited, not re-checked

`check-package` does not validate owners. The builder has already dropped everyone the caches reject,
so what it counts is what GitHub would actually route to. Checking again would be a second
implementation of the same rule, reachable only from this command.

The consequence is that a package can report fewer owners than its `owners.yaml` lists. So the
response carries the owners the builder dropped from the governing fragment whenever the check fails,
with the reason and the authoring line — otherwise a release engineer reads "1 unique owner" against a
file that plainly names three and has nowhere to go with that.

Ownership decays silently: people change teams and leave the company without touching any YAML, and
nothing re-runs `lint-fragments` on a file nobody edited. `check-package` is the one gate every
release passes through, which is why the decay has to be visible here even though the rule that
detects it lives elsewhere.

#### Why guardrail sections are excluded

Rendering the whole file makes every repo-wide catch-all a potential match. `/sdk/` owned by
`@Azure/azure-sdk-write` is a deliberate backstop so that no path in the repository is unowned; it is
not a statement that any particular package has owners. Without the exclusion, every package in the
repository would resolve to the backstop and pass, and `check-package` would report nothing ever
again.

`exclude-from-check-package: true` marks those sections. Rendering ignores the flag entirely — the
entries still appear in `.github/CODEOWNERS` and GitHub still honours them — but ownership
resolution steps over them, so a package that has no ownership of its own reports
`no_matching_path` and names the fragment its team should create.

The sections carrying the flag in the reference config are the repository-root, `/sdk/`, end-to-end
sample, management-fallback, provisioning, EngSys, code-generation, automation, and
repository-configuration guardrails. The `Core Libraries`, `Client Libraries`, and
`Management Libraries` sections are **not** excluded: they hold real per-package ownership.

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

`export-section` goes with it. The pipeline was one of its two callers; the other is
`eng/common/scripts/Test-CodeownersSections.ps1`, which exported a section from two revisions of a
CODEOWNERS file to diff them. Section order is now declared in `.github/owners.config.yaml` and the
whole file is generated, so that comparison is a diff of the generated file.

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
$EDITOR sdk/ai/owners.yaml           # add the alias under the right path entry
azsdk config codeowners generate     # confirms the YAML renders; discard the rendered file
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
  - Context: the orphan list is (paths that exist) minus (paths declared), which is expensive to
    compute and noisy.
  - Options: (a) keep it as a YAML comment in the owners config, maintained by hand; (b) emit it
    from a separate report command; (c) do not track it at all.

- [ ] **Migration cutover per repo**: Should the two systems run in parallel for a period, with the
      YAML renderer writing only into a subset of sections?
  - Context: `generate` already targets specific sections today. A partial cutover is possible but
    means gate 2 cannot cover the whole file until the last section migrates.
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
      `.github/CODEOWNERS`, and the regeneration job opens a PR carrying the rendered result after
      it merges.
- [ ] Two ownership PRs merging in quick succession produce one regeneration PR, not two.
- [ ] A service team can add or remove an owner by editing only `sdk/<service>/owners.yaml`, and the
      resulting PR is routed to the existing owners for review.
- [ ] A fragment that declares `../some-other-service` is reported as `CFG-PATH-001` and does not
      appear in the rendered file, and `lint-fragments` fails the pull request that introduced it.
- [ ] A fragment path entry with no `pr-labels` fails lint with `LNT-LBL-002`, while a static path
      entry in the owners config with no `pr-labels` renders unchanged.
- [ ] A fragment `pr-label` with no `label-owners` block in the same file fails with `LNT-LBL-003`.
- [ ] A PR that edits one entry in `sdk/ai/owners.yaml` fails on a validation error in a different,
      unmodified entry in that same file.
- [ ] Two fragments declaring the same label set render one `# ServiceLabel:` block with unioned
      owners and a `# Sources:` comment naming both files.
- [ ] The rendered file contains no comment other than the fixed banner, the monikers, and
      `# Sources:` lines; `CodeownersParser` re-parses it with zero block errors.
- [ ] `lint-fragments` reports `LNT-OWN-003` / `LNT-OWN-004` for path entries and label-owner blocks
      that fall below `configs.minimum-path-owners` / `configs.minimum-label-owners`, counting a team
      as the individuals it expands to.
- [ ] `lint-fragments` reports `LNT-OWN-001` for an owner missing from the caches and exits non-zero.
- [ ] `lint-fragments` reports `LNT-LBL-001` for a fragment label absent from `common-labels.csv`, and
      reports nothing for a label used only by `.github/owners.config.yaml`.
- [ ] `lint-fragments --fragment sdk/ai/owners.yaml` reads no other fragment.
- [ ] `generate` removes a cached-invalid owner from the rendered file, reports it, and exits 0; a
      path left with no owners is omitted rather than rendered ownerless.
- [ ] `generate` renders a config-declared owner unchanged even when the caches reject it.
- [ ] `generate` exits non-zero when the membership caches are unavailable.
- [ ] `check-package` counts the individuals a team expands to, and lists the dropped owners of the
      governing fragment when it fails.
- [ ] `validate-owner` exits non-zero for an invalid alias and names which requirement failed.
- [ ] An owners config section that duplicates a fragment path or label set is reported as
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
3. Run `azsdk config codeowners validate-owner --owner alice` and report the result. If `alice` is
   invalid, name which half failed — write access or public Azure org membership — since the two are
   fixed in different places.
4. Run `azsdk config codeowners lint-fragments --fragment sdk/ai/owners.yaml` to confirm the file is
   valid as a whole.
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
4. Run `azsdk config codeowners lint-fragments --fragment sdk/contoso/owners.yaml`.
5. Report the block that *will* land in the `Client Libraries` section once the regeneration job
   runs, and remind the user not to commit `.github/CODEOWNERS`.

### Diagnose a duplicate-definition failure

**Prompt:**

```text
generate is reporting CFG-DUP-001 for /sdk/ai/Azure.AI.Inference/ and the entry is missing
from CODEOWNERS. What do I do?
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
- `--output-file <path>`: Override `configs.output`.
- `--omit-fallback-sections`: Skip sections marked `exclude-from-check-package`. Used by
  `check-package`; rarely useful on its own.

Exits **0** after writing the rendered file. Entries it could not render are dropped and reported,
not raised. It exits non-zero only when the membership caches are unavailable, since rendering
against an empty cache would drop nearly every owner in the repository.

**Expected Output:**

```text
Wrote .github/CODEOWNERS.

Excluded 3 item(s) from the rendered file:
  [LNT-OWN-001] sdk/ai/owners.yaml:14: 'test-user-31' — 'test-user-31' is not in
    Azure/azure-sdk-write and their Azure org membership is not public. Both are required.
  [CFG-PATH-001] sdk/ai/owners.yaml:22: '../storage' — path contains a '..' segment.
    Fragments may only declare paths at or below their own directory (sdk/ai/).
  [GEN-DROP-001] sdk/openai/owners.yaml:9: 'Azure.AI.OpenAI/' — Every owner on this path was
    rejected by the membership cache, so the path is not rendered and falls through to the
    next broader match.
```

**Error Case (exit 1):**

```text
✗ Cached membership for 'Azure/azure-sdk-write' or Azure org visibility came back empty.
  Refusing to validate owners against an empty cache; run
  'azsdk config codeowners update-cache' and retry.
```

### Check the YAML without keeping the result

There is no separate drift-check mode. `generate` renders and writes; whether the committed file was
already in sync is a question for `git`, not the CLI.

**Command:**

```bash
azsdk config codeowners generate && git diff --stat .github/CODEOWNERS
```

A contributor validating a YAML change runs `generate` and then discards the rendered file — a stale
`.github/CODEOWNERS` is the *expected* state of their branch, and the regeneration job produces the
real one after the change merges. The same two commands in the regeneration job, with `git diff
--exit-code`, are what decide whether there is a pull request to open.

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

### Lint fragments

```bash
azsdk config codeowners lint-fragments --fragment sdk/ai/owners.yaml
azsdk config codeowners lint-fragments                                  # every fragment
```

**Options:**

- `--fragment <relative-path>`: Repeatable. Defaults to every fragment in the repository.
- `--repo-root <path>`: Repository root. Defaults to the enclosing git checkout.

Each fragment is judged on its own. `lint-fragments` reports and exits non-zero on any violation. It
does not edit the repository, so it has no `--fix` and no `--force`. It takes no `--repo`: owners come
from repository-independent caches and labels from the common label set.

**Error Cases (exit 1):**

```text
sdk/ai/owners.yaml
  ✗ LNT-LBL-002: line 23 - path entry 'Azure.AI.Projects.Agents/' declares no pr-labels.
  ✗ LNT-LBL-003: line 14 - pr-label 'AI Agents' is not claimed by any label-owners block
      in this file.
  ✗ LNT-OWN-003: line 9 - path 'Azure.AI.Inference/' resolves to 1 owner; 2 are required.
```

### Validate one owner

```bash
azsdk config codeowners validate-owner --owner test-user-07
azsdk config codeowners validate-owner --owner Azure/azure-sdk-eng
```

**Options:**

- `--owner <alias-or-team>`: Required. A GitHub alias or an `Azure/<team>` handle.

Exits 0 when the owner can own code, non-zero when it cannot. An invalid alias fails for one of two
reasons that are fixed by different people, so the output names which one applies.

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

Output shape, issue codes, and `--output json` are unchanged from the previous design, with one
addition: when the check fails, the response also lists the owners `generate` dropped from the
governing fragment, so an owner count that disagrees with the file on disk is explicable.

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

- Milestone: gate 1 rejects hand edits to `.github/CODEOWNERS`; gate 2 lints every touched fragment;
  the regeneration job opens PRs from `main`; `check-package` resolves from the YAML.
- Pipeline changes in `eng/pipelines/pipeline-owners-extraction.yml`:
  - delete the `Export Client Libraries section` and `Upload CODEOWNERS cache` tasks. Nothing reads
    `cache/azure/<repo>/CODEOWNERS.cache` once `check-package` resolves from the YAML, so the
    producer retires with its only consumer rather than being gated per repo.
  - strip the removed `--package-types` and `--section` options from the `generate` invocation that
    fed them. The **repo-level gating that matters is `check-package` itself**: it cannot resolve a
    repository that has not completed Phase 2, so the release checks for the other four repos have
    to keep running the pre-migration CLI until they have.
  - keep `BuildTeamCache`. It produces the org- and team-membership caches, which remain the only
    things the storage account serves.
  - the `GenerateCodeowners` stage is **repurposed, not deleted**. It already contains the only
    clone and `create-pull-request.yml` invocation in the file. Strip the removed `--package-types`
    and `--section` options from its `generate` call, point it at the YAML, and reuse its
    clone/create-PR machinery for the regeneration job rather than building new infrastructure.
- **The scheduled audit step is removed.** `pipeline-owners-extraction.yml` ran
  `azsdk config codeowners audit --fix` on a schedule with `workingDirectory:
  tools/azsdk-cli/Azure.Sdk.Tools.Cli`, against no checkout of any target repository. There is no
  replacement: `generate` drops decayed owners on every render, `lint-fragments` runs on the pull
  request that introduces a bad owner, and `check-package` catches decay at release time. The
  `AuditCodeowners` parameter goes with it.
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
    CODEOWNERS artifact from storage. `export-section` and its options go with it.
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

- **Replace the audit rule set with `lint`.** The rules are typed against the work-item model and
  none survives unchanged. The registered rule classes (`InvalidOwnerRule`, `TeamNotWriteRule`,
  `MalformedTeamRule`, `LabelNotInRepoLabelsRule`, `ServiceAttentionMisuseRule`), the rule-engine
  registration in `Services/ServiceRegistrations.cs`, and `AuditContext` / `AuditViolation` are
  deleted rather than ported.
  - The rule engine itself goes with them. Five rules over one data source do not need dispatch,
    priorities, or a fix protocol; `CodeownersLintHelper` calls them directly, which is both shorter
    and readable end to end.
  - `AuditViolation` was keyed on `int? WorkItemId` and serialized `work_item_id`. `LintViolation`
    carries source file and line instead. This is a JSON contract change for any consumer of audit
    output.
  - `LabelNotInRepoLabelsRule` derived which repos use a label from `LabelOwner.Repository` and
    `Package.Language` via `BuildLabelToReposMap`. That derivation has no analogue here:
    `LNT-LBL-001` reads the common label set and does not know which repository it is running in.
- **Plumb `--repo-root`.** The `audit` command took only `--fix`, `--force`, and `--repo`; `lint`
  takes `--repo-root` and none of the other three.
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
| `IGitHubService` | Internal service | `GitHubLabelsTool` | GitHub identity and label operations. |
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
- **Whitespace**: one blank line after the file banner, one before and after each section banner,
  one between entries, no trailing whitespace, exactly one terminating newline.
- **Sparse rendering**: YAML comments in the owners config and in fragments never appear in the
  output; the banner is byte-identical across repos; the only comments emitted are the banner,
  `# Sources:`, and monikers.
- **Required PR labels**: a fragment path entry with `pr-labels` absent, `null`, or `[]` fails lint
  with `LNT-LBL-002`; the same shapes on a static owners config path entry render without a
  `# PRLabel:` moniker and produce no diagnostic.
- **Exit codes**: `generate` returns 0 after writing the rendered file even when entries were
  dropped, and non-zero only when the caches are unavailable.
- **Dropping**: an invalid fragment owner is removed and reported; a path left with no owners is
  omitted rather than rendered ownerless (`GEN-DROP-001`); an emptied label-owner block is omitted
  (`GEN-DROP-002`); a `..` path is dropped and reported rather than raised; a config-declared owner
  the caches reject renders unchanged and is not reported.
- **Whole-file validation**: a fragment containing one valid and one invalid entry reports the
  invalid one regardless of which entry a simulated diff touched; multiple independent violations in
  one file are all reported in a single run rather than stopping at the first.
- **Fragment isolation**: linting one fragment reads no other fragment; a fragment whose labels are
  owned only by a *different* fragment fails `LNT-LBL-003`.
- **Minimums**: `LNT-OWN-003` and `LNT-OWN-004` fire below threshold and stay silent at or above it;
  a team counts as the individuals it expands to; someone named both directly and through a team
  counts once; a team with no cached membership counts as nobody.
- **Lint**: an owner absent from either cache is reported with a message naming which half failed;
  an empty cache raises rather than reporting every owner invalid; a label used only by
  `.github/owners.config.yaml` is not reported; label comparison is case-insensitive; an
  unreachable common label list raises rather than passing.
- **Schema**: the checked-in JSON Schemas accept the reference assets and reject a `..` path, a
  missing `pr-labels`, a misspelled key, a label-owner block with no owners, and a duplicate owner.
- **Determinism**: rendering the same inputs twice is byte-identical; rendering with fragments
  enumerated in reverse order is byte-identical.
- **Round trip**: the rendered asset re-parses through `CodeownersParser` with zero block errors, and
  `# Sources:` comments neither start nor terminate a block.

### Integration Tests

- Render the spec assets (`owners.config.yaml` + the two fragments) and assert byte equality with
  `assets/codeowners/CODEOWNERS.rendered`. This is the executable contract for the whole design.
  The asset is generated mechanically by the renderer, never hand-edited; it currently parses to 52
  entries (38 with paths, 14 label-only) across 12 discoverable sections with zero block errors.
- `generate` returns 0 and writes the file on a clean tree, and also on a tree carrying a `CFG-*`
  error — reporting the dropped entry rather than failing. Gate 1 rejects a hand edit to
  `.github/CODEOWNERS` on the changed-file list alone, including one that renders identically.
- `check-package` against the **YAML assets** for a package that passes, and
  one that fails each of the four package-validation codes in `CheckPackageIssue.Codes` —
  `no_matching_path`, `insufficient_owners`, `missing_pr_label`, and `insufficient_service_owners`.
  The remaining codes (`invalid_directory_path`, `invalid_repo`, `invalid_cache_source`,
  `unexpected_error`) report input or operational failures rather than ownership defects and are
  covered separately.
- `check-package` resolution parity: for a sampled set of package directories, the owners it reports
  from the YAML match the owners `CodeownersParser` resolves from the rendered asset. This is what
  proves the two resolution paths agree.
- Lint rules against fixture caches, including the fail-fast behavior on stale, empty, and
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
      the "Azure DevOps work items" data source is replaced by the YAML sources described here, the
      `audit` command and its rule engine are replaced by `lint`
      ([Component 10](#component-10-lint-rules)), and the rules that were not carried over are listed
      there.
- [ ] Update `tools/azsdk-cli/docs/mcp-tools.md` for the removed MCP tools, and remove the matching
      entries from `TestPrompts.json`.
- [ ] Keep the CLI examples in this spec synchronized with option names in `CodeownersTool` and
      `GitHubLabelsTool`.
