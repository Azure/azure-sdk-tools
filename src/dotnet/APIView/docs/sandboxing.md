# Sandboxing (Offline API Review Generation)

> **Status: Deprecated.** Sandboxing is a legacy pattern that the team does not plan to extend. This document records where it exists and its implications. New language parsers should **not** adopt this pattern.

## 1. What Is Sandboxing?

APIView converts language-specific SDK artifacts (e.g., `.whl`, `.tsp`, `.swagger`) into a JSON token file (`CodeFile`) using per-language parsers. By default, parsers run **on the APIView server host** — most as external processes launched via `LanguageProcessor` (`System.Diagnostics.Process.Start()`), and a few (C, C++, Json) via in-process deserialization.

"Sandboxing" means running the parser in an **Azure DevOps pipeline** instead of on the APIView server host. The motivation was security: some parsers pull in third-party dependencies that could introduce vulnerabilities when executed server-side.

## 2. Languages That Use Sandboxing

| Language | Sandboxed? | Toggle |
|----------|-----------|--------|
| Python | Yes (configurable) | `ReviewGenByPipelineDisabledForPython` app setting. When `true`, Python falls back to in-process parsing. |
| TypeSpec | Yes (always) | Hard-coded `IsReviewGenByPipeline = true` in constructor. |
| Swagger | Yes (always) | Hard-coded `IsReviewGenByPipeline = true` in constructor. |
| C#, Java, Go, Rust, JS/TS, C, C++ | No | Parsers run on the APIView server host (most as external processes, some in-process). |

## 3. How It Works

### a. Overview

1. User uploads an artifact or a review needs regeneration.
2. APIView stores the original file in Blob Storage and creates a **placeholder** `CodeFile` with `ContentGenerationInProgress = true` and a user-facing message: _"API review is being generated for this revision and it will be available in few minutes."_
3. APIView triggers an Azure DevOps pipeline named `tools - generate-{Language}-apireview` (e.g., `tools - generate-Python-apireview`).
4. The pipeline runs the parser, produces the JSON token file, and publishes it as a build artifact.
5. The pipeline calls back to APIView via the `ReviewController.UpdateApiReview` endpoint.
6. APIView downloads the artifact ZIP from DevOps, deserializes each `CodeFile`, and updates Cosmos DB + Blob Storage.

### b. Sequence

```
User ──upload──▶ APIView Server
                   │
                   ├─ Store original artifact in Blob Storage
                   ├─ Create placeholder CodeFile (ContentGenerationInProgress = true)
                   ├─ Persist review/revision to Cosmos DB
                   └─ Trigger DevOps pipeline via DevopsArtifactRepository.RunPipeline()
                         │
                         ▼
                   Azure DevOps Pipeline
                   (tools - generate-{Language}-apireview)
                         │
                         ├─ Download original artifact
                         ├─ Run language parser
                         ├─ Publish JSON token file as build artifact
                         └─ HTTP GET → /Review/UpdateApiReview?repoName=...&buildId=...&artifactPath=...
                                │
                                ▼
                         APIView Server
                           │
                           ├─ Download artifact ZIP from DevOps
                           ├─ Deserialize CodeFile from JSON entries
                           ├─ Upsert CodeFile to Blob Storage
                           └─ Update revision metadata in Cosmos DB
```

### c. Batch Regeneration

Background updates for sandboxed languages are batched in `ReviewManager.UpdateReviewsUsingPipeline()` to avoid overwhelming DevOps with parallel pipeline runs. Revisions are collected into a list and sent in configurable batches with a **10-minute delay** between batches.

## 4. Key Code Locations

### a. Configuration & Flags

| File | What |
|------|------|
| [APIViewWeb/Languages/LanguageService.cs](../APIViewWeb/Languages/LanguageService.cs) | Base class: `IsReviewGenByPipeline` (default `false`), `ReviewGenerationPipelineUrl`, `GetReviewGenPendingCodeFile()`, `GeneratePipelineRunParams()` |
| [APIViewWeb/Languages/PythonLanguageService.cs](../APIViewWeb/Languages/PythonLanguageService.cs) | Reads `ReviewGenByPipelineDisabledForPython` config; sets `IsReviewGenByPipeline` accordingly |
| [APIViewWeb/Languages/TypeSpecLanguageService.cs](../APIViewWeb/Languages/TypeSpecLanguageService.cs) | `IsReviewGenByPipeline = true`; overrides `GeneratePipelineRunParams()` to validate GitHub URL |
| [APIViewWeb/Languages/SwaggerLanguageService.cs](../APIViewWeb/Languages/SwaggerLanguageService.cs) | `IsReviewGenByPipeline = true` |

### b. Pipeline Trigger & Callback

| File | What |
|------|------|
| [APIViewWeb/Managers/APIRevisionsManager.cs](../APIViewWeb/Managers/APIRevisionsManager.cs) | `GenerateAPIRevisionInExternalResource()` — builds pipeline params and triggers the run. `RunAPIRevisionGenerationPipeline()` — serializes params and calls `DevopsArtifactRepository.RunPipeline()`. `UpdateAPIRevisionCodeFileAsync()` — callback handler that downloads the artifact ZIP and updates the revision. |
| [APIViewWeb/Repositories/DevopsArtifactRepository.cs](../APIViewWeb/Repositories/DevopsArtifactRepository.cs) | `RunPipeline()` — queues a build in Azure DevOps with the review parameters. |
| [APIViewWeb/Controllers/ReviewController.cs](../APIViewWeb/Controllers/ReviewController.cs) | `UpdateApiReview` GET endpoint — the callback target that DevOps pipelines hit after generating the token file. |

### c. Placeholder & UI

| File | What |
|------|------|
| [APIView/Model/CodeFile.cs](../APIView/Model/CodeFile.cs) | `ContentGenerationInProgress` property on `CodeFile` |
| [APIViewWeb/Managers/CodeFileManager.cs](../APIViewWeb/Managers/CodeFileManager.cs) | `CreateCodeFileAsync()` — returns placeholder when `IsReviewGenByPipeline` is true |
| [ClientSPA/src/app/_components/review-page/review-page.component.ts](../ClientSPA/src/app/_components/review-page/review-page.component.ts) | Displays "API-Revision content is being generated" message when the API returns HTTP 202 |

### d. Batch Background Updates

| File | What |
|------|------|
| [APIViewWeb/Managers/ReviewManager.cs](../APIViewWeb/Managers/ReviewManager.cs) | `UpdateReviewsInBackground()` — routes sandboxed languages to `UpdateReviewsUsingPipeline()`. `UpdateReviewsUsingPipeline()` — collects eligible revisions and sends them in batches. |

### e. Models

| File | What |
|------|------|
| [APIViewWeb/Models/APIRevisionGenerationPipelineParamModel.cs](../APIViewWeb/Models/APIRevisionGenerationPipelineParamModel.cs) | Pipeline parameter payload: `ReviewID`, `RevisionID`, `FileID`, `FileName`, `SourceRepoName`, `SourceBranchName` |

## 5. App Configuration Keys

These settings are read from the app configuration (e.g., `appsettings.json`, App Configuration, or environment variables) at runtime. They are **not** committed in source:

| Key | Purpose |
|-----|---------|
| `ReviewGenByPipelineDisabledForPython` | When `true`, disables sandboxing for Python (falls back to in-process). |
| `PythonReviewGenerationPipelineUrl` | URL shown to users linking to the Python generation pipeline. |
| `TypeSpecReviewGenerationPipelineUrl` | URL shown to users linking to the TypeSpec generation pipeline. |
| `SwaggerReviewGenerationPipelineUrl` | URL shown to users linking to the Swagger generation pipeline. |
| `Azure-Devops-internal-project` | Azure DevOps project name (defaults to `"internal"`). |
| `apiview-deployment-environment` | Environment suffix appended to pipeline name (e.g., `tools - generate-Python-apireview-staging`). |

## 6. Known Limitations & Issues

1. **No failure reporting back to the UI.** If the DevOps pipeline fails, the review stays in the "being generated" state indefinitely. Users must manually check the pipeline.
2. **Latency.** Review generation depends on DevOps pipeline queue time, which can be slow under high load.
3. **10-minute batch delay.** Background regeneration inserts a hard-coded 10-minute sleep between batches, making large-scale upgrades slow.
4. **Callback endpoint is unauthenticated GET.** `ReviewController.UpdateApiReview` accepts a GET request with query parameters and no visible authentication, relying on network-level controls.
5. **Verify-upgradability mode is not supported.** The `UpdateReviewsInBackground` method explicitly skips sandboxed languages when running in `verifyUpgradabilityOnly` mode.

## 7. Implications for Future Work

- **Do not add new languages to sandboxing.** The pattern adds operational complexity (pipeline maintenance, failure handling, latency) without sufficient benefit now that parsers are being externalized in other ways.
- **Existing sandboxed languages will continue to work** but should be migrated away from this pattern when feasible.
- **If modifying pipeline-related code**, be aware of the tight coupling between the DevOps pipeline naming convention (`tools - generate-{Language}-apireview`), the JSON parameter serialization (single quotes replacing double quotes), and the callback endpoint.
