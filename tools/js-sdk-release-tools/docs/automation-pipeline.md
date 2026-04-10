# JS SDK Automation Pipeline

This document describes how the JS SDK release automation pipeline works, covering the entry point, SDK type and run mode determination, folder cleanup logic per mode, and detailed generation steps for each SDK type.


## Table of Contents

1. [Overall Architecture](#1-overall-architecture)
2. [Folder Cleanup Logic](#2-folder-cleanup-logic)
3. [HighLevelClient (HLC) — Management Plane SDK](#3-highlevelclient-hlc--management-plane-sdk)
4. [RestLevelClient (RLC) — REST Level Client](#4-restlevelclient-rlc--rest-level-client)
5. [ModularClient (MLC) — Modular Client](#5-modularclient-mlc--modular-client)
6. [Changelog & Version Bump (Common)](#6-changelog--version-bump-common)
7. [Utility Operations Summary](#7-utility-operations-summary)
8. [Output JSON Structure](#8-output-json-structure)
9. [Overall Flow Diagram](#9-overall-flow-diagram)

---

## 1. Overall Architecture

### CLI Entry Points

The package exposes the following CLI commands (defined in `package.json` `bin`):

#### AutoPR / Release Pipeline

| Command | Parameters | Description |
|---|---|---|
| `code-gen-pipeline` | `--inputJsonPath`, `--outputJsonPath`, `--use`, `--typespecEmitter`, `--sdkGenerationType`, `--local` | Main automation entry point; used by the AutoPR release pipeline to generate and package SDK code end-to-end |
| `hlc-code-gen-for-pipeline` | *(same as above)* | Alias for `code-gen-pipeline` (legacy HLC-specific name) |

#### Dev Loop Experience

| Command | Parameters | Description |
|---|---|---|
| `update-changelog` | `--sdkRepoPath`, `--packagePath` | Regenerates `CHANGELOG.md` only (does not bump version) |
| `update-version` | `--sdkRepoPath`, `--packagePath`, `--releaseType`, `--version`, `--releaseDate` | Updates `package.json` version only (does not rewrite changelog) |
| `generate-ci-yaml` | `--sdkRepoPath`, `--packagePath` | Creates or updates the `ci.yml` / `ci.mgmt.yml` file for a package |

These commands are also invoked by the SDK automation system via [`eng/swagger_to_sdk_config.json`](https://github.com/Azure/azure-sdk-for-js/pull/37749/changes#diff-317766c39b2f66ac2343b9830dd5d2041792cdd520eceb1df4772175e058aa84) in `azure-sdk-for-js` ([Dev Inner Loop project board](https://github.com/orgs/Azure/projects/865)), which maps them to shell scripts so that the spec-to-SDK tooling can invoke them without knowing the specific package manager or tool path:

| Config key | Script path (in azure-sdk-for-js) | Underlying CLI command | Purpose |
|---|---|---|---|
| `updateVersionScript` | `eng/scripts/update-version.ps1` | `update-version` | Bumps `package.json` version and writes changelog for a package |
| `updateMetadataScript` | `eng/scripts/update-metadata.ps1` | `generate-ci-yaml` | Creates or updates `ci.yml` / `ci.mgmt.yml` for a package |

The `update-metadata.ps1` script (added in [azure-sdk-for-js #37749](https://github.com/Azure/azure-sdk-for-js/pull/37749)) installs `js-sdk-release-tools` from the local `eng/tools/js-sdk-release-tools` path, then runs:

```
generate-ci-yaml --sdkRepoPath <SdkRepoPath> --packagePath <PackagePath>
```

This allows SDK authors to regenerate the CI YAML for their package locally as part of the inner loop without running the full automation pipeline. See [azure-sdk-for-js #37458](https://github.com/Azure/azure-sdk-for-js/pull/37458) for the corresponding version bump that ships these changes.

#### Local Code Generation

| Command | Parameters | Description |
|---|---|---|
| `hlc-code-gen` | see [hlc.md](./hlc.md) | Local HLC (management-plane) code generation from swagger/README |
| `rlc-code-gen` | see [llc.md](./llc.md) | Local RLC (data-plane) code generation |
| `changelog-tool` | see [changelog-tool.md](./changelog-tool.md) | Generate changelog by comparing api.md against published npm package |

### SDK Type Enum (`SDKType`)

| Type | Value | Description |
|---|---|---|
| `HighLevelClient` | `'HighLevelClient'` | HLC — Management plane SDK (autorest-based, corresponds to `@azure/arm-*`) |
| `RestLevelClient` | `'RestLevelClient'` | RLC — REST level client (autorest or TypeSpec based) |
| `ModularClient` | `'ModularClient'` | MLC — Modular client (TypeSpec-based, typically management plane) |

### Run Mode (`RunMode`)

| Mode | Value | Description |
|---|---|---|
| `Release` | `'release'` | Official production release pipeline |
| `Local` | `'local'` | Developer local run (skips node_modules backup) |
| `SpecPullRequest` | `'spec-pull-request'` | Triggered by spec PR for automated validation |
| `Batch` | `'batch'` | Bulk SDK generation across multiple packages |

---

### Overall Flow Diagram

```
CLI Entry (autoGenerateInPipeline.ts)
  │
  ├── Parse inputJson → parseInputJson()
  ├── Determine SDKType
  ├── backupNodeModules()  (non-local mode only)
  │
  ├── switch(SDKType)
  │   │
  │   ├── HighLevelClient ──→ generateMgmt()
  │   │   ├── autorest code generation
  │   │   ├── Find changed packages (git diff)
  │   │   ├── Update rush.json / ci.yml / _meta.json
  │   │   ├── rush update → rush build → changelog → rush pack
  │   │   └── Update snippets / README
  │   │
  │   ├── RestLevelClient ──→ generateRLCInPipeline()
  │   │   ├── TypeSpec → tsp-client init or tsp compile
  │   │   │   OR
  │   │   ├── Swagger → autorest code generation
  │   │   ├── Update rush.json / ci.yml
  │   │   ├── install → customize → lint → build → pack
  │   │   ├── format → snippets → changelog
  │   │   └── Output artifacts & apiView
  │   │
  │   └── ModularClient ──→ generateAzureSDKPackage()
  │       ├── CODEOWNERS & ignore-links
  │       ├── tsp-client init code generation
  │       ├── buildPackage:
  │       │   ├── rush/pnpm update
  │       │   ├── lint fix (Release/Local)
  │       │   ├── customize (Data Plane)
  │       │   ├── turbo build
  │       │   ├── extract ApiView info
  │       │   ├── test package
  │       │   ├── format
  │       │   └── update snippets
  │       ├── changelog & bump version (Management Plane only)
  │       ├── tryBuildSamples
  │       ├── createArtifact (pack → .tgz)
  │       └── createOrUpdateCiYaml
  │
  ├── restoreNodeModules()  (non-local mode only)
  └── Write outputJson
```

---

## 2. Folder Cleanup Logic

The cleanup behavior is determined by **SDK type** and **run mode**. The core function is `cleanUpPackageDirectory()`.

Two run mode categories are used internally:

- **Pipeline modes** (`Release` or `Local`): Perform targeted cleanup to preserve manually authored files.
- **Automation modes** (`SpecPullRequest` or `Batch`): Perform a full cleanup to ensure a pristine environment.

### 2.1 HighLevelClient (HLC) — Management Plane

HLC packages correspond to `@azure/arm-*` and are generated by the `generateMgmt()` entry point using autorest.

| Run Mode | Cleanup Behavior | Details |
|---|---|---|
| `Release` / `Local` | **No explicit cleanup** | Autorest overwrites generated files in-place. No directory-level cleanup is performed by the tool. |
| `SpecPullRequest` / `Batch` | **No explicit cleanup** | Same — autorest generation overwrites files. |

> **Note**: The `generateMgmt()` function does NOT call `cleanUpPackageDirectory()`. The HLC cleanup logic inside `cleanUpPackageDirectory()` (partial cleanup preserving `test/` and `assets.json`) is only triggered from the MLC/RLC code paths when the target package is identified as an existing HLC package (i.e., converting from HLC to MLC).

### 2.2 RestLevelClient (RLC) — Data Plane REST Client

RLC packages are identified as `"sdk-type": "client"` without modular markers.

| Run Mode | Cleanup Behavior | Details |
|---|---|---|
| `Release` / `Local` | **Skip cleanup** | The TypeSpec emitter (or autorest) handles regenerating only the generated source files (e.g., `src/generated/`, `generated/`, or `src/`). The tool does not delete any files beforehand. |
| `SpecPullRequest` / `Batch` | **Full cleanup** | Removes the entire package directory and recreates it empty. |

### 2.3 ModularClient (MLC) — Modular Client

MLC packages are identified by `is-modular-library: true` in `tspconfig.yaml`.

**Cleanup sub-types within MLC:**

#### Management Plane MLC — Converting from HLC (existing HLC `package.json` found)

When a management plane package previously generated via autorest (HLC) is being regenerated as a ModularClient, the old package directory retains HLC markers in `package.json`. Cleanup is based on run mode:

| Run Mode | Cleanup Behavior | Details |
|---|---|---|
| `Release` / `Local` | **Partial cleanup** | Preserves `test/` and `assets.json`. All other generated files, including `src/`, are removed and regenerated. |
| `SpecPullRequest` / `Batch` | **Full cleanup** | Removes the entire package directory. |

#### Management Plane MLC — New or existing ModularClient package

When generating a brand-new package (no directory yet), or regenerating a package that is already a `ModularClient` in its `package.json`:

| Run Mode | Cleanup Behavior | Details |
|---|---|---|
| `Release` / `Local` | **Skip cleanup** | The emitter handles file-level regeneration. No directory-level cleanup is performed. |
| `SpecPullRequest` / `Batch` | **Skip cleanup** | Same — emitter handles it. No directory-level cleanup. |

> **Note**: If the package directory does not exist yet, `cleanUpPackageDirectory()` returns immediately without any action.

#### Data Plane MLC

| Run Mode | Cleanup Behavior | Details |
|---|---|---|
| `Release` / `Local` | **Skip cleanup** | Same as RLC: emitter handles src-level regeneration. |
| `SpecPullRequest` / `Batch` | **Full cleanup** | Removes the entire package directory. |

### 2.4 Summary Table

| SDK Type | Plane | Source State | `Release` / `Local` | `SpecPullRequest` / `Batch` |
|---|---|---|---|---|
| `HighLevelClient` | Management | N/A | No cleanup (autorest overwrites files in-place) | No cleanup (autorest overwrites files in-place) |
| `RestLevelClient` | Data | N/A | Skip (emitter cleans `src/`) | Full cleanup |
| `ModularClient` | Management | Converting from HLC | Partial: keep `test/`, `assets.json` | Full cleanup |
| `ModularClient` | Management | New or already MLC | Skip (emitter handles) | Skip (emitter handles) |
| `ModularClient` | Data | N/A | Skip (emitter cleans `src/`) | Full cleanup |

---

## 3. HighLevelClient (HLC) — Management Plane SDK

### Processing Steps

| Step | Required | Operation | Command / Details |
|---|---|---|---|
| **1. Code Generation** | ✅ Required (unless `skipGeneration`) | Run autorest to generate code | `autorest --version=3.9.7 --typescript --modelerfour.lenient-model-deduplication --azure-arm --head-as-boolean=true --license-header=MICROSOFT_MIT_NO_VERSION --generate-test --typescript-sdks-folder={sdkRepo} {readmeMd}` + optional `--tag=package-{apiVersion}` `--use={use}` |
| **2. Find Changed Packages** | ✅ Required | `getChangedPackageDirectory()` | Uses `git diff` to find changed package directories after generation |
| **3. Update `rush.json`** | ✅ Required (rush repo) | `changeRushJson()` | Add package entry to `rush.json` |
| **4. Modify Test/Sample Config** | ✅ Required | `changeConfigOfTestAndSample()` | Modify `tsconfig.json` to skip compiling `test/` and `sample/` directories |
| **5. Write `_meta.json`** | ✅ Required (non-skipGeneration) | Write code generation metadata | Contains `commit`, `readme`, `autorest_command`, `repository_url`, `release_tool`, etc. |
| **6. Generate/Modify CI YAML** | ✅ Required (non-skipGeneration) | `modifyOrGenerateCiYml()` | Create or update `ci.mgmt.yml` |
| **7. Install Dependencies** | ✅ Required | Rush or pnpm | `node common/scripts/install-run-rush.js update` (rush repo) or `pnpm install` (pnpm repo) |
| **8. Lint Fix** | ⚠️ Optional | `lintFix()` — only in `Release` / `Local` mode (pnpm repo) | First builds `@azure/eslint-plugin-azure-sdk`, then `dev-tool run vendored eslint ... --fix` |
| **9. Build** | ✅ Required | Compile package (excluding test/sample) | `node common/scripts/install-run-rush.js build -t {packageName}` (rush) or `pnpm build --filter {packageName}...` (pnpm) |
| **10. Generate Changelog & Bump Version** | ✅ Required (non-skipGeneration) | `generateChangelogAndBumpVersion()` | Compare `api.md` between npm published version and local; detect breaking changes; generate changelog; bump version |
| **11. Pack** | ✅ Required | Generate `.tgz` package | `node common/scripts/install-run-rush.js pack --to {packageName} --verbose` (rush) or `pnpm run --filter {packageName}... pack` (pnpm) |
| **12. Update Snippets** | ✅ Required | `updateSnippets()` | `dev-tool run update-snippets` |
| **13. Modify README** | ✅ Required (non-skipGeneration) | `changeReadmeMd()` | Update package `README.md` |
| **14. Add ApiView Info** | ✅ Required | `addApiViewInfo()` | Find `temp/**/*.api.json` file path and add to `outputJson` |
| **15. Restore Config** | ✅ Required (non-skipGeneration) | `changeConfigOfTestAndSample(Revert)` | Restore original `tsconfig.json` configuration |

---

## 4. RestLevelClient (RLC) — REST Level Client

There are two generation paths based on the source: **TypeSpec project** or **Swagger/README project**.

### Path A: TypeSpec Project (`options.typespecProject` exists)

| Step | Required | Operation | Command / Details |
|---|---|---|---|
| **1. Get Target Package Dir** | ✅ Required | `getGeneratedPackageDirectory()` | Parse `emitter-output-dir` / `service-dir` + `package-dir` from `tspconfig.yaml` |
| **2. Clean Up Package Dir** | ✅ Required | `cleanUpPackageDirectory()` | Cleanup strategy based on `runMode` and SDK type (see [Section 2](#2-folder-cleanup-logic)) |
| **3a. Code Gen (command mode)** | ✅ Required | TypeSpec direct compile | ① Copy `emitter-package.json` → ② `npm install` → ③ Update `tspconfig.yaml` → ④ `npx tsp compile {source} --emit @azure-tools/typespec-ts --arg "js-sdk-folder={sdkRepo}"` |
| **3b. Code Gen (script mode)** | ✅ Required | tsp-client generation | `npm --prefix eng/common/tsp-client exec -- tsp-client init --update-if-exists --debug --tsp-config {tspconfig.yaml} --local-spec-repo {tspDefDir} --repo {repoUrl} --commit {commitId}` |

### Path B: Swagger Project (no `typespecProject`)

| Step | Required | Operation | Command / Details |
|---|---|---|---|
| **1. Find Autorest Config** | ✅ Required | Search SDK repo for existing autorest config | Scans `sdk/{RP}/{package}-rest/swagger/README.md` files looking for a matching `require` URL or `input-file` path that references the incoming spec. The PR-comment-based config generation path was removed as a security fix ([#14743](https://github.com/Azure/azure-sdk-tools/pull/14743)). |
| **2. Code Generation** | ✅ Required | Run autorest | `autorest --version=3.9.7 {README.md} --output-folder={packagePath}` + optional `--use` `--multi-client=true` `--tag=package-{apiVersion}` |

### Common Post-generation Steps (both paths)

| Step | Required | Operation | Command / Details |
|---|---|---|---|
| **4. Generate/Modify CI YAML** | ✅ Required | `modifyOrGenerateCiYml()` | Create or update `ci.yml` |
| **5. Modify Test/Sample Config** | ✅ Required | `changeConfigOfTestAndSample()` | Skip test/sample compilation |
| **6. Install Dependencies** | ✅ Required | Rush or pnpm | `node common/scripts/install-run-rush.js update` (rush) or `pnpm install` (pnpm) |
| **7. Apply Custom Code** | ⚠️ Optional | `customizeCodes()` — pnpm repo only | `dev-tool customization apply-v2 -s ./generated -c ./src` |
| **8. Lint Fix** | ⚠️ Optional | `lintFix()` — `Release` / `Local` mode only | `pnpm turbo build --filter @azure/eslint-plugin-azure-sdk...` then `dev-tool run vendored eslint ... --fix` |
| **9. Build** | ✅ Required | Compile package | `pnpm turbo build --filter {packageName}...` |
| **10. Pack** | ✅ Required | Generate `.tgz` | `node common/scripts/install-run-rush.js pack --to {packageName}` (rush) or `pnpm run --filter {packageName}... pack` (pnpm) |
| **11. Format Code** | ✅ Required | `formatSdk()` | `dev-tool run vendored prettier --write "src/**/*.{ts,cts,mts}" ...` |
| **12. Update Snippets** | ✅ Required | `updateSnippets()` | `dev-tool run update-snippets` |
| **13. Generate Changelog & Bump Version** | ✅ Required | `generateChangelogAndBumpVersion()` | Same as HLC |
| **14. Add ApiView Info** | ✅ Required | `addApiViewInfo()` | Find `*.api.json` files |
| **15. Restore Config** | ✅ Required | `changeConfigOfTestAndSample(Revert)` | Restore original `tsconfig.json` |

---

## 5. ModularClient (MLC) — Modular Client

### Processing Steps

| Step | Required | Operation | Command / Details |
|---|---|---|---|
| **1. Get Target Package Dir** | ✅ Required | `getGeneratedPackageDirectory()` | Parse `emitter-output-dir` / `service-dir` + `package-dir` from `tspconfig.yaml` |
| **2. Generate CODEOWNERS & ignore-links** | ⚠️ Optional | `codeOwnersAndIgnoreLinkGenerator()` | For first-time published packages: update `.github/CODEOWNERS` and `eng/ignore-links.txt` |
| **3. Record Original Version** | ✅ Required | `getNpmPackageInfo()` | Read existing `package.json` version to restore after code generation |
| **4. Clean Up Package Dir** | ✅ Required | `cleanUpPackageDirectory()` | Cleanup strategy based on `runMode` + SDK type (see [Section 2](#2-folder-cleanup-logic)) |
| **5. Specify API Version** | ⚠️ Optional | `specifyApiVersionToGenerateSDKByTypeSpec()` | Modify `api-version` field in `tspconfig.yaml` if `apiVersion` is specified |
| **6. Code Generation** | ✅ Required | `generateTypeScriptCodeFromTypeSpec()` | `npm --prefix eng/common/tsp-client exec -- tsp-client init --update-if-exists --debug --tsp-config {tspconfig.yaml} --local-spec-repo {typeSpecDir} --repo {repoUrl} --commit {commitId}` |
| **7. Restore Version** | ✅ Required | `updatePackageVersion()` | Restore `package.json` version to the pre-generation original to avoid version drift |
| **8. Build Package** | ✅ Required | `buildPackage()` — contains multiple sub-steps | See [sub-steps below](#buildpackage-sub-steps) |
| **9. Generate Changelog & Bump Version** | ✅ Required | `generateChangelogAndBumpVersion()` | Same as HLC; skipped for Data Plane packages |
| **10. Try Build Samples** | ⚠️ Conditional | `tryBuildSamples()` | `dev-tool run build:samples`. Blocking rules: **Management plane** — failure is a hard error in `Release` mode only; treated as a warning in all other modes (`SpecPullRequest`, `Batch`, `Local`). **Data plane** — always treated as a warning (never blocks). Known gap ([#14610](https://github.com/Azure/azure-sdk-tools/issues/14610)): sample failures are not caught during spec PR validation (`SpecPullRequest` mode), so a package that passes spec PR checks can still fail at release time. |
| **11. Update Package Result** | ✅ Required | `updateNpmPackageResult()` | Read `package.json` name/version into `PackageResult` |
| **12. Create Release Artifact** | ✅ Required | `createArtifact()` | `node rushx pack` (rush) or `pnpm run --filter {packageName}... pack` (pnpm), generates `.tgz` |
| **13. Create/Update CI YAML** | ✅ Required | `createOrUpdateCiYaml()` | Create or update `ci.mgmt.yml` |

### `buildPackage()` Sub-steps Detail

| Sub-step | Required | Command / Operation |
|---|---|---|
| Update `rush.json` | ✅ Required (rush repo) | Add package to `rush.json` project list |
| pnpm / rush install | ✅ Required | `node rushScript update` (rush) or `pnpm install` (pnpm) |
| Lint fix | ⚠️ Optional | `dev-tool run vendored eslint ... --fix` — only in `Release` / `Local` mode (pnpm repo) |
| Apply custom code | ⚠️ Optional | `dev-tool customization apply-v2 -s ./generated -c ./src` — Data Plane packages only |
| turbo build | ✅ Required | `pnpm turbo build --filter {packageName}... --token 1` (build errors are warnings for Data Plane) |
| Extract ApiView info | ✅ Required | Find `temp/**/*-node.api.json` or `temp/**/*.api.json` |
| Test package | ⚠️ Optional | `rushx test:node` or `pnpm run test:node` — `TEST_MODE=record`; failure does not block |
| Format | ✅ Required | `dev-tool run vendored prettier --write ...` |
| Update snippets | ✅ Required | `dev-tool run update-snippets` |

---

## 6. Changelog & Version Bump (Common)

> **Note**: Changelog generation is **skipped** for Data Plane (`ModularClient` / `DataPlane`) packages.

### Core Logic

```
1. Query npm registry for published package info (tryGetNpmView)
2. Determine if first release (shouldTreatAsFirstRelease)
   ├── First Release:
   │   → makeChangesForFirstRelease(): use initial changelog template,
   │     set version to 1.0.0-beta.1 or 1.0.0
   │
   └── Non-first Release:
       ├── Download published stable version (npm pack {packageName}@{stableVersion})
       ├── Determine if track2 or track1:
       │
       ├── Track2 Previously Released:
       │   ├── Compare old and new api.md (using DifferenceDetector)
       │   ├── Generate changelog (using ChangelogGenerator)
       │   ├── Calculate new version (getNewVersion):
       │   │   - Has breaking change → bump minor/major
       │   │   - No changes → bump patch
       │   │   - Beta version → bump preview
       │   └── makeChangesForReleasingTrack2(): write CHANGELOG.md and update package.json version
       │
       └── Track1 Previously Released:
           └── makeChangesForMigrateTrack1ToTrack2(): generate migration changelog
```

### Key Sub-operations

| Operation | Command |
|---|---|
| Download and extract npm package | `npm pack {packageName}@{version}` → `tar -xzf {tgz}` |
| Get original version | `git show HEAD:{package.json path}` |
| Clean up temp files | Delete `changelog-temp/` directory |

---

## 7. Utility Operations Summary

| Operation | Function | Required / Optional | Description |
|---|---|---|---|
| Backup node_modules | `backupNodeModules()` | ✅ Required (non-local) | Recursively rename `node_modules` → `node_modules_backup` |
| Restore node_modules | `restoreNodeModules()` | ✅ Required (non-local) | Recursively rename back to `node_modules` |
| Format code | `formatSdk()` | ✅ Required | `dev-tool run vendored prettier --write ...` |
| Update snippets | `updateSnippets()` | ✅ Required | `dev-tool run update-snippets` |
| Lint fix | `lintFix()` | ⚠️ Optional (`Release`/`Local` only) | Builds eslint plugin then `dev-tool run vendored eslint ... --fix` |
| Apply custom code | `customizeCodes()` | ⚠️ Optional (Data Plane, pnpm) | `dev-tool customization apply-v2 -s ./generated -c ./src` |
| Clean up package dir | `cleanUpPackageDirectory()` | ✅ Required | Cleanup strategy based on SDK type + `RunMode` |
| Specify API version | `specifyApiVersionToGenerateSDKByTypeSpec()` | ⚠️ Optional | Modify `api-version` field in `tspconfig.yaml` |

---

## 8. Output JSON Structure

Final structure written to `--outputJsonPath`:

```json
{
  "packages": [
    {
      "packageName": "@azure/arm-xxx",
      "version": "1.0.0",
      "language": "JavaScript",
      "path": ["sdk/xxx/arm-xxx", "ci.mgmt.yml"],
      "packageFolder": "sdk/xxx/arm-xxx",
      "typespecProject": ["specification/xxx/XXX"],
      "readmeMd": ["specification/xxx/resource-manager/readme.md"],
      "artifacts": ["sdk/xxx/arm-xxx/azure-arm-xxx-1.0.0.tgz"],
      "apiViewArtifact": "sdk/xxx/arm-xxx/temp/arm-xxx.api.json",
      "changelog": {
        "content": "### Breaking Changes\n...",
        "hasBreakingChange": true,
        "breakingChangeItems": ["..."]
      },
      "result": "succeeded",
      "installInstructions": { "full": "npm install ..." }
    }
  ],
  "language": "JavaScript"
}
```

---

## 9. Overall Flow Diagram

```
CLI Entry (autoGenerateInPipeline.ts)
  │
  ├── Parse inputJson → parseInputJson()
  ├── Determine SDKType
  ├── backupNodeModules()  (non-local mode only)
  │
  ├── switch(SDKType)
  │   │
  │   ├── HighLevelClient ──→ generateMgmt()
  │   │   ├── autorest code generation
  │   │   ├── Find changed packages (git diff)
  │   │   ├── Update rush.json / ci.yml / _meta.json
  │   │   ├── rush update → rush build → changelog → rush pack
  │   │   └── Update snippets / README
  │   │
  │   ├── RestLevelClient ──→ generateRLCInPipeline()
  │   │   ├── TypeSpec → tsp-client init or tsp compile
  │   │   │   OR
  │   │   ├── Swagger → autorest code generation
  │   │   ├── Update rush.json / ci.yml
  │   │   ├── install → customize → lint → build → pack
  │   │   ├── format → snippets → changelog
  │   │   └── Output artifacts & apiView
  │   │
  │   └── ModularClient ──→ generateAzureSDKPackage()
  │       ├── CODEOWNERS & ignore-links
  │       ├── tsp-client init code generation
  │       ├── buildPackage:
  │       │   ├── rush/pnpm update
  │       │   ├── lint fix (Release/Local)
  │       │   ├── customize (Data Plane)
  │       │   ├── turbo build
  │       │   ├── extract ApiView info
  │       │   ├── test package
  │       │   ├── format
  │       │   └── update snippets
  │       ├── changelog & bump version (Management Plane only)
  │       ├── tryBuildSamples
  │       ├── createArtifact (pack → .tgz)
  │       └── createOrUpdateCiYaml
  │
  ├── restoreNodeModules()  (non-local mode only)
  └── Write outputJson
```
