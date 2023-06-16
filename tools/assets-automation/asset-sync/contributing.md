# Asset Sync Tests

- Using [Ascii-flow](https://asciiflow.com/#/) to generate the art.
- Using [pester](https://pester.dev/docs/quick-start) to test module.

To properly run these tests, an internet connection is required. They are designed to work an external github repository: `Azure/azure-sdk-assets-integration`. This repository stores various test scenario branches that are duplicated as part of this pester integration test suite. If the default git user does not have access to the repo, you will run into problems. To get access, one must be part of the `Azure` organization.

## Using Pester

First, ensure you have `pester` installed:

`Install-Module Pester -Force`

Then invoke tests with:

`Invoke-Pester ./assets.Tests.ps1`

### See stdout output

`Invoke-Pester <other arguments> -PassThru`

### Select a subset

To select a subset of tests, invoke using the `FullNameFilter` argument. EG

`Invoke-Pester ./assets.Tests.ps1 -FullNameFilter="*Evaluate-Target-Dir*"`

The "full-name" is simply the full namespace including parent "context" names.

```powershell
Describe "AssetsModuleTests" {
  Context "Evaluate-Target-Dir" {
    It "Should evaluate a root directory properly." {
    }
  }
}
```

Full evaluated test name is going to be:

`AssetsModuleTests.Evaluate-Target-Dir.Should evaluate a root directory properly.`
