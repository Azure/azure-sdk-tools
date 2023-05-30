This is a tool to update changelog.md with generated release notes for Azure SDK for Dotnet. 

In detail, following release notes will be generated automatically:

- Obsoleted API in the stable release (i.e. Method/Property/Model/...)
- The change in the api-version tag used when generating the SDK (i.e. tag: package-preview-2023-03)
- The change in the version of dependencies (i.e. Azure.Core, Azure.ResourceManager)

#### Usage:
```
> ChangelogGen.exe apiFile version releaseDate(xxxx-xx-xx)
```

#### Example:
```
> ChangelogGen.exe ...\azure-sdk-for-net\sdk\compute\Azure.ResourceManager.Compute\api\Azure.ResourceManager.Compute.netstandard2.0.cs 1.2.3 2099-02-03
```

#### Remark:
- For preview release, the previous release will be used as baseline to detect changes.
- For GA release, the previous GA release will be used as baseline to detect changes.
- The generated release notes will be merged into the last release (1.1.0-beta.1 (Unreleased)) in changelog.md.
- Only Management Plane is supported now