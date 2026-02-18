# CONTRIBUTING GUIDE

## Release api view parser package

After making changes to the JS api view parser code, we need to release the `@azure-tools/ts-genapi` package. Steps to release:

1. Make sure package.json has the updated version that you want to release.
2. Changelog.md should be updated with the version and changes to be released.
3. Make sure the version getting exported on [this line is correct](https://github.com/Azure/azure-sdk-tools/blob/main/tools/apiview/parsers/js-api-parser/src/export.ts#L64)
4. Run the internal release pipeline on Azure Devops [`tools/tools - js - apiview-parser`](https://dev.azure.com/azure-sdk/internal/_build?definitionId=5149&_a=summary). Manually approve the release once the build succeeds.
5. Make sure the public devops feed is updated with the package version you just released - [`azure-sdk-for-js - Azure Artifacts`](https://dev.azure.com/azure-sdk/public/_artifacts/feed/azure-sdk-for-js). Search for @azure-tools/ts-genapi" and verify the package version released.

## Update package references

After releasing the package `@azure-tools/ts-genapi` to the public devops feed, please make sure the pinned package references are updated:

1. Version that gets picked up in [APIView pipeline](https://github.com/Azure/azure-sdk-tools/blob/main/src/dotnet/APIView/apiview.yml#L10) and [the language specific parser](https://github.com/Azure/azure-sdk-tools/blob/main/src/dotnet/APIView/APIViewWeb/Languages/JavaScriptLanguageService.cs#L15). [Here's an example PR](https://github.com/Azure/azure-sdk-tools/pull/13953)
2. Pinned version in [eng/scripts/Generate-APIView-CodeFile.ps1 in azure-sdk-for-js repo](https://github.com/Azure/azure-sdk-for-js/blob/main/eng/scripts/Generate-APIView-CodeFile.ps1#L15) .[Here's an example PR](https://github.com/Azure/azure-sdk-for-js/pull/37284)
3. Pinned version in [eng/scripts/Generate-APIView-CodeFile.ps1 in typespec repo](https://github.com/microsoft/typespec/blob/main/eng/emitters/scripts/Generate-APIView-CodeFile.ps1#L14). [Here's an example PR](https://github.com/microsoft/typespec/pull/9661)

## Other changes

In the production environment of the API tool parser, `JavaScriptParserToolPath` is the build variable that contains path where the js parser package gets installed and gets consumed from.