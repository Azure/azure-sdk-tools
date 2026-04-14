# APIView — Language Parser Guide

This guide explains how to update and release APIView language parsers. Each language has its own process. For the overall parser architecture, see [overview.md](overview.md).

---

## C# Language Parser

### Step 1 — Verify pipeline execution

Navigate to the [C# API Parser Pipeline](https://dev.azure.com/azure-sdk/internal/_build?definitionId=5789) and confirm a pipeline run with your changes has triggered and completed successfully.

### Step 2 — Identify the updated package

In the completed pipeline run:
1. Navigate to the **Artifacts** section
2. Expand the `packages` folder
3. Look for files starting with `CSharpAPIParser.<version>`
4. Note the version number

### Step 3 — Verify package deployment

Check that your package has been deployed to the [CSharpAPIParser feed](https://dev.azure.com/azure-sdk/internal/_artifacts/feed/CSharpAPIParser). Verify the version matches the artifact from Step 2.

### Step 4 — Update package references

Create a PR to update the package version in these files:

| File | What to Update |
|------|----------------|
| `CSharpLanguageService.cs` | Upgrade version reference |
| `CSharpAPIParser/TreeToken/CodeFileBuilder.cs` | Upgrade version reference |
| `apiview.yml` | Update the `CSharpAPIParserVersion` parameter to match the new version |

> **Example:** See [this pull request](https://github.com/Azure/azure-sdk-tools/pull/example) for reference on the required changes.

### Step 5 — Configure review updates (optional)

To control whether existing reviews are automatically updated with the new parser:

1. Navigate to [APIView App Configuration](https://portal.azure.com/#@microsoft.onmicrosoft.com/resource/subscriptions/a18897a6-7e44-457d-9260-f2854c0aca42/resourceGroups/apiview/providers/Microsoft.AppConfiguration/configurationStores/apiviewkvconfig)
2. Check the `ReviewUpdateDisabledLanguages` setting:
   - **Enable updates:** Remove `"C#"` from the list
   - **Disable updates:** Add `"C#"` to the list

### Step 6 — Deploy

Merge your PR to deploy the updated parser.

### Step 7 — Validate

Confirm that C# reviews are being updated with the new parser:

1. Navigate to [Application Insights](https://ms.portal.azure.com/#@microsoft.onmicrosoft.com/resource/subscriptions/a18897a6-7e44-457d-9260-f2854c0aca42/resourceGroups/apiview/providers/microsoft.insights/components/APIView/logs)
2. Run this query:
   ```kusto
   traces | where message contains "Successfully Updated C# revision with"
   ```
3. If records are returned, C# revisions are being successfully updated.

---

## Rust Language Parser

The Rust APIView parser (`@azure-tools/rust-genapi`) version is managed in three locations:

| File | Property | Description |
|------|----------|-------------|
| `package.json` | `version` | Source of truth for the npm package |
| `apiview.yml` (line 11-13) | `RustAPIParser` default | Controls which version is installed during deployment |
| `RustLanguageService.cs` (line 15) | `VersionString` | Displayed in the APIView UI |

### Update process

1. Deploy the Rust parser code changes (new npm package version)
2. Create a PR to update the version in **all three locations**
3. Keep all three values in sync

> **Example:** See [Update Rust Parser Version (#10571)](https://github.com/Azure/azure-sdk-tools/pull/10571) for reference.

---

## Python Parser Release

### Release process

1. Ensure all PRs are merged in the [apiview-stub-generator](https://github.com/Azure/azure-sdk-tools/tree/main/packages/python-packages/apiview-stub-generator) and version + release date are updated
2. Run the [release pipeline](https://dev.azure.com/azure-sdk/internal/_build?definitionId=5789) manually: **tools - apiview-stub-generator**. Wait for the release to complete before proceeding.
3. Update these two files in `azure-sdk-tools` to pin the new version:

   | File | Purpose |
   |------|---------|
   | [`eng/pipelines/apiview-review-gen-python.yml` (line 23)](https://github.com/Azure/azure-sdk-tools/blob/main/eng/pipelines/apiview-review-gen-python.yml#L23) | Upgrade existing reviews to the new version |
   | [`APIViewWeb/Languages/PythonLanguageService.cs` (line 20)](https://github.com/Azure/azure-sdk-tools/blob/main/src/dotnet/APIView/APIViewWeb/Languages/PythonLanguageService.cs#L20) | Tell APIView the current version so it knows whether to upgrade |

4. Update [`eng/apiview_reqs.txt`](https://github.com/Azure/azure-sdk-for-python/blob/main/eng/apiview_reqs.txt) in the `azure-sdk-for-python` repo to include the new version + pinned dependencies. Copy all requirements from [apiview_reqs.txt in azure-sdk-tools](https://github.com/Azure/azure-sdk-tools/blob/main/packages/python-packages/apiview-stub-generator/apiview_reqs.txt). **Ensure `apiview-stub-generator==x` is included** (not `./.`).
5. Deploy APIView changes — reach out to the APIView Eng Sys team (Dozie, Praveen, or Mariana) or tag them in the APIView channel. After deployment, all APIViews should be updated in 4-5 hours.

### Mirroring packages to dev feed

If you see an error like:

```
ERROR: Could not find a version that satisfies the requirement setuptools_scm>=8 (from versions: none).
```

You need to mirror the missing package to the dev feed:

1. Run the [mirror-packages-to-dev-feed pipeline](https://dev.azure.com/azure-sdk/internal/_build?definitionId=5789) from branch `resolve-mirror-job`
2. Add all package requirements to **"PackageSpecifiers"** (e.g., `setuptools_scm>=8`)
3. Verify the package appears in [azure-sdk-for-python artifacts](https://dev.azure.com/azure-sdk/public/_artifacts/feed/azure-sdk-for-python) with the correct version
