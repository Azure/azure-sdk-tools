# Isolated past-due verification

Diagnostic branch for [#17192](https://github.com/Azure/azure-sdk-tools/issues/17192), based on the exact azsdk 0.6.51 release commit. It does not change production source or the existing notification pipeline.

- Default: render shipped reminder/confirmation templates using synthetic data and exercise the real notification service with a fake HTTP boundary.
- Pipeline: install published CLI 0.6.51 and run only `abandon-overdue --dry-run` with the existing read identity. Save raw output in a private artifact.
- Explicit `sendTestEmails: true`: send exactly two clearly marked synthetic emails to **gaoh@microsoft.com** only, with no CCs. No automatic retries. Inbox receipt still requires confirmation from the recipient.
- Missing secret/resource authorization fails rather than changing permissions. Secrets and endpoint exceptions are not written to evidence.
- No schedules, work-item writes, production owner emails, or changes to pipeline 8135. Test-tag exclusions remain unchanged.

The synthetic confirmation tests delivery/rendering only; it does not prove a real abandonment occurred. Approval and resource checks on the isolated pipeline must remain in place. Do not blindly retry a send after failure: check saved per-message acceptance first.