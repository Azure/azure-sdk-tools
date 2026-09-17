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

Both `GA` and the legacy Azure DevOps value `APEX GA` are read as GA; new
release-type writes still use `GA`.

SDK PR eligibility uses current GitHub state, not cached Azure DevOps PR
statuses. Every linked SDK PR must be confirmed closed without merging before
cleanup is allowed, even if its stored status is empty or says it is still
active. Active, merged, or unrecognized GitHub PR states prevent abandonment.
Unknown release types never authorize cleanup. A partial release is protected
even if every associated PR is closed or no PR URL is recorded. Private Preview
eligibility uses GitHub's spec merge state, not API approval.

GitHub lookup errors (including inaccessible private PRs) are reported and the
affected plan is skipped for cleanup. When sending overdue reminders, a failed
activity lookup instead produces a generic reminder to update the target month
or review the dashboard, without asserting an activity state or recommending
abandonment. The lookup error remains visible with a nonzero exit code even if
the generic reminder is sent; email failures are reported separately. Cancellation
does not trigger a fallback email. An unreadable Private Preview API-spec work
item fails the initial scan rather than being treated as a missing spec. Other
per-plan update and reminder failures are reported while the batch continues.
Each abandonment atomically tests the scanned Azure DevOps work-item revision.
If an owner or release automation changes the plan during the scan, the update
is rejected and reported without retrying or sending a confirmation for that plan.
The next scan re-evaluates its current state. Missing revisions also prevent updates.
Cancellation stops processing without waiting for a stalled GitHub read to finish.
Successful abandonment sends a confirmation
email; the subsequent reminder query excludes the newly abandoned plan.

## Preview eligible plans

Run `azsdk release-plan abandon-overdue --dry-run` before live cleanup to list
the plans currently eligible for abandonment. It uses the same overdue query,
calendar-month grace period, release-type rules, live GitHub checks, and valid
snapshot-revision requirement as an actual run. It does not update work items,
change plan statuses, or send notifications.

The preview shows each eligible plan's ID, dashboard link, current status,
target month, and eligibility reason. Add `--output json` for structured output:
`dry_run` is `true`, `release_plans` contains the eligible plans with their
unchanged statuses, and `eligibility_reasons` is keyed by work item ID.
Unlike `list-overdue`, this list excludes plans still in their grace month and
plans protected by the release-work rules.

Lookup failures are reported with a nonzero exit code. If some plans cannot be
evaluated, confirmed candidates are still returned, but the output explicitly
marks the list as incomplete. A successful empty preview means no plans qualify.
The preview is a point-in-time assessment, not a reservation: a later run
re-evaluates eligibility and may skip plans that changed or whose updates fail.

## Operations

- `azsdk release-plan list-overdue` is read-only. Add `--notify-owners true`
  and `--emailer-uri` to send state-specific reminders.
- `azsdk release-plan abandon-overdue --dry-run` previews eligible plans only.
  Omitting `--dry-run` writes the eligible plans' state to
  `Abandoned` and notifies their submitters through `AZSDKTOOLS_NOTIFICATION_SERVICE_URL`.
- Preview requires Azure DevOps read permissions; actual cleanup also requires
  work-item update permissions. Set `GITHUB_TOKEN` with read
  access to the linked repositories, including private spec repositories.
  The pipeline uses the shared GitHub-login template; its service connection
  must be authorized for this pipeline. Email delivery uses the existing emailer secret.
- The pipeline installs the latest released `azsdk` binary. Publish a CLI
  version containing this policy before enabling the monthly schedule.
- Cleanup failures leave the pipeline partially succeeded and do not suppress
  the following reminders. Authentication or installation failures still stop
  processing, and cancellation does not start new reminder work.
- Abandonment confirmation uses the existing best-effort notification service;
  mail delivery errors are logged and do not roll back the state update.

This maintenance policy does not require users to supply an abandonment reason.
