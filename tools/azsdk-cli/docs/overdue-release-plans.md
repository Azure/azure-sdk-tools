# Overdue release plan maintenance

These CLI-only commands list overdue release plans, send reminders, and abandon
eligible inactive plans. **Preview with `--dry-run` before running cleanup.**

## Scope and grace period

The scan considers Azure DevOps release plans in `New`, `Not Started`, or
`In Progress` state, excluding plans tagged `Release Planner App Test`.
A plan is overdue when the current UTC month is later than its recorded SDK
target release month. The scan accepts full or abbreviated English month names
(`September 2026` or `Sep 2026`); missing or unparseable target months are excluded.

The first overdue calendar month is for reminders only. Cleanup can begin in
the following month, provided the plan also meets the eligibility rules below:

| Target month   | Reminder-only grace month | Earliest cleanup date |
| -------------- | ------------------------- | --------------------- |
| September 2026 | October 2026              | November 1, 2026      |
| December 2026  | January 2027              | February 1, 2027      |

The grace period is based on the recorded target month, not creation date, last
modification, or the first reminder. Creation age and SDK generation status are
not additional eligibility checks. Review incorrect dates before cleanup; the
scan does not infer or repair them.

## Eligibility and reminder policy

The following rules apply after the scope and date checks above. Reminders are
attempted while overdue; abandonment is allowed only after the grace period.

| Release type and recorded work                                                      | Reminder action                                              | Eligible for abandonment |
| ----------------------------------------------------------------------------------- | ------------------------------------------------------------ | ------------------------ |
| Public Preview / GA: any SDK marked `Released`                                      | Update the target month or complete remaining SDK releases   | No                       |
| Public Preview / GA: no released SDKs and no linked SDK PRs                         | Update the target month or abandon using the Azure SDK Agent | Yes                      |
| Public Preview / GA: no released SDKs and all linked SDK PRs closed without merging | Update the target month or abandon using the Azure SDK Agent | Yes                      |
| Public Preview / GA: any active or merged SDK PR                                    | Update the target month or complete remaining SDK releases   | No                       |
| Private Preview: spec PR missing or unmerged                                        | Merge the spec PR, update the target month, or abandon       | Yes                      |
| Private Preview: spec PR merged                                                     | No incomplete-spec reminder                                  | No                       |
| Unknown release type                                                                | Update the target month or review the dashboard              | No                       |

- SDK release status comes from the release plan. **Any SDK marked `Released`
  protects a Public Preview/GA plan**, even if no PRs are linked or all are closed.
- For Public Preview/GA, linked SDK PR states come from GitHub, not cached Azure
  DevOps fields. All linked SDK PRs must be confirmed closed and unmerged to
  authorize cleanup. Active, merged, or unrecognized SDK PR states protect the plan.
- Private Preview uses the spec PR's merge state, not API approval.
- Both `GA` and the legacy Azure DevOps value `APEX GA` are read as GA. New
  release-type writes continue to use `GA`.

## Commands and output

| Command                                                                    | Behavior                                                                                                               |
| -------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------- |
| `azsdk release-plan list-overdue`                                          | Read-only list of all scoped overdue plans, including protected plans and those still in their grace month. No emails. |
| `azsdk release-plan abandon-overdue --dry-run`                             | Read-only preview of eligible plans using the same checks as cleanup. No updates or emails.                            |
| `azsdk release-plan abandon-overdue`                                       | Marks eligible plans `Abandoned` and attempts confirmation emails. **Performs writes.**                                |
| `azsdk release-plan list-overdue --notify-owners true --emailer-uri <uri>` | Attempts overdue reminders without changing plan states.                                                               |

Preview and actual cleanup show plan IDs, dashboard links, statuses, target
months, and reasons. Preview preserves the current statuses; actual cleanup
returns only plans successfully marked `Abandoned`. Displayed IDs use the
release-plan ID, falling back to the work-item ID when necessary.

Add `--output json` for structured output. In cleanup responses, `release_plans`
contains the results and `eligibility_reasons` is keyed by each plan's
`WorkItemId`, which can differ from its `ReleasePlanId`. Preview also includes
`dry_run: true`. Both text and JSON retain partial results alongside errors.

Dry-run output also reports scanned, eligible, skipped, and evaluation-error
counts, plus skipped-plan IDs, links, target months, and reasons. JSON exposes
these as `preview_summary` and `skipped_plans`. Counts cover the plans returned
by the overdue query, not plans already excluded by its state, tag, or date
filters. Each skipped plan records its **first exclusion reason**; for example,
a plan in its grace month is not checked for additional PR-based exclusions.
Lookup failures and invalid revisions count as evaluation errors, not confirmed
exclusions. Scanned equals eligible plus skipped plus evaluation errors.

A successful empty preview means no scanned plans qualify. It is a point-in-time
assessment, not a reservation: an actual run re-evaluates eligibility, and a plan
can change or its update can fail after the preview.

## Safety and failures

- **Initial scan failure:** failure to retrieve the plans, including an unreadable
  linked Private Preview API-spec work item, stops that command before processing
  plans. Unreadable spec data is not treated as a missing spec PR.
- **GitHub lookup failure:** an invalid, inaccessible, or failed PR lookup skips
  that plan for cleanup. The reminder command instead attempts a generic overdue
  email asking the owner to update the target month or review the dashboard,
  without asserting an activity state or recommending abandonment.
- **Concurrent updates:** both modes require a valid work-item revision. Actual
  cleanup atomically checks that revision when changing the state. A revision
  conflict skips the plan with an error, without retry or confirmation email.
- **Partial failures:** reported lookup, update, or reminder errors produce a
  nonzero exit code while other plans continue. Preview identifies an incomplete
  eligible list; actual cleanup retains only successful state changes. A fallback
  reminder does not hide the lookup error. Confirmation delivery is best effort,
  as described below.
- **Cancellation:** stops further processing and the wait for a stalled GitHub
  read. It does not trigger a fallback email or undo completed state changes.

## Notifications and pipeline operation

Azure DevOps read permissions are required for listing and preview; actual
cleanup also requires work-item update permissions. GitHub credentials must
allow reads of linked repositories, including private specs. The CLI supports
`GITHUB_TOKEN` or an existing GitHub CLI login.

Reminders require `--notify-owners true`, a valid `--emailer-uri`, and a valid
submitter email address. They report lookup and send failures to the caller.
Confirmation emails use `AZSDKTOOLS_NOTIFICATION_SERVICE_URL` and the shared
notification service, which currently accepts only `@microsoft.com` recipients.
A missing URL or unsupported recipient skips confirmation; delivery failures are
logged and do not roll back abandonment. **A successful cleanup exit code does
not guarantee email delivery.**

The [unreleased-SDK pipeline](https://github.com/Azure/azure-sdk-tools/blob/main/eng/pipelines/report-unreleased-sdks.yml)
is configured to run on `main` on the first day of each month at 00:00 UTC:

- Scheduled runs perform cleanup, then query again to remind the remaining
  overdue plans. Newly abandoned plans are excluded from that second query.
- Manual pipeline runs attempt reminders only; **they are not read-only previews**.
- The cleanup step allows errors so reminders can still be attempted. Earlier
  setup failures or pipeline cancellation prevent subsequent steps from running;
  a later reminder failure can still fail the job.
- Authorize the pipeline's service connections and configure its existing
  emailer secret. The pipeline installs the latest released CLI, not the checked-out
  source, so publish a version containing these commands before enabling the schedule.

To continue a release after abandonment, create a new release plan with an
updated SDK target release month. The CLI cannot reopen an abandoned plan.
