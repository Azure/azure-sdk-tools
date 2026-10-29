# Service API-Version Readiness

Status: Draft implementation contract; review and a CLI release are required
before the automation integration is enabled.

## Background and Scope

Approved SDK pull requests can wait for manual confirmation that their service
API version is deployed. This read-only check reports whether public Azure ARM
provider metadata advertises the exact version used to generate the SDK. It is
not release approval, service-health validation, or proof that every operation
is available in every region. Data-plane SDKs are not applicable.

## Ownership

- Public `Azure/azure-sdk-tools`: the azsdk CLI/MCP command, authoritative SDK
  metadata resolution, pinned-spec mapping, Azure identity, provider lookup,
  evaluation, and structured evidence.
- SDK automation worker: event handling, calling the command, rendering one
  App-owned comment, label reconciliation, and stale-result invalidation.
- Existing ingress, queue, worker topology, provisioning, and rollback stay
  unchanged. Ingress does not execute this check.

## Command and Inputs

```sh
azsdk pkg check-api-version --owner Azure --repo azure-sdk-for-java --pr 123 \
  --expected-head-sha <full-sdk-pr-head-sha> --output json
```

The MCP entry point is `azsdk_check_service_api_version`. Inputs are repository
owner, one of the five SDK repository names, positive SDK PR number, and an
optional expected full head SHA. There is no API-version override, arbitrary
service endpoint, or mutation option. A spec PR is not an SDK PR: source identity
comes from the SDK files' recorded spec commit and TypeSpec project.

## Authentication

GitHub access reuses the CLI token resolver. The worker supplies its short-lived
GitHub App installation token through the child's `GITHUB_TOKEN` environment,
never command arguments or logs. Local users can use the existing `gh auth`
login. Tokens are sent only to the fixed GitHub API host; the tool never reads
comments or PR descriptions as generation evidence.

Token precedence is `GITHUB_TOKEN`, the existing legacy
`GITHUB_PERSONAL_ACCESS_TOKEN` fallback, then `gh auth token`. A supplied App
token is used as-is; authentication rejection does not silently switch to a
developer's token. The readiness command uses the cancellation-aware resolver,
so its 40-second evidence deadline also covers local token discovery. An
already-canceled call does not launch `gh`, process output logging is disabled,
and failed token discovery does not include captured stdout/stderr in the result.

ARM access uses the CLI's Azure credential service: developer credentials
locally and the worker's configured managed identity in Azure. The GitHub token
is never sent to ARM, and an Azure token is never passed through GitHub.
Redirects are rejected. This feature creates no identity or role assignment.

## Evaluation

1. Read the current open SDK PR and discover affected packages from its changed
   files. Read generation files from the actual head repository at that SHA.
2. Resolve the recorded spec repository, full commit, project, provider, and
   exact API version. Reject missing, malformed, conflicting, or ambiguous data.
3. Read the pinned TypeSpec configuration and checked-in Swagger. Do not compile
   TypeSpec, scan the whole spec repository, or select the latest version.
4. Map relevant resource types conservatively. An unresolved child route cannot
   pass using a parent resource's advertised version.
5. Make one read-only ARM lookup per provider namespace and reuse its result:
   `GET https://management.azure.com/providers/{namespace}?api-version=2021-04-01`.
   This ARM metadata API version is distinct from the service API version.
6. Require at least one exact-version match per affected management-plane
   project, and require every affected project to pass for an overall Passed.
7. Re-read PR head/state before returning. A changed or closed PR cannot receive
   a passing conclusion for the earlier revision.

### Expected Evidence by Language

The readiness rule is the same for .NET, Java, JavaScript, Python, and Go; SDK
package versions and language-specific preview suffixes are not service API
versions. Discovery differs only in the package layout and generated evidence:

| SDK repository | Package root | Checked-in API-version evidence relative to that root |
| --- | --- | --- |
| `azure-sdk-for-net` | `sdk/<service>/<package>` | `metadata.json` |
| `azure-sdk-for-java` | `sdk/<service>/<package>` | Every top-level `src/main/resources/META-INF/*_metadata.json`, bounded to 32 files |
| `azure-sdk-for-js` | `sdk/<service>/<package>` | `metadata.json`, including scalar `apiVersion` |
| `azure-sdk-for-python` | `sdk/<service>/<package>` | `_metadata.json`, including scalar `apiVersion` |
| `azure-sdk-for-go` | `sdk/resourcemanager/<service>/<module>` | `testdata/_metadata.json` |

All five also require `tsp-location.yaml` at the package root, with the recorded
spec repository, full commit SHA, and TypeSpec project directory. This check is
for TypeSpec-generated SDK packages; missing provenance is Inconclusive, not
permission to guess from a package name or current `main`.

`apiVersions` maps provider namespaces to exact service versions. Scalar
`apiVersion` binds to the provider declared by the pinned `main.tsp`; this is
not inferred from a language namespace. Identical versions are deduplicated.
Conflicting versions, conflicting optional spec provenance, or a malformed Java
metadata file prevent an overall Passed result. Every matching Java file is
validated, not just the file whose name matches the package basename.

Changed files include deletion-only packages and the source of a cross-package
rename. Such a package is excluded only when its directory is absent at the
checked SDK head. Existing packages cannot be skipped merely because their
changed files were deleted. A PR with no surviving affected package is
Inconclusive. Recognized management paths include both `resource-manager`
directories and `.Management` projects; labels are only a fallback when the
recorded path cannot establish the plane.

### What Demonstrates API Readiness

For example, the pinned Swagger may expose
`/providers/Microsoft.Example/widgets/{name}` with `info.version` equal to the
SDK's `2026-01-01`. The check resolves `widgets`, requests the
`Microsoft.Example` provider metadata, and searches that resource type's
`apiVersions` for the exact string `2026-01-01`. A hit is the project's Passed
evidence. If applicable types are present but no exact string matches, the
project is Not detected. A missing provider or unresolved type is Inconclusive;
authentication or network failure is Error.

The check does not call service resource operations, require an operations-list
endpoint, probe every region, create resources, or run SDK samples. It reads
only explicit, bounded checked-in Swagger output mappings at the recorded spec
commit. Files above the Contents API's base64 threshold use bounded raw content
on `api.github.com` at that same revision, never an arbitrary `download_url`.
Evidence responses are capped at 4 MiB and redirects are rejected.

An unresolved child type/name cannot borrow the parent type's deployment signal.
When several SDK packages or provider namespaces are affected, each resolved
project must pass; one passing language package cannot hide another package's
missing evidence or absent version. Matching resource types and their advertised
locations are included for service-owner review. Even Passed remains only a
provider-manifest deployment signal, not proof that all operations or all regions
work. This expectation is deliberately identical across the five languages.

## Versioned Result Contract

The root JSON object contains `contractVersion: 1`, `status`, `headSha`,
`headRepository`, `plane`, `checkedAt`, `detail`, and `projects`. CLI-wide fields
such as `operation_status` and `response_error` may also be present. The readiness
decision is `status`, not the generic CLI execution status.

Each project includes its `source` (SDK package, generation-file paths, pinned
spec repository/commit/configuration, API version), `status`, `namespace`,
`detail`, and `matches`. Each match contains `resourceType` and advertised
`regions`. Advertised regions are evidence, not per-operation regional proof.

| Status | Meaning | Example |
| --- | --- | --- |
| Passed | At least one applicable type advertises the exact version for every checked project. | The supported-version list includes `2026-01-01`. |
| Not detected | Applicable types exist, but none advertise the version. | Only older versions are listed. |
| Inconclusive | Evidence or mapping is missing, conflicting, or unavailable. | SDK API-version metadata is missing. |
| Error | Authentication, transport, cancellation, or another application failure prevented the check. | ARM denied access or the command timed out. |

Exit code is zero for a completed observation (Passed, Not detected, or
Inconclusive), and one for Error. Consumers must read the readiness status even
when the exit code is zero. An unusable or unknown contract is treated as Error.

## Worker and Comment Contract

- Adding `review-approved` requests a check; data-plane results are skipped.
- The worker action has a 60-second timeout override. Readiness work is bounded
  to 45 seconds, leaving time to publish a safe result. The CLI bounds its own
  data/ARM work to 40 seconds and honors cancellation.
- Only a valid Passed result for the current open, approved head may add
  `service-api-version-check-passed`. Other outcomes appear in the comment only;
  no failure, unknown, or error label is added. An old passed label is removed.
- A new commit or removal of `review-approved` clears stale passing state
  without automatically re-running the check. Description edits cannot change
  the authoritative checked version.
- Update one comment belonging to the configured App. Show status, checked SDK
  SHA, exact version, pinned spec commit, matched types/regions, and missing
  evidence or a safe error summary. Do not expose credentials or raw failures.
- Concurrent or delayed deliveries must not publish an older passing result
  over a newer head or later non-passing result. Human comments are untouched.

## Rollout and Testing

The public Tools PR and automation PR are independently reviewable. The worker
requires a released CLI version containing this command or an explicitly
provisioned, validated CLI artifact. Do not claim that a merged source change is
already installed in the hosted worker.

Unit tests cover five metadata layouts, four statuses, identity separation,
bounded discovery, mapping failures, cancellation, stale heads, and safe errors.
The automation suite covers the process contract, comments, labels, timeout,
approval removal, and overlapping deliveries. Live fork tests must demonstrate
the CLI result and worker publication separately; mocked tests do not establish
hosted identity, installation, or Azure access.

Useful agent prompt: "Check the service API-version readiness for SDK PR
<URL> and explain the evidence without changing the PR or releasing anything."

No merge, package publication, infrastructure deployment, Azure permission
change, or hosted allowlist change is part of these two implementation PRs.