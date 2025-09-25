# Contributing to the `azsdk-cli`

This project is released to a public `nuget` feed and a `github release`. It does not currently ship to `nuget.org.`

There are few guidelines that contributors should be aware of:

- [x] Alongside code changes, devs should update the [CHANGELOG.md](Azure.Sdk.Tools.Cli/CHANGELOG.md) to reflect their changes.
  - New features should incur a `minor` version bump: `0.X.0`.
  - Code fixes only require a `patch` version bump: `0.0.Y`.
  - Follow code review guidance from github team `@azure/azsdk-cli` if uncertain what version change should be made.
- [x] Devs should follow the [README.md](./README.md) and existing tool samples for reference when adding features or fixes.
