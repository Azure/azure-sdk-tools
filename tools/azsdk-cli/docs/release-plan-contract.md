# Release-plan public responses and abandonment

## Why these changes

- [#15463](https://github.com/Azure/azure-sdk-tools/issues/15463): dashboard links and skill guidance already exist, but MCP JSON exposes the entire backing work-item model. Prompt-only instructions do not prevent agents from suggesting the internal URL or confusing its ID with the public plan ID.
- [#15340](https://github.com/Azure/azure-sdk-tools/issues/15340): confirmation is not authorization. The abandon handler previously allowed any caller with work-item edit access. The later issue comment specifies admin-only; this draft implements that policy, not creator-or-admin.

## Public response contract

`release_plan_details` and `release_plans` contain an explicit public projection. Public field names such as `ReleasePlanId`, `ReleasePlanLink`, `Status`, and `SDKInfo` are preserved. Backing work-item IDs/URLs, parent/API-spec work-item IDs, tags/descriptions, and notification-recipient metadata are omitted. Legitimate SDK PR and generation-pipeline links are retained.

The original C# work-item objects remain available internally and are not globally changed. This is a deliberate JSON contract change: consumers that read the removed storage fields must use the public ID/link. Existing tool parameter names and legacy work-item-ID inputs remain supported. Plans without a separate plan number use the same fallback ID as the dashboard.

This is not a generic redactor for arbitrary user-authored text or upstream exception messages. Logs and existing access-error diagnostics remain available for troubleshooting. New SDK PR descriptions link the dashboard rather than appending a backing work-item URL; existing PR descriptions are not rewritten.

## Administrator policy

An administrator is a member of the Azure DevOps **Release** project's built-in **Project Administrators** group, including memberships expanded by ADO. This group was selected for the draft because it can be checked using the same identity as the work-item write, without introducing a separate GitHub login or matching identities by email.

The implementation:

1. Gets the authenticated identity from the work-item `VssConnection`, including its existing sign-in fallback.
2. Resolves the Release project and its built-in administrator group by project ID, not by a user-supplied display name or local allowlist.
3. Reads the caller's expanded parent memberships and compares identity descriptors.
4. Rejects non-members and fails closed if identity, group, or membership data is missing, ambiguous, inactive, or unavailable. Cancellation propagates without writing.
5. Rechecks before every abandon operation. A plan's submitter, PR author, or notification recipient never grants access.

Get responses expose advisory `capabilities.can_abandon` and `capabilities.reason`. Failure to check permissions does not prevent reading the plan. A denial tells the user to contact a release-plan administrator, not to edit ADO directly. User confirmation is still required; capabilities do not replace confirmation or the mutation-time check.

## Trust boundary and rollout

The CLI/MCP check protects the supported agent/command path. It does **not** change ADO ACLs or prevent a user who already has direct work-item edit access from calling ADO independently, using an old binary, or modifying this open-source client. If admin-only abandonment must be enforced for every entry point, configure an ADO process/state-transition rule or place the mutation behind an authorized service and restrict direct writes. Confirm that enforcement with the ADO owners before treating this as organization-wide access control.

Review the group's membership and intended service-identity access before release. This draft does not grant permissions, change group membership, or use an environment-variable bypass. The dashboard PM-view allowlist remains presentation-only and is not an authorization source. Scheduled cleanup in [PR #16969](https://github.com/Azure/azure-sdk-tools/pull/16969) is a separate proposal; its service identity and write path need their own reviewed policy rather than silently inheriting interactive user permissions.

Dashboard write buttons, abandonment requests/approval workflows, creator identity backfills, reason capture, and transactional duplicate prevention are outside this draft. No existing plans or PR descriptions need migration for the public-ID response change.

## Validation

- Serialization tests cover single/list responses, preserved SDK links, legacy IDs, and unchanged serialization of other work-item types.
- Authorization tests cover members/non-members, other-project admins, missing/ambiguous/inactive identities, failed lookups, cancellation, and permission revocation between get and abandon.
- Hermetic skill evals cover public links, denied capabilities, submitter/consent not bypassing denial, and confirmed admin abandonment.
- Live validation should be read-only (identity and membership lookup). Do not abandon a production plan to validate access.