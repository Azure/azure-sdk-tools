# Service API-Version Readiness

Status: Draft implementation contract; review and a CLI release are required
before the automation integration is enabled.

## Purpose and Ownership

Approved SDK PRs can wait for confirmation that their API version is deployed.
This read-only CLI/MCP check reports whether public Azure ARM metadata lists the
exact version used to generate the SDK. It is not release approval, a health
check, or proof that every operation works in every region. Data-plane SDKs are
not applicable.

The public Tools CLI owns metadata resolution, pinned-spec mapping, Azure access,
and the result. The automation worker owns events, comments, labels, and stale
results. Ingress does not run the check; topology, provisioning, and rollback
remain unchanged.

## Command and Inputs

```sh
azsdk pkg check-api-version --owner Azure --repo azure-sdk-for-java --pr 123 \
  --expected-head-sha <full-sdk-pr-head-sha> --output json
```

MCP: `azsdk_check_service_api_version`. Inputs are owner, one of the five SDK
repository names below, a positive SDK PR number, and an optional full expected
head SHA. A spec PR is not an SDK PR: use the spec commit and project recorded in
SDK files. There is no version override, arbitrary endpoint, or mutation option.

## Authentication

Reuse the CLI resolver in this order: `GITHUB_TOKEN`, legacy
`GITHUB_PERSONAL_ACCESS_TOKEN`, then `gh auth token`. The worker passes its
short-lived App installation token only through the child's `GITHUB_TOKEN`,
never arguments or logs. A rejected supplied token does not trigger a fallback
to another identity. Local users can use their existing `gh auth` login.

The 40-second evidence deadline includes token discovery. Cancellation prevents
or stops `gh`; process output logging is off, and failures exclude stdout/stderr.
ARM uses separate developer credentials or the worker's configured managed
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
7. Re-read the PR head and state. A changed or closed PR cannot pass.

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

## Testing and Rollout

The CLI and automation PRs are independently reviewable. Unit tests cover the
language layouts, statuses, credentials, limits, mapping, cancellation, stale
heads, and safe errors. Automation tests cover the process contract, comments,
labels, timeout, approval removal, and overlapping deliveries. Verify live CLI
results and worker publication separately; mocks do not prove hosted identity,
installation, or Azure access.

Install a CLI release containing the command or a validated trusted artifact
before enabling the worker integration. Merged source is not an installed CLI.
These PRs do not merge SDKs, publish packages, deploy infrastructure, or change
Azure permissions or hosted allowlists.