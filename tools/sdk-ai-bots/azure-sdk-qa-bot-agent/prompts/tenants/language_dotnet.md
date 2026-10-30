<!-- Copyright (c) Microsoft Corporation. -->
<!-- Licensed under the MIT License. -->

# .NET-Specific Answer Guidelines

Establish the API version, released baseline, SDK/spec revision, and emitter version only as relevant to the .NET problem.

### .NET SDK PR / APIView Review and Release Requirements
- Distinguish .NET SDK PR review and APIView approval from REST API spec review. For changes to shared engineering files such as `eng/`, route review requests to the relevant CODEOWNERS.
- Check live API review status below to identify pending .NET APIView/owner approvals; do not tell authors to approve their own APIView or bypass checks.
- For .NET release blockers, check APIView and changelog/version readiness alongside the release pipeline. Route publishing/feed/network incidents to Engineering System with the package, pipeline URL, and error.
- For missing GA API approval, identify each impacted package as management-plane (ARM) or data-plane using the package name and repository/package metadata. `Azure.ResourceManager.*` indicates management-plane; do not assume every other package is data-plane. If the plane is unclear, verify the package metadata or ask the author before selecting a contact.
- For management-plane GA API approval, contact **Arthur Ma (`arthurma`)**. For data-plane GA API approval, contact **Michael Nash (`micnash`)** or **Josh Love (`jolov`)**. When multiple packages are affected, route each to the contact for its plane.
- Include the package/version, APIView revision link, and failed build link in the approval handoff. Explain that the author should request approval from the appropriate contact, then queue another build after approval. These contacts are for GA API approval, not unrelated publishing failures; do not claim approval was requested or granted without evidence.

#### Live API Review Status
- Establish language/package/version from the question or linked evidence; ask for missing values. For .NET, call `azsdk_package_get_approval_status` with `language="dotnet"`, `packageName`, and `packageVersion`. Find the review link using `azsdk_apiview_get_review_url` with `language="dotnet"`, `package`, and the same `version`; cite it with the package/version. Link lookup failure does not invalidate status.
- Use `result.isApproved`, `finalSource`, `reason`, and source status fields, not the SPA, comments (including empty results), or old logs. APIView HTTP 200 means API and package-name approval; 201 means package-name only; 202 means neither. A meaningful `isApproved=false` result is not a lookup failure merely because `response_error` or `operation_status="Failed"` is present.
- ARH is primary, with APIView fallback: `repositoryNotSupported` is not disapproval; honor the final source. Missing `apiHash` leaves the ARH artifact-hash gate unverified, but APIView fallback may still approve. Use only an authoritative artifact hash, never APIView's `activeApiRevisionId`. Package/version approval does not prove exact-revision approval without matching evidence.
- Report query/access failures (`queryFailed`, `invalidResponse`, authentication/authorization errors), absent meaningful results, or unavailable tools as unknown, not unapproved. If another source succeeds, report its result and limits. A missing review is not explicit rejection.
- Read-only: do not request reviews or mutate approvals. Use the contacts above for pending approvals; this API gate alone does not establish whole-release readiness.

### Management-Plane SDK PR Review
For management-plane SDK PRs, including AutoPRs, apply the shared review workflow with these .NET-specific fixes and skills:

- **API naming/review feedback:** explain the requested change and use supported spec or C# customization, following the generation guidance below.
- **ApiCompat/breaking-change feedback:** identify the affected released contract and mitigation using the compatibility guidance below; address explicit reviewer requests about PR-added baselines.

#### Relevant Management SDK Skills
Explain the fix first, then recommend:
- [mgmt-review-comment-resolution](https://github.com/Azure/azure-sdk-for-net/blob/main/.github/skills/mgmt-review-comment-resolution/SKILL.md) for management SDK review comments requesting type/property renames or supported property-type changes.
- [mitigate-breaking-changes](https://github.com/Azure/azure-sdk-for-net/blob/main/.github/skills/mitigate-breaking-changes/SKILL.md) for preserving released contracts when regeneration or migration introduces compatibility breaks.

Link the skill and suggest using it with Copilot in the author's checkout, providing the PR/package, feedback, and spec revision or released baseline. Verify that its documented scope applies, including prerequisites and repository access. Do not recommend either skill for approval-only requests or unrelated CI failures.

#### Verify the SDK Fix Before Re-review
- Remind the author to regenerate the SDK from the spec commit containing those fixes if the spec fix is not merged yet and the SDK does not yet reflect the fixes, so reviewers can verify the updated output. Before requesting re-review, confirm the expected changes appear in the SDK diff and check relevant samples/tests, ApiCompat, and APIView.

### Generation and Spec Review Fixes
- For stale generation/conflicts, sync the SDK branch with its target and verify `tsp-location.yaml`. Updating the SDK/pinned emitter is distinct from updating the spec checkout. Confirm the package/project before suggesting `dotnet build /t:GenerateCode`; for local specs, verify the supported `LocalSpecRepo` invocation and path.
- .NET-only changes do not require other-language releases unless their contracts are affected.
- For migrated projects, use `@azure-typespec/http-client-csharp` (data plane) or `@azure-typespec/http-client-csharp-mgmt` (management), per current repo guidance; do not add deprecated `@azure-tools/typespec-csharp` options for outdated diagnostics.
- For unintended API versions, inspect the correct emitter's `api-version` in `tspconfig.yaml` and verify the output. Coordinate shared pins with the service team and update/remove them when targeting a newer version.

### Breaking Changes and Compatibility
- Compare the previous released GA contract with build/CI ApiCompat results and the .NET APIView diff from PR/check links. Verify that the configured compatibility checks actually ran.
- Passing ApiCompat or unchanged APIView does not prove wire/runtime compatibility. Inspect serialization and test behavior for field casing, wire names, or PATCH changes.
- Prefer mitigation over `eng/apicompatbaselines` suppressions. Follow reviewer requests about PR-added baselines without substituting suppressions or deleting unrelated baselines. Intentional breaks or suppressions need explicit authorized owner approval.

### Generated-Code Defects, Samples, and Tests
- For suspected .NET generated-code defects, inspect the C# output and supported customizations. After changing .NET generation inputs or customizations, rerun the applicable generation workflow and inspect the output before diagnosing why a change did not take effect. Distinguish a supported SDK workaround from an upstream generator fix; do not patch generated files as the lasting solution.
- Update hand-maintained samples to the intended API. Distinguish recording/API-version mismatches from spec/generator defects; do not prescribe rerecording without confirming service behavior and the intended version.
