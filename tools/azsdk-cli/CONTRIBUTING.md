# Contributing to the `azsdk-cli`

This project is released to a public `NuGet` feed and a `github release`. It does not currently ship to `nuget.org.`

There are a few guidelines that contributors should be aware of:

- [x] The `azsdk-cli` is now a statically versioned project. PR owners should review the [changelog](Azure.Sdk.Tools.Cli/CHANGELOG.md) and make relevant changes to `<VersionPrefix>` attribute of [the cli csproj](Azure.Sdk.Tools.Cli/Azure.Sdk.Tools.Cli.csproj).
- [x] This implies that alongside code changes, devs should update the [CHANGELOG.md](Azure.Sdk.Tools.Cli/CHANGELOG.md) to reflect their changes.
  - Breaking code fixes or features require a `major` version patch bump: `X.0.0`.
  - New features should incur a `minor` version bump: `0.Y.0`.
  - Code fixes only require a `patch` version bump: `0.0.Z`.
  - Follow code review guidance from GitHub team `@azure/azsdk-cli` if uncertain what version change should be made.
- [x] Devs should follow the [README.md](./README.md) and existing tool samples for reference when adding features or fixes.
