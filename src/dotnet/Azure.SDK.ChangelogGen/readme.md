This is a tool to update changelog.md with generated release notes for Azure SDK for Dotnet. 

In detail, following release notes will be generated automatically:

- The change in the API interface for the release (i.e. Added/Remove/Obsoleted Model/Method/Property)
- The change in the api-version tag used when generating the SDK (i.e. tag: package-preview-2023-03)
- The change in the version of dependencies (i.e. Azure.Core, Azure.ResourceManager)

Usage:

```
> ChangelogGen.exe apiFile
```

>Example: ChangelogGen.exe ...\azure-sdk-for-net\sdk\compute\Azure.ResourceManager.Compute\api\Azure.ResourceManager.Compute.netstandard2.0.cs

Remark:
The generated release notes will be merged into the last release (1.1.0-beta.1 (Unreleased) in most case) in changelog.md. In detail, the generated groups in the release will be overwritten while the other part will be kept untouched.
