# About common-tests

Every PowerShell source that is complex enough to have unit tests should have these tests be written in the [Pester](https://pester.dev/)
framework and placed in `eng/common-tests/**`, i.e. this directory or one of its descendants.  
By design, unlike `eng/common`, `eng/common-tests` is not subject to
[mirroring to repositories](https://github.com/Azure/azure-sdk-tools/blob/main/doc/common/common_engsys.md).

## When tests in this directory are executed

The PowerShell Pester unit tests will be executed by the public
[`tools - eng-common-tests`](https://dev.azure.com/azure-sdk/public/_build?definitionId=5985&_a=summary) pipeline upon a PR that makes changes
to `eng/common/scripts/**` or  `eng/common-tests/**`. The pipeline source is `eng/common-tests/ci.yml`.

In addition, specific tools might selectively run a subset of tests during their pipeline run, as e.g. done by `tools/code-owners-parser/ci.yml`.

## How to ensure your PowerShell pester unit test is executed

By Pester's default convention, the tests are to be placed in files whose
[names ends with `.tests.ps1`](https://pester.dev/docs/usage/file-placement-and-naming),
e.g. [`job-matrix-functions.tests.ps1`](https://github.com/Azure/azure-sdk-tools/blob/8a02e02adfc0d213509fce2764132afa74bd4ba4/eng/common-tests/matrix-generator/tests/job-matrix-functions.tests.ps1).

Furthermore, each test needs to be tagged with `UnitTest`, [e.g. as seen here](https://github.com/Azure/azure-sdk-tools/blob/8a02e02adfc0d213509fce2764132afa74bd4ba4/eng/common-tests/matrix-generator/tests/job-matrix-functions.tests.ps1#L51): `Describe "Matrix-Lookup" -Tag "UnitTest", "lookup"`.

Finally, as already mentioned, all such test files must be located in the path of `eng/common-tests/**`.

For more, see [Invoke-Pester doc](https://pester.dev/docs/commands/Invoke-Pester).