# Spec: Codeowners Management from YAML Ownership Files

## Overview

`.github/CODEOWNERS` is a generated artifact. Ownership is authored as YAML in the
repository and rendered by the CLI.

Two kinds of file feed the render:

| File | Purpose |
| --- | --- |
| `.github/owners.config.yaml` | Repository-level configuration: the ordered sections of the generated file, static entries, and fragment discovery settings. One per repository. |
| `<service directory>/owners.yaml` | A fragment. Contributes path entries and label-owner blocks for one service. |
| `.github/CODEOWNERS` | Rendered output. Overwritten by `generate`; not edited by hand. |

The rendered file opens with a header naming its inputs:

```
# ------------------------------------------------------------------------------
# GENERATED FILE - DO NOT EDIT
# Ownership is defined in .github/owners.config.yaml and sdk/*/owners.yaml
# Regenerate with: azsdk config codeowners generate
# ------------------------------------------------------------------------------
```

## Schemas

The JSON schemas are the normative definition of both file formats, and each
carries per-field documentation:

- [`owners.config.schema.json`](../../schemas/owners.config.schema.json)
- [`owners.schema.json`](../../schemas/owners.schema.json)

Worked examples: [`owners.config.yaml`](./assets/codeowners/owners.config.yaml),
[`sdk-ai-owners.yaml`](./assets/codeowners/sdk-ai-owners.yaml),
[`sdk-openai-owners.yaml`](./assets/codeowners/sdk-openai-owners.yaml).

Both files declare `version: 1`, and a fragment's version must match the config's.
Referencing the schema from the top of each file enables editor validation:

```yaml
# yaml-language-server: $schema=https://raw.githubusercontent.com/Azure/azure-sdk-tools/main/tools/azsdk-cli/schemas/owners.schema.json
```

### `owners.config.yaml` shape

```yaml
version: 1
configs:
  allowed-owner-yaml-paths: ["sdk/*/owners.yaml"]
  default-section: Client Libraries
  output: .github/CODEOWNERS
  minimum-path-owners: 2
  minimum-label-owners: 2
sections:
  - name: Client Libraries
    defined-in-files: true
    sort: true
    paths: []
    label-owners: []
```

`configs` governs discovery and thresholds. `sections` is ordered, and that order
is the render order. CODEOWNERS matching is last-match-wins, so broader matches
belong earlier in the list.

### `owners.yaml` shape

```yaml
version: 1
paths:
  - path: Azure.AI.Projects/
    owners: [alias-one, alias-two]
    pr-labels: [AI Projects]
label-owners:
  - labels: [AI Projects]
    service-owners: [alias-one, alias-two]
    azure-sdk-owners: [alias-three]
```

A `path` is relative to the directory holding the fragment; `.` means that
directory itself. A fragment may only own its own subtree, so `..` and
repo-absolute paths are rejected.

## Discovery

`configs.allowed-owner-yaml-paths` lists globs naming every location a fragment
may occupy, and each glob ends in a literal file name. A fragment found anywhere
else is an error, so ownership cannot be placed where the scan will not look.

Declare a single fragment file name. A directory holding both `owners.yaml` and
`owners.yml` would render both over the same paths.

## Sections and routing

Each section renders under its own heading. A section with `defined-in-files: true`
accepts fragment entries; fragment entries that name no section land in
`configs.default-section`.

An individual entry may set `section:` to route itself elsewhere. Static entries in
the config may not — an entry there is already in a section, so it is moved rather
than overridden.

Static and fragment entries in the same section are ordered together with no
positional privilege. A section with `sort: true` is sorted; otherwise entries
render in authored order.

`exclude-from-check-package: true` hides a section from `check-package` path
resolution. It is intended for repository-wide guardrail entries that own every
package path but say nothing about whether a package declared owners of its own.
Those entries still render.

## Label owners

A `label-owners` block states who is notified for a set of issue labels. Blocks
declaring the same label set are unioned across every fragment, so several
services may contribute owners for one label.

Every label must exist in the common label set
(`tools/github/data/common-labels.csv`), and every `pr-labels` value on a path
entry must also be claimed by a `label-owners` block in the same fragment.

## Provenance

An entry rendered from fragments carries a comment naming the files it came from:

```
# Sources: sdk/ai/owners.yaml, sdk/openai/owners.yaml
# AzureSdkOwners: @alias-three
# ServiceLabel: %AI Projects
# ServiceOwners: @alias-one @alias-two
```

## Validation

Two levels, distinguished by when they run and what they can reach.

### Generate-time (`CFG-*`)

Structural checks that must hold before a file can be rendered. They need no
network access. Any violation is fatal and no output is written.

| Code | Trigger |
| --- | --- |
| `CFG-SCHEMA-001` | A config or fragment does not parse against its schema. |
| `CFG-LOC-001` | A fragment sits outside `configs.allowed-owner-yaml-paths`. |
| `CFG-SEC-001` | A section is declared twice, `default-section` names no section, an entry routes to a section that does not exist or does not accept fragment entries, or a static entry declares a section override. |
| `CFG-PATH-001` | A fragment path contains a `..` segment. |
| `CFG-PATH-003` | A path is missing, or is repo-absolute in a fragment. |
| `CFG-DUP-001` | A path is declared in both the config and a fragment. |
| `CFG-DUP-002` | A path is claimed by more than one fragment entry. |
| `CFG-DUP-003` | A path is declared twice within one section. |
| `CFG-DUP-004` | A label set is declared both statically and by a fragment. |
| `CFG-LBL-001` | A fragment path entry has no `pr-labels`. |

### Lint (`LNT-*`)

`lint-fragments` adds the checks that require the membership caches, so it
validates owners against GitHub rather than only checking shape.

| Code | Trigger |
| --- | --- |
| `LNT-SCHEMA-001` | The fragment could not be read or parsed. |
| `LNT-DUP-001` | Two path entries in one fragment resolve to the same path expression. |
| `LNT-OWN-001` | An alias is not a valid code owner. |
| `LNT-OWN-002` | A team owner is malformed, or does not descend from `azure-sdk-write`. |
| `LNT-OWN-003` | A path entry resolves to fewer than `minimum-path-owners` individuals. |
| `LNT-OWN-004` | A label-owners block resolves to fewer than `minimum-label-owners` service owners. |
| `LNT-LBL-001` | A label is not in the common label set. |
| `LNT-LBL-002` | A path entry declares no `pr-labels`. |
| `LNT-LBL-003` | A path entry uses a PR label that no `label-owners` block in the same fragment claims. |

Owner counting expands a team to the individuals it contains, so a single team
handle can satisfy a minimum.

## Owner validity and caches

An owner is a GitHub alias or an `Azure/<team>` handle. To be valid it must have
write access to the repository and public Azure organization membership; a team
must descend from `azure-sdk-write`.

Membership is read from published caches rather than the GitHub API. The caches
are refreshed by `update-cache`, and lint rejects any cache older than 72 hours so
a stale snapshot cannot silently pass an owner who has left.

## Commands

All are under `azsdk config codeowners`.

| Command | Purpose |
| --- | --- |
| `generate` | Render the CODEOWNERS file from the ownership YAML. `--omit-fallback-sections` drops sections marked `exclude-from-check-package`; `--output-file` overrides `configs.output`. |
| `lint-fragments` | Check fragments for invalid owners, insufficient owners, and unknown labels. Defaults to every fragment; `--fragment` limits it. Requires a current cache. |
| `check-package` | Check that one package directory has sufficient path owners, PR labels, and service owners. |
| `validate-owner` | Check whether a single alias or team can be a code owner. |
| `update-cache` | Run the pipeline that refreshes the membership caches. |

`--repo-root` locates the repository and defaults to the current directory.

## Continuous integration

`eng/common` supplies the shared template that runs `lint-fragments` on pull
requests that touch ownership YAML, and `check-package` for the packages in a
release build.
