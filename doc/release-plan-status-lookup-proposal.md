# Release-plan status updates by ID

Related: [#16844](https://github.com/Azure/azure-sdk-tools/issues/16844), [#17119](https://github.com/Azure/azure-sdk-tools/pull/17119), and [ID exposure / pipeline rollout](https://github.com/Azure/azure-sdk-tools/issues/17130).

## Revised direction

Use the **release-plan ID** as the primary identifier. Manual and automatic releases obtain it differently. This replaces the earlier API-version-based proposal; API-version extraction in [#16868](https://github.com/Azure/azure-sdk-tools/issues/16868) is not a dependency.

```text
Manual: requester supplies ID --> azsdk release / release pipeline --> plan by ID
Auto:   triggering SDK PR --> existing in-progress ADO plan --> plan ID
Both:   validate language/package entry --> update release information
```

## Manual releases

- Add optional `releasePlanId` to `azsdk_release_sdk` and `--release-plan-id` to `azsdk package release`.
- The requester supplies the ID directly; forward it as the pipeline's `ReleasePlanId` input.
- No supplied ID means no release-plan update. Do not infer a plan from the package name.
- A supplied ID must identify exactly one eligible plan. Do not reinterpret it as an ADO work item ID or substitute another plan.

## Automatic releases

- Use the SDK PR that triggered the merge pipeline, not the latest PR touching the package.
- Find in-progress release plans whose language-specific SDK PR link matches that exact PR.
- Exactly one match: use its release-plan ID and validate the language/package entry.
- No match: skip the plan update. Multiple matches: report ambiguity and make no updates; do not apply tie-breakers.
- Reuse existing ADO release-plan metadata. No separate GitHub project, new tracking database, or permanent plan ID in SDK source.

## Update rules

| Result | Action |
| --- | --- |
| Valid manual ID or one automatic SDK-PR match | Update only the identified SDK entry |
| No manual ID or no automatic match | No plan update |
| Invalid/duplicate ID, multiple PR matches, or conflicting language/package | Report the problem; no update or fallback |

- Language and package name select/validate the SDK entry within the identified plan; they are not substitute plan-lookup keys.
- **Do not pass or match spec API version.** Preserve existing package version/version ID, release pipeline link, and completion reporting.
- Preserve retry/conflicting-release protection and guarded writes. Finish only after all required SDKs are released or have approved exclusions.
- A status-correlation problem does not undo a successful package publication; surface it for investigation.

## Template rollout and validation

```text
Expose CLI input --> validate ONE template --> client/management CI rollout
```

- Declare and forward `ReleasePlanId` through CI entry points and release templates. Start with one template package; do not bulk-change pipelines before pilot approval.
- Confirm the required PR trigger-control tag before broad YAML changes. A `[skip ci]` commit marker does not by itself guarantee PR validation is skipped.
- Test manual IDs, omitted IDs, exact automatic PR matches, no/multiple matches, multiple packages/languages, and unchanged release metadata. Use mocked status writes before a controlled template-pipeline validation.
- [#17130](https://github.com/Azure/azure-sdk-tools/issues/17130) tracks ID exposure and pipeline rollout; #17119 must align its receiver with this revised flow before rollout.
