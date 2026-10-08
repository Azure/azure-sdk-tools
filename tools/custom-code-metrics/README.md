# Custom code metrics

Shared TypeSpec contracts, a static dashboard, measurement and immutable
publication commands for Azure SDK custom-source metrics. Language repositories
own their schedules, trusted checkouts and publication jobs; tools owns the
website, contracts, common commands, hosting infrastructure and validation CI.

**Only the .NET adapter and initial .NET snapshot format 1.0 are implemented today.** Moving
ownership here does not make the existing C# counting rules or repository-specific
wire contract valid for arbitrary languages.

| Repository / language | Adapter status |
| --- | --- |
| Azure/azure-sdk-for-net / .NET | Implemented; language-aware collector in the .NET repository |
| Azure/azure-sdk-for-java / Java | **NOT IMPLEMENTED** |
| Azure/azure-sdk-for-js / JavaScript/TypeScript | **NOT IMPLEMENTED** |
| Azure/azure-sdk-for-python / Python | **NOT IMPLEMENTED** |
| Azure/azure-sdk-for-go / Go | **NOT IMPLEMENTED** |
| Azure/azure-sdk-for-rust / Rust | **NOT IMPLEMENTED** |
| Azure/azure-sdk-for-cpp / C++ | **NOT IMPLEMENTED** |

Those adapters are the collection roadmap, not enabled jobs. Each requires
language-specific compiled-source membership, generated/custom evidence,
categories and contract compatibility decisions. Do not apply the C# heuristics
to other languages, publish empty/zero-valued success observations for missing
adapters, or compare incompatible measurements as one portfolio.

The accessible **Repository** selector defaults to .NET and lists those seven
repositories. Only .NET has validated observations today. Other selections show
the named repository's uncollected/collector-not-implemented state, hide all
measurement panels and do not request nonexistent feeds. Switching back to .NET
restores its baseline, filters, history and library detail. Friendly dropdown
labels carry the language without a redundant product/language/repository
breadcrumb; .NET values are never presented as another language's
metrics, and observations/cohorts are scoped to the selected repository.

The dashboard uses an original, locally bundled Fluent-inspired layout: a compact
repository masthead, aligned overview cards, consistent provenance colors,
readable striped tables and quiet collection/baseline metadata. System fonts,
light/dark contrast and visible keyboard focus need no external assets or
telemetry. Package names stay on one line; narrow tables scroll inside a
keyboard-focusable region rather than overflowing the page. The provenance
disclosure explains why custom source is not customization debt.
Cards and chart labels use **Custom source**. Source-state badges and an
override for including uncommitted observations are not displayed; internal
source-state metadata and official publication checks remain enforced.

Library columns sort through their native header buttons, including keyboard
Enter/Space. New columns start ascending and repeated activation reverses the
direction; the active header exposes `aria-sort` and a visible arrow. The default
is custom lines descending. Numeric comparisons remain numeric, ties are stable
by library name, and N/A stays last in either direction. Filters, repository
changes and detail selection preserve the current library sort.

## Prerequisites and layout

Requires Node.js 22+, npm and the committed lockfile. PowerShell scripts require
PowerShell 7+. Browser checks use an installed Microsoft Edge. Pester 5.3.3+
supports offline wrapper tests. Deployment/publication additionally require
Azure CLI and explicitly authorized Entra access; local builds need no Azure
credentials.

| Path | Purpose |
| --- | --- |
| `main.tsp`, `tspconfig.yaml` | Single authoritative TypeSpec definition and JSON Schema emitter configuration |
| `schemas\*.schema.json` | Canonical generated snapshot/index/history schemas |
| `schema.mjs` | Generate/check schemas and byte-exact producer mirror sync/check |
| `dashboard` | Offline/hosted frontend, semantic validators, reporting and tests |
| `publishing.mjs`, `Publish-Metrics.ps1` | .NET feed publisher and Azure CLI token wrapper |
| `Collect-Metrics.ps1` | One measurement command operating on the caller's language checkout |
| `ci.yml` | Shared tooling validation CI; no scheduled source-repository sweep |
| `infra\main.bicep`, `Deploy-*.ps1` | Policy-safe hosting resources and separate deployment helpers |

From this repository's root:

```powershell
Push-Location .\tools\custom-code-metrics
npm ci
npm run check
npm test
Pop-Location
```

## Measurement and schema contract

This is a **source provenance metric**, not customization debt, public API impact,
or TypeSpec attribution. .NET observes shipping `Azure.*` libraries under `sdk`.
Its language-aware collector and counting details are maintained in
`doc\dev\CustomCodeMetrics.md` in the separate .NET producer checkout; those
producer changes have not yet landed on the repository's `main` branch.
Physical source lines include comments and blank lines. Categories are management,
data-plane and provisioning; each file is custom or generated, with no unknown
bucket or redundant measurement rules.

For every library/group, `totalLines = customLines + generatedLines`;
`customRatio = customLines / totalLines`, or `null` for a zero denominator.
Groups sum numerators and denominators before dividing, rather than averaging
library ratios. The initial format excludes linked files whose **actual Git path** matches
`sdk/core/<package>/src/Shared/**` from the counts and optional evidence of
other consuming libraries, regardless of generated markers. The owning core
library still counts its own `src/Shared` source. This does not exclude core
packages or all shared code: other linked source remains library-weighted.

Snapshot contract **1.0** includes observation identity/time, repository
name/commit/dirty state, weighted summary/category/service rollups, libraries and
exclusions. Optional file evidence contains paths/counts, never source content.
`ReportIndex` and `HistoryMonth` envelopes are also **1.0**, containing current 1.0
observations. Shape validation is generated from the schema; shared semantic
validators additionally enforce safe integers, arithmetic, rollups, identities,
file evidence consistency and conflicting retry rejection.
Snapshot identities must encode the same commit and UTC collection time,
including fractional seconds, in both complete snapshots and compact history.

Edit `main.tsp`, never the generated JSON. Compiler and emitter are pinned at
1.16.0. Regenerate and check from this package:

```powershell
npm run format
npm run generate
npm run check
```

`check` verifies formatting, compiles TypeSpec and compares all three emitted
schemas byte-for-byte without rewriting the canonical files. Generated IDs and
comments identify this package. Objects are sealed and reusable types bundled
under `$defs`. See the [TypeSpec JSON Schema decorators reference](https://typespec.io/docs/emitters/json-schema/reference/decorators/).
Changes to actual contract/counting rules require appropriate versioning.
This format is being introduced by an unmerged PR; its earlier prototype version
label 3.0 was administratively renumbered to initial 1.0 without changing fields,
membership or counting rules. Keep all earlier prototype files unchanged.
Current ingestion rejects prototype versions 0, 2 and 3 without conversion.
The old prototype also labeled 1.0 had rules metadata and an unknown bucket;
those fields are still rejected by the current sealed two-way contract.

An explicitly recorded, separate administrative copy may change only the version
label of the already measured artifact for initial-format validation. Its counts,
collection time, commit and snapshot identity remain unchanged; it is **not a new
measurement** or a code-improvement trend. No automatic conversion is performed.

The publisher validates **every** existing referenced history month and latest
snapshot before any uploads, including older months outside the incoming
observation's month. An incompatible prototype feed is rejected without replacing
its index or immutable blobs. There are no published playground Blob feeds
to migrate; do not repurpose an existing incompatible feed without an explicit
separate migration. Envelope field shapes/versions do not change.
The existing latest must also occur exactly once in that history and agree with
its compact observation; an independently valid but inconsistent latest cannot
be carried forward.

### Offline producer schema mirror

The .NET repository retains a generated `eng\scripts\CustomCodeMetrics.schema.json`
mirror for its offline collector/Pester tests. No TypeSpec or npm dependency is
needed to run the collector or the copy helper. After regenerating here, use
an explicit destination:

```powershell
node .\schema.mjs sync-copy C:\work\azure-sdk-for-net\eng\scripts\CustomCodeMetrics.schema.json
node .\schema.mjs check-copy C:\work\azure-sdk-for-net\eng\scripts\CustomCodeMetrics.schema.json
```

The helper can be invoked by absolute script path from any working directory.
Its canonical source is always script-relative; relative destinations resolve
against the caller's working directory. `sync-copy` creates missing destination
directories and copies exact bytes. `check-copy` never writes and fails on
drift or missing files. Commit regenerated canonical files and the producer
mirror together when changing the contract; never hand-edit either.

For VS Code, select this package's compiler:

```json
{
  "typespec.tsp-server.path": "${workspaceFolder}\\tools\\custom-code-metrics\\node_modules\\@typespec\\compiler"
}
```

## Dashboard builds and published indexes

From this package:

```powershell
npm run build:dashboard
Invoke-Item .\dashboard\dist\index.html
```

The bundled site opens directly from disk. Seeded builds need no runtime backend,
CDN or network. Browser file imports, manual index loading and the Clear data
action are not supported; observations come from validated build-time seeds or a
configured automatic hosted feed. Failed or inconsistent feed loads retain
existing observations in page memory.

To preload actual measurements, provide one or more real collector outputs:

```powershell
node .\dashboard\build.mjs --snapshot C:\work\azure-sdk-for-net\artifacts\custom-code-metrics\<snapshot>.json
```

Build output/data are ignored by Git. The default build explicitly clears seeds;
rebuild with measured data after an unseeded build. Test fixtures must not become
the published site's initial measurements.

Configure the reporting index at build/deployment time with `--index-url` or
`-IndexUrl`, not through browser controls. Hosted requests omit credentials;
cross-origin hosting must allow anonymous GET/CORS.

History includes only committed-source, compatible observations and defaults to a **fixed library cohort**
(the intersection of library IDs). Disable fixed cohort to see each day's
portfolio and additions/removals. Filters apply to weighted history too.
Missing days break the line; empty cohorts are N/A, not zero. Identical committed-source
same-revision daily retries deduplicate to the latest; conflicts fail explicitly.
One measured revision is a baseline, not a code-change trend.

## Publication and hosted reporting

Website builds and data publication are separate. Prepare only the shared
validators/Node publishing helpers for a collector pipeline:

```powershell
npm ci
npm run check
npm run build:publishing
.\Publish-Metrics.ps1 -SnapshotPath C:\data\<snapshot>.json `
  -StorageAccount <account> -SubscriptionId <subscription>
```

`build:publishing` does not build/write `dashboard\dist`, reset its seeds or
require the language repository to build a website. The wrapper can run from any
cwd; it resolves `publishing.mjs` relative to itself. It obtains a storage Entra
token from Azure CLI, passes it only in the child environment and restores the
previous environment. It does not use keys or SAS. Failures are explicit.

Official publication rejects dirty observations, inconsistent counts/identities,
incompatible data and conflicting measurements. The full audit goes only to the
private archive. Approved public snapshots omit file evidence; monthly history
contains library identity/service/category/counts and observation metadata, not
project/framework details or redundant rollups.

The current feed remains:

```text
reports/dotnet/index.json
reports/dotnet/snapshots/<snapshot-id>.json
reports/dotnet/history/<sha256>/<yyyy-mm>.json
```

Every JSON blob is gzip encoded. Immutable reporting data has one-year immutable
caching; the index revalidates. No-overwrite writes compare actual bytes on
retry. The publisher uploads archive/snapshot/content-addressed history before
ETag-guarded index publication. Partial failures preserve the previous index;
concurrent losers fail and must rerun against the new index. Never routinely
delete referenced immutable blobs.

For hosted mode:

```powershell
node .\dashboard\build.mjs --index-url "https://<approved-reader>/dotnet/index.json"
.\Deploy-Dashboard.ps1 -SubscriptionId <subscription> `
  -IndexUrl "https://<approved-reader>/dotnet/index.json"
```

The configured automatic anonymous startup loads latest plus 90-day monthly
history, not the full archive. Choose 30/90/365 days to load older months lazily.
Ranges end at the selected observation's UTC date, including older observations
provided as build-time seeds. Content hashes are verified, immutable months
reused from page cache, and failed range/selection changes roll back without losing prior data.
Staleness starts **strictly after 36 hours**. Unconfigured builds remain offline.

Deployment preflights latest/history before acquiring a site deployment token.
The pinned Static Web Apps CLI uses a transient environment token with credential
persistence disabled. HTML/unversioned JS/CSS revalidate. The site has no runtime
backend; a policy-approved external reader can supply its anonymous feed.

### Static preview deployment

To make a measured baseline visible before a live feed is available, deploy
one or more **absolute snapshot file paths** instead of `-IndexUrl`:

```powershell
.\Deploy-Dashboard.ps1 -SubscriptionId <subscription> `
  -SnapshotPath C:\data\<actual-snapshot>.json
```

`-SnapshotPath` and `-IndexUrl` are mutually exclusive. Preview builds validate
the complete input before requesting a deployment secret, require committed-source
observations, and strip optional file-level audit evidence from public seeds
without modifying the source. They do not read Blob storage or fetch any feed.
A seeded build retains the collection date/revision in observation metadata
and the embedded-baseline/no-automatic-feed fact in its quiet footer. Persistent
preview, loaded-count and embedded-staleness banners are not displayed. Genuine
loading/error notifications remain explicit; hosted-feed health is discreet
footer metadata. A single measured revision remains a baseline, not a fabricated
trend. Filters remain available; the preview does not expose a manual index
loader, browser file picker or Clear data control. Configured automatic
hosted-feed loading is a separate build/deployment mode.

For a local preview build without deployment:

```powershell
node .\dashboard\build.mjs --preview --snapshot C:\data\<actual-snapshot>.json
```

Repeat `--snapshot` for additional real observations. Ordinary `--snapshot`
builds retain local evidence and offline behavior; only explicit public preview
mode strips evidence and records quiet baseline metadata. Both deployment modes use the pinned
Static Web Apps CLI and transient deployment-token environment handling.

## Infrastructure and activation prerequisites

`infra\main.bicep` provisions a Free Static Web App, Hot LRS storage, versioning
and 14-day soft delete, private archive/report containers and a managed publisher
identity. Shared-key authentication is disabled. Defaults keep reports private.
`-PublicReports` is only for an already approved environment permitting anonymous
report blobs; it is not a workaround for organizational policy. A permitted public
profile limits Blob CORS to the site origin and GET/HEAD, never container listing.

`Deploy-Infrastructure.ps1 -ProvisionResourcesOnly` skips role assignments.
The compatible `-Owner` parameter accepts one individual Microsoft/GitHub alias
or Microsoft UPN and emits the documented **`Owners`** tracking tag, not the
unrecognized singular `Owner` tag. `-Purpose` records the resource's purpose
on the group and owned resources. These format checks do not verify that an
individual is linked to GitHub or that cleanup eligibility has been approved.
See the [resource management guidelines](https://github.com/Azure/azure-sdk-tools/blob/main/doc/engsys_resource_management.md).
Playground resources are expiry-managed; persistent hosting requires EngSys
coordination. The helper adds no cleanup bypass, non-expiring lease or exemption.
`-Environment` defaults to `EngineeringSystem` for accurate tracking metadata.
`-StorageAccountName` accepts an optional lowercase 3-24-character name; omitting
it preserves the resource-group-unique storage-name default.

For an explicitly approved **existing** resource group, use
`-UseExistingResourceGroup` with an explicit storage name. This mode verifies the requested group identity,
refuses collisions with the dedicated metrics names, and deploys incrementally
without calling resource-group creation or changing its tags:

```powershell
.\Deploy-Infrastructure.ps1 -SubscriptionId <approved-subscription> `
  -ResourceGroup <approved-existing-group> -UseExistingResourceGroup `
  -Location westus2 -Owner <individual-alias> `
  -StorageAccountName <available-name> -Environment EngineeringSystem `
  -BootstrapPrincipalId <approved-user-object-id>
```

Select a supported resource location explicitly; an existing group's location
need not be supported by Static Web Apps. Existing-group mode is for creating
new dedicated resources, not overwriting another project's resources. It
grants no retention exemption and does not modify group locks or cleanup tags.
The helper retains a `-BootstrapPrincipalId` parameter for its manual bootstrap
profile; personal writer grants are an optional administrative convenience, not
a runtime architectural requirement. The scheduled publisher needs scoped
Storage Blob Data Contributor on archive/reports. A private-storage reader needs
only Storage Blob Data Reader on reports, never archive.

**The static preview is restored in the user-approved Engineering System
`typespec` resource group; automatic reporting is not operational.**
The [current preview](https://orange-pebble-01bfc3d1e.6.azurestaticapps.net) embeds
the administrative initial-1.0 copy of the measured October 7 observation.
It remains a single baseline with quiet no-automatic-feed metadata, not a new
measurement, a nightly feed or evidence that all language adapters exist.

The preserved October 7 prototype-3 .NET observation and its separate
initial-format copy both contain
459 libraries, 276 services and 7.32% custom source.
The measured `Azure.Provisioning.CostManagement` library has 120 custom
lines out of 8,817 total lines (1.36%), with foreign core Shared helpers
excluded. The change from earlier v2 percentages reflects the new membership
rules, not a measured code improvement.

The new dedicated resources are in subscription
`a18897a6-7e44-457d-9260-f2854c0aca42`, existing resource group `typespec`.
The Free Static Web App is `azsdk-custom-code-metrics`; the private Hot LRS
storage account is `azsdkcustommetrics` and the dedicated publisher identity
is `id-azsdk-custom-code-metrics`. Resources use West US 2 while the existing
group remains in West US with its original metadata. Its tags and all twelve
pre-existing resources were verified unchanged after deployment.

The new report/archive containers remain private, public Blob and shared-key
access remain disabled, and only container-scoped Storage Blob Data Contributor
roles were created for the dedicated publisher and approved manual user.
No federation or reporting feed was activated and no Blob observations were
published; the preview embeds approved compact data without file-level evidence.
User-approved placement does not establish a cleanup exemption. No group
tracking tags, lease, lock or allowlist was changed.

For the historical outage, Azure activity records show the external
`azure-sdk-tests` service principal deleting the former Playground resources
during October 8, 2026, 00:19-00:21 UTC, before initial-format renumbering.
They were in `rg-azsdk-custom-code-metrics`, subscription
`faa080af-c1d8-40ad-9cce-e1a450ca5b57`, with storage account
`azsdkcmibsokvwfsterm` and obsolete hostname
`agreeable-rock-0fabe8a1e.4.azurestaticapps.net`. Those resources were not recreated.
The singular tracking-tag mismatch was a documented setup gap, not proof of the
deleted group's exact tags or cleanup decision branch.
No pipeline/schedule is registered and no approved reader API is implemented.
Do not bypass policy with account keys, browser SAS, or policy overrides.

### Language-owned collection and publication

Language repositories own their schedules, trusted checkouts and publication jobs.
There is no tools-owned nightly sweep, repository cloning or seven-language
success loop. A language job uses one shared `Collect-Metrics.ps1` command on
its own checkout, then publishes the returned observation directly to Blob
storage through `Publish-Metrics.ps1`. Website deployment is separate.

`Collect-Metrics.ps1 -Language dotnet -RepoRoot <checkout> -OutputDirectory <path>`
defaults to `dotnet`. This is the only implemented adapter: it checks the exact
schema mirror, runs the producer's Pester suite and calls its MSBuild-aware
collector from the producer checkout. It returns exactly one completed snapshot
path and restores the caller's working directory. Relative output paths resolve
against the caller, not the producer.

The recognized future language names are `java`, `js`, `python`, `go`, `rust`
and `cpp`; requesting any of them fails explicitly **before testing or writing
an observation**. Unknown names are rejected. The selector's uncollected states
do not imply that these adapters or a generic counting contract exist.

For a .NET-owned CI job, after checking out a trusted tools revision and installing
Node.js, Pester and the exact .NET SDK from its own `global.json`:

```powershell
$repo = "C:\work\azure-sdk-for-net"       # This job's own trusted source checkout
$tool = "C:\work\azure-sdk-tools\tools\custom-code-metrics"
Push-Location $tool
try {
    npm ci
    if ($LASTEXITCODE -ne 0) { throw "Shared tooling restore failed." }
    npm run check
    if ($LASTEXITCODE -ne 0) { throw "Shared schema validation failed." }
    npm run build:publishing
    if ($LASTEXITCODE -ne 0) { throw "Publisher build failed." }
    $snapshot = & .\Collect-Metrics.ps1 -Language dotnet -RepoRoot $repo -OutputDirectory "C:\artifacts\metrics"
    # Run this publication step inside the language job's authorized AzureCLI task.
    & .\Publish-Metrics.ps1 -SnapshotPath $snapshot `
        -StorageAccount <account> -SubscriptionId <subscription>
}
finally {
    Pop-Location
}
```

This example is caller wiring, not a registered pipeline or a new SDK CI change.
Land the separate .NET producer changes before using the adapter; they are not yet
on `main`. The caller owns exact checkout verification, main-branch/trusted-source
gates, artifact retention and a workload-federated ARM identity scoped to the
report/archive containers. Never authorize publication for untrusted PR/fork
jobs or all pipelines. Register this package's `ci.yml` only for shared tool
validation (`tools - custom-code-metrics - ci`).

Private storage still requires an approved anonymous reports-only reader before
the website can consume a live feed. That reader and language-owned schedules
are not implemented by this PR. Verify actual publication and scheduled execution
before calling nightly reporting operational.

### Safe dry runs

`Publish-Metrics.ps1`, `Deploy-Dashboard.ps1` and `Deploy-Infrastructure.ps1`
support `-WhatIf`. Declined operations make no Azure calls, retrieve no tokens
and launch no publisher or deployment process. Dashboard dry runs also skip
local builds and feed requests. Without `-WhatIf`, the existing validation,
preflight and transient credential handling remain unchanged.

## Validation

From this package, with Pester and Edge available:

```powershell
.\Validate.ps1
```

This runs schema formatting/compilation/freshness, Node unit/relocation tests,
strict TypeScript checks, real Edge offline/mocked-hosted checks and offline
PowerShell wrapper/adapter tests, then leaves a clean **unseeded** site.
For targeted checks use `npm test`, `npm run test:publishing`,
`npm run test:dashboard:browser`, or `Invoke-Pester .\tests -Output Detailed`.
No test publishes or changes Azure resources.

Use `CUSTOM_CODE_METRICS_SNAPSHOT` to validate real collector output in data and
browser tests. Browser tests otherwise use synthetic fixtures confined to tests.
For a final measured-data local preview, rebuild explicitly with
`node .\dashboard\build.mjs --snapshot <actual-path>` after validation.

Preserved tests cover weighted counts/filtering, safe integers, evidence,
rollups, retry conflicts, cohorts/missing-day gaps, anonymous latest and lazy
months, content integrity, UTC anchors, stale boundaries, caching/rollback,
immutable retries, gzip/cache metadata, ETag races and private audit separation.
Relocation coverage checks byte-exact mirror sync/read-only failures, missing
files, script-relative schema/build paths, publisher-only output isolation and
offline per-repository command and shared CI boundaries. The .NET repository owns
collector counting tests.
Repository UI coverage checks all seven choices, honest unavailable states without
cross-repository values or feed requests, .NET filter/history/detail restoration,
declared-repository mismatch rejection, and the full real portfolio at 390px
both initially and immediately after resizing with an open library detail.
Visual checks cover 1440/1280px desktop and 390/320px narrow layouts in both
light and dark mode, painted charts, equal summary cards, readable typography,
contrast, keyboard focus and horizontal table scrolling. Set
`CUSTOM_CODE_METRICS_SCREENSHOT_DIRECTORY` to an absolute artifact directory
alongside `CUSTOM_CODE_METRICS_SNAPSHOT` when running browser checks to retain
the viewport/theme screenshots for inspection.
