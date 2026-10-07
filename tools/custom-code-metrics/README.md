# Custom code metrics

Shared TypeSpec contracts, a static dashboard, immutable publication tools and
cross-repository collection orchestration for Azure SDK custom-source metrics.
The collectors stay in their language repositories; tools owns the website,
contracts, publishing, hosting infrastructure and central nightly schedule.

**Only the .NET adapter and .NET snapshot v3.0 are implemented today.** Moving
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
| `Collect-DotNetMetrics.ps1` | Central adapter invoking the .NET repository's tested collector |
| `ci.yml`, `nightly.yml` | Validation CI and standalone central collection/publication definition |
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
library ratios. Version 3 excludes linked files whose **actual Git path** matches
`sdk/core/<package>/src/Shared/**` from the counts and optional evidence of
other consuming libraries, regardless of generated markers. The owning core
library still counts its own `src/Shared` source. This does not exclude core
packages or all shared code: other linked source remains library-weighted.

Snapshot contract **3.0** includes observation identity/time, repository
name/commit/dirty state, weighted summary/category/service rollups, libraries and
exclusions. Optional file evidence contains paths/counts, never source content.
`ReportIndex` and `HistoryMonth` envelopes remain **1.0**, containing v3.0
observations. Shape validation is generated from the schema; shared semantic
validators additionally enforce safe integers, arithmetic, rollups, identities,
file evidence consistency and conflicting retry rejection.

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
The linked-core-helper exclusion is a membership change from v2 to v3, not
an observed code improvement. Keep existing v2 snapshots/immutable reports
untouched. Current ingestion strictly rejects v2 snapshots/history rather than
converting or connecting them to v3 trends. Start a new v3 baseline.

The publisher validates **every** existing referenced history month and latest
snapshot before any uploads, including older months outside the incoming
observation's month. An incompatible v2 feed is rejected without replacing
its index or immutable blobs. There are no published playground Blob feeds
to migrate; do not repurpose an existing incompatible feed without an explicit
separate migration. Envelope field shapes/versions do not change.

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
CDN or network. Browser file imports and the Clear data action are not supported;
observations come from validated build-time seeds or a published index. Failed or
inconsistent index loads retain existing observations in page memory.

To preload actual measurements, provide one or more real collector outputs:

```powershell
node .\dashboard\build.mjs --snapshot C:\work\azure-sdk-for-net\artifacts\custom-code-metrics\<snapshot>.json
```

Build output/data are ignored by Git. The default build explicitly clears seeds;
rebuild with measured data after an unseeded build. Test fixtures must not become
the published site's initial measurements.

The manual **Load a published snapshot index** option retains the legacy
`{"snapshots":["one.json","two.json"]}` format. Relative snapshot URLs resolve
against the HTTP(S) index; cross-origin hosting must allow anonymous GET/CORS.
Requests omit credentials.

History defaults to clean, compatible observations and a **fixed library cohort**
(the intersection of library IDs). Disable fixed cohort to see each day's
portfolio and additions/removals. Filters apply to weighted history too.
Missing days break the line; empty cohorts are N/A, not zero. Identical clean
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
loaded from published indexes. Content hashes are verified, immutable months
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
the complete input before requesting a deployment secret, require clean
observations, and strip optional file-level audit evidence from public seeds
without modifying the source. They do not read Blob storage or fetch any feed.
The deployed site retains the collection date/revision in observation metadata
and the embedded-baseline/no-automatic-feed fact in its quiet footer. Persistent
preview, loaded-count and embedded-staleness banners are not displayed. Genuine
loading/error notifications remain explicit; hosted-feed health is discreet
footer metadata. A single measured revision remains a baseline, not a fabricated
trend. Published-index loading and filters remain
available; there is no browser file picker or Clear data control.

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
The helper retains a `-BootstrapPrincipalId` parameter for its manual bootstrap
profile; personal writer grants are an optional administrative convenience, not
a runtime architectural requirement. The scheduled publisher needs scoped
Storage Blob Data Contributor on archive/reports. A private-storage reader needs
only Storage Blob Data Reader on reports, never archive.

**The static preview is deployed; nightly reporting is not operational.** The
[public preview](https://agreeable-rock-0fabe8a1e.4.azurestaticapps.net) shows the
validated October 7, 2026 v3 .NET baseline (459 libraries, 276 services, 7.32%
inferred custom source). It embeds compact data, not a live feed; its collection
date and baseline/no-automatic-feed metadata remain visible without warning banners.
The v3 provisioning `Azure.Provisioning.CostManagement` library has 120 custom
lines out of 8,817 total lines (1.36%), with foreign core Shared helpers
excluded. The change from earlier v2 percentages reflects the new membership
rules, not a measured code improvement; the website starts a separate v3 baseline.

Private containers exist in
`azsdkcmibsokvwfsterm` / `rg-azsdk-custom-code-metrics`, subscription
`faa080af-c1d8-40ad-9cce-e1a450ca5b57`. Public Blob access remains prohibited
and shared keys remain disabled. Container-scoped writer roles were configured
separately; deploying this preview did not change those roles or publish Blob
data. No pipeline/schedule is registered and no approved reader API is
implemented. Do not bypass policy with account keys, browser SAS, or policy
overrides.

### Central nightly orchestration

`nightly.yml` is a tools-owned standalone **definition**, not an active schedule.
It schedules 08:00 UTC on tools `main` and allows publication only from trusted
internal tools `main` manual/scheduled runs, never PR/fork runs. It checks out
self/tools and .NET `main` into explicit separate paths, verifies each exact
resolved commit and clean tracked state, installs the exact checked-out .NET
`global.json` SDK, checks canonical schemas and the .NET mirror, runs shared
publisher and .NET collector tests, collects through the .NET adapter, retains
the snapshot artifact and publishes via workload federation. It does not deploy
the website. Other language adapters are not silently included.

Before activation:

1. Land coordinated tools and .NET changes and register this YAML in the internal
   Azure SDK project (`tools - custom-code-metrics`); register `ci.yml` separately
   as `tools - custom-code-metrics - ci` for validation.
2. Configure `DotNetGitHubServiceConnection` to an explicitly authorized
   **read-only GitHub endpoint for Azure/azure-sdk-for-net**. The default
   `SET_READ_ONLY_GITHUB_SERVICE_CONNECTION` is a placeholder, not an existing
   authorized connection. Set the parameter's YAML default to the approved
   endpoint name before enabling scheduled runs.
3. Grant the publisher's container roles and create/authorize the
   `azure-sdk-playground-custom-code-metrics` workload-federated ARM connection
   with its exact Azure DevOps issuer/subject. Restrict it to this pipeline and
   trusted tools/main checks; never authorize all pipelines or PR/fork jobs.
4. Provision an approved anonymous reports-only reader if storage remains
   private. Verify real publication, reader output, hosted site and scheduled
   execution, and subscribe owners to failures before declaring this operational.

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
offline pipeline guards. The .NET repository owns collector counting tests.
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
