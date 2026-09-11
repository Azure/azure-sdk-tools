# Overdue release plan maintenance

The [unreleased-SDK pipeline](https://github.com/Azure/azure-sdk-tools/blob/main/eng/pipelines/report-unreleased-sdks.yml)
runs on the first day of each month at 00:00 UTC. It applies cleanup first,
then sends reminders for the remaining overdue plans. Manual pipeline runs
send reminders only; the CLI cleanup command itself performs writes when invoked.

## Calendar-month grace period

A plan is overdue when the current UTC month is later than its target release
month. Both `MMMM yyyy` and `MMM yyyy` are accepted.

The first overdue month is a warning-only grace period. For a September 2026
target, October 1 sends a reminder; November 1 is the first cleanup-eligible
scan. Updating the target month moves that boundary. The policy applies to
active plans (`New`, `Not Started`, and `In Progress`), not completed, closed,
duplicate, abandoned, or test-tagged plans.

## Policy

| Release type and work state                             | Reminder when overdue                                                | Auto-abandon after the grace period |
| ------------------------------------------------------- | -------------------------------------------------------------------- | ----------------------------------- |
| Public Preview / GA: no SDK PRs                         | Update the target month or abandon using the Azure SDK Agent         | Yes                                 |
| Public Preview / GA: all SDK PRs closed without merging | Update the target month or abandon using the Azure SDK Agent         | Yes                                 |
| Public Preview / GA: any active or merged SDK PR        | Update the target month or complete remaining SDK release activities | No                                  |
| Public Preview / GA: any SDK already released           | Update the target month or complete remaining SDK release activities | No                                  |
| Private Preview: spec PR missing or unmerged            | Merge the spec PR, update the target month, or abandon               | Yes                                 |
| Private Preview: spec PR merged                         | No incomplete-spec reminder                                          | No                                  |

SDK PR status must be explicitly `Closed` before cleanup checks its current
GitHub state. Reopened or merged PRs prevent abandonment. Unknown release
types and unknown SDK PR statuses never authorize cleanup. A partial release
is protected even if every associated PR is closed or no PR URL is recorded.
Private Preview eligibility uses GitHub's spec merge state, not API approval.

GitHub lookup errors (including inaccessible private PRs) are reported and the
affected plan is skipped. An unreadable Private Preview API-spec work item
fails the initial scan rather than being treated as a missing spec. Other
per-plan update and reminder failures are reported while the batch continues.
Cancellation stops processing. Successful abandonment sends a confirmation
email; the subsequent reminder query excludes the newly abandoned plan.

## Operations

- `azsdk release-plan list-overdue` is read-only. Add `--notify-owners true`
  and `--emailer-uri` to send state-specific reminders.
- `azsdk release-plan abandon-overdue` writes the eligible plans' state to
  `Abandoned` and notifies their submitters through `AZSDKTOOLS_NOTIFICATION_SERVICE_URL`.
- Azure DevOps work-item permissions are required. Set `GITHUB_TOKEN` with read
  access to the linked repositories, including private spec repositories.
  The pipeline uses the shared GitHub-login template; its service connection
  must be authorized for this pipeline. Email delivery uses the existing emailer secret.
- The pipeline installs the latest released `azsdk` binary. Publish a CLI
  version containing this policy before enabling the monthly schedule.
- Abandonment confirmation uses the existing best-effort notification service;
  mail delivery errors are logged and do not roll back the state update.

This maintenance policy does not require users to supply an abandonment reason.
