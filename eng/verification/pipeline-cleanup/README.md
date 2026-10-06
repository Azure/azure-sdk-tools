# Pipeline-identity single-plan cleanup

Diagnostic continuation for [#17192](https://github.com/Azure/azure-sdk-tools/issues/17192), built from the exact published azsdk 0.6.52 source. This branch does not alter production CLI code or production pipeline 8135.

- Fresh full preview uses the published binary and `--dry-run` only.
- The scoped released-handler test can select only **plan 2006 / work item 29426**. Every other service method, ID, or field update is blocked.
- `applySinglePlan` defaults to false. The explicit authorized test performs at most one `System.State = Abandoned` update with the real atomic `/rev` guard, verifies persistence, captures confirmation without sending, and verifies a no-op rerun.
- Same `opensource-api-connection` as production. No emailer secret, work-item creation, notification recipient, retry, or schedule.
- Evidence is private; missing permissions must fail rather than changing resource policies. Inspect the original result before any second execution.