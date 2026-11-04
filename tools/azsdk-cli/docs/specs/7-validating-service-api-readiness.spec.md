# Service API-Version Readiness

Status: Draft CLI implementation contract; review and a CLI release are required
before consumers use the command.

## Purpose and Ownership

Approved SDK PRs can wait for confirmation that their API version is deployed.
This read-only CLI command reports whether public Azure ARM metadata lists the
exact version used to generate the SDK. It is not release approval, a health
check, or proof that every operation works in every region. Data-plane SDKs are
not applicable.

The CLI owns metadata resolution, pinned-spec mapping, Azure access, and the
result for the captured SDK PR head. It does not publish comments, change labels,
or approve a release.

## Command and Inputs

```sh
azsdk pkg check-api-version --owner Azure --repo azure-sdk-for-java --pr 123 --output json
```

Inputs are owner, one of the five SDK repository names below, and a positive SDK
PR number. The command reads the head SHA from the PR once and returns evidence
for that SHA; callers do not supply a SHA. Callers that publish results must
compare the returned SHA with the current PR head immediately before writing.
There is no MCP entry point.
A spec PR is not an SDK PR: use the spec commit and project recorded in SDK files.
There is no version override, arbitrary endpoint, or mutation option. Local-path
validation is deferred.

## Authentication

Reuse the CLI resolver in this order: `GITHUB_TOKEN`, legacy
`GITHUB_PERSONAL_ACCESS_TOKEN`, then `gh auth token`. Callers pass tokens only
through the child's `GITHUB_TOKEN`, never arguments or logs. A rejected supplied token does not trigger a fallback
to another identity. Local users can use their existing `gh auth` login.

The 40-second evidence deadline includes token discovery. Cancellation prevents
or stops `gh`; process output logging is off, and failures exclude stdout/stderr.
ARM uses separate developer credentials or a configured managed
identity through the Azure credential service. Tokens stay on their fixed API
hosts, redirects are rejected, and no identities or role assignments are created.

## Evaluation

1. Read the open SDK PR, changed packages, and generation files from its actual
  head repository and SHA. PR descriptions and comments are not evidence.
2. Resolve the recorded spec repository, full commit, project, provider, and exact
  API version. Reject missing, invalid, conflicting, or ambiguous evidence.
3. Read the pinned TypeSpec configuration and checked-in Swagger. Do not compile
  TypeSpec, scan the repository, or select the latest version.
4. Map resource types conservatively. An unresolved child cannot pass using its
  parent's advertised version.
5. Reuse one read-only ARM lookup per provider namespace:
   `GET https://management.azure.com/providers/{namespace}?api-version=2021-04-01`.
  This metadata API version is not the service API version being checked.
6. Require at least one exact-version match per affected management project.
  Every affected project must pass for an overall Passed.

### Expected Evidence by Language

The same rule applies to all five languages. SDK package versions and preview
suffixes are not service API versions.

| SDK repository | Package root | Checked-in API-version evidence relative to that root |
| --- | --- | --- |
| `azure-sdk-for-net` | `sdk/<service>/<package>` | `metadata.json` |
| `azure-sdk-for-java` | `sdk/<service>/<package>` | Every top-level `src/main/resources/META-INF/*_metadata.json`, bounded to 32 files |
| `azure-sdk-for-js` | `sdk/<service>/<package>` | `metadata.json`, including scalar `apiVersion` |
| `azure-sdk-for-python` | `sdk/<service>/<package>` | `_metadata.json`, including scalar `apiVersion` |
| `azure-sdk-for-go` | `sdk/resourcemanager/<service>/<module>` | `testdata/_metadata.json` |

All require package-root `tsp-location.yaml` with the spec repository, full commit
SHA, and TypeSpec project directory. Missing provenance is Inconclusive; do not
guess from a package name or current `main`.

`apiVersions` maps providers to exact versions. Scalar `apiVersion` uses the
provider in pinned `main.tsp`, not a language namespace. Deduplicate identical
versions; reject conflicts, conflicting optional provenance, and malformed Java
metadata. Validate every matching Java file, not just the package-named file.

Include deletion-only packages and rename source packages unless their directory
is absent at the checked head. No surviving affected package means Inconclusive.
Recognize `resource-manager` and `.Management` paths before using labels as a
fallback for the API plane.

### What Demonstrates API Readiness

For pinned Swagger `/providers/Microsoft.Example/widgets/{name}` with
`info.version: 2026-01-01`, resolve `widgets` and look for the exact string
`2026-01-01` in that resource type's ARM `apiVersions`. A hit is Passed evidence.
Applicable types without a hit mean Not detected; missing provider/type evidence
means Inconclusive. Authentication or network failure is Error.

Read only bounded, explicit checked-in Swagger mappings at the recorded commit.
Large files use raw content on `api.github.com` at the same revision, never
`download_url`. Responses are capped at 4 MiB. Do not call resource operations,
require operations-list endpoints, probe regions, create resources, or run samples.

## Versioned Result Contract

The root contains `contractVersion: 1`, `status`, `headSha`, `headRepository`,
`plane`, `checkedAt`, `detail`, and `projects`. Read `status` for the readiness
decision, not the CLI-wide `operation_status`; `response_error` may also appear.

Projects contain `source` (package, generation-file paths, pinned spec repository,
commit, configuration, and API version), `status`, `namespace`, `detail`, and
`matches`. Matches contain `resourceType` and advertised `regions`, not regional
health guarantees.

| Status | Meaning | Example |
| --- | --- | --- |
| Passed | At least one applicable type advertises the exact version for every checked project. | The supported-version list includes `2026-01-01`. |
| Not detected | Applicable types exist, but none advertise the version. | Only older versions are listed. |
| Inconclusive | Evidence or mapping is missing, conflicting, or unavailable. | SDK API-version metadata is missing. |
| Error | Authentication, transport, cancellation, or another application failure prevented the check. | ARM denied access or the command timed out. |

Exit zero means Passed, Not detected, or Inconclusive; exit one means Error.
Always read `status`. Treat an unusable or unknown contract as Error.

## Testing and Availability

Unit tests cover language layouts, statuses, credentials, limits, mapping,
cancellation, captured-head evidence, and safe errors. Verify live CLI results
separately; mocks do not prove hosted identity, installation, or Azure access.

Use a CLI release containing the command or a validated trusted artifact.
Merged source is not an installed CLI. The command does not merge SDKs, publish
packages, deploy infrastructure, or change Azure permissions or hosted allowlists.