# Common engineering release history

## Unreleased

### Added

- Added the on-demand service API-version readiness pipeline with a required service-connection input and a bounded PR comment.

### Bugs Fixed

- Pass manual release-plan IDs to PowerShell through the task environment so input is validated as data rather than interpreted as script text.
- Release-plan completion uses the job's `AutoReleaseSdkPullRequestUrl` when no explicit `SdkPullRequest` is supplied. An absent or empty variable is initialized to an empty string at runtime before macro expansion, so manual runs do not pass an unresolved variable. Empty initialization is logged. Existing runtime values and explicit parameters are preserved.
