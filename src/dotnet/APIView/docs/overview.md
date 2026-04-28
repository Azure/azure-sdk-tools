# APIView — Architecture Overview

## 1. What Is APIView?

APIView is the Azure SDK team's **API review platform**. It ingests SDK artifacts (DLLs, JARs, Python wheels, etc.) or pre-parsed JSON token files representing the public API surface and presents them in a web UI where architects and reviewers can comment, diff across versions, approve, and track the lifecycle of an API from first draft through release. Reviews are created automatically by CI pipelines (on both PR and merge builds) or manually by uploading an artifact through the web UI.

It supports **16+ languages** (C#, Java, Python, JavaScript/TypeScript, Go, Rust, C, C++, Swift, Protocol Buffers, Swagger/OpenAPI, TypeSpec, and more) through a common token model that normalizes every language into the same reviewable format.

> **Development policy:** New features should target the Angular SPA and tree-token model only. The legacy Razor Pages frontend and flat-token parser are not receiving new investment. See [legacy.md](legacy.md) for details on what remains and the migration path.

### a. What APIView Does

- **Parses** pre-built SDK artifacts into a language-agnostic token model.
- **Renders** public API surfaces with syntax highlighting and hierarchical navigation.
- **Diffs** any two revisions of the same package using Myers' O(ND) algorithm.
- **Hosts threaded comments** anchored to specific API elements (types, methods, parameters).
- **Tracks approval state** per API revision, with change history and audit trail.
- **Carries forward approvals** automatically when an API surface is unchanged between versions.
- **Integrates with GitHub PRs**, auto-posting review links as PR comments.
- **Supports AI-powered reviews** via a Copilot background service that polls for pending jobs.
- **Links APIs across languages** through `CrossLanguageDefinitionId`, enabling side-by-side review of the same concept in C#, Java, Python, etc.
- **Sends notifications** via email and real-time SignalR push.
- **Marks revisions as released** when a release pipeline calls with `setReleaseTag=true`.

### b. What APIView Does NOT Do

- **Does not compile code.** Parsers consume already-built binaries, not source files.
- **Does not run tests.** It is a review tool, not a CI runner.
- **Does not host packages.** It is not a package registry or artifact feed.
- **Does not generate code.** It analyzes existing APIs; it does not produce SDK source.
- **Does not enforce release gates directly.** It reports approval status via HTTP status codes; the actual block/allow decision lives in the external CI/CD pipeline that queries it.
- **Does not perform runtime analysis.** It inspects the public API surface statically from metadata, not from executing the code.

---

## 2. Solution Structure

```
APIView.sln
├── APIView/              Core library — token models, rendering, diff algorithm, C# parser
├── APIViewWeb/           ASP.NET Core web application — backend API + Angular SPA host
├── APIViewUnitTests/     Unit and integration tests
├── APIViewJsonUtility/   CLI tool for inspecting / converting token JSON files
├── ClientSPA/            Angular SPA frontend (separate build, output → APIViewWeb/wwwroot/spa)
└── docs/                 Documentation (you are here)
```

---

## 3. Data Model Hierarchy

```
Review (ReviewListItemModel)
│   One per package (e.g., "Azure.Storage.Blobs" for C#).
│   Fields: id, PackageName, Language, CreatedBy, IsApproved
│
└──► APIVersion (APIVersionModel)   [1 → many per Review]
    │   One per distinct package version string (e.g. "1.0.0-beta.1").
    │   Normalizes the raw version into a canonical VersionIdentifier and classifies
    │   it as Stable, Preview, or Rolling. Tracks PR association and retention state.
    │   Fields: ReviewId, VersionIdentifier, Kind, PullRequestNumber, PrStatus,
    │           RetainUntil, IsDeleted, ChangeHistory[]
    │
    └──► APIRevision (APIRevisionListItemModel)   [1 → many per APIVersion / Review]
        │   One per upload / version snapshot. Type is Manual, Automatic, or PullRequest.
        │   Fields: ReviewId, APIRevisionType, IsApproved, Approvers, IsReleased, ReleasedOn
        │   Carries change history (ChangeHistory[]) for audit.
        │
        └──► CodeFile (APICodeFileModel)   [1 → many per APIRevision]
                 The parsed token stream stored in Blob Storage.
                 Fields: Language, PackageVersion, PackageName, ContentHash
```

A **Review** is the long-lived container for a package.  
An **APIVersion** represents a specific version string of the package and groups all revisions built from that version. It provides a stable identity for version-level operations such as approval, retention, and PR association.  
An **APIRevision** is a snapshot of the API surface at a point in time.  
A **CodeFile** is the serialized parser output (tokens + navigation + diagnostics).

---

## 4. Backend Architecture (APIViewWeb)

**Stack:** ASP.NET Core · Cosmos DB · Azure Blob Storage · SignalR · Application Insights

### a. Layers

```
HTTP Layer                  Business Logic              Data Access
──────────                  ──────────────              ───────────
LeanControllers/ (14)  ───► Managers/ (14+)  ──────►  Cosmos Repositories (8)
  REST (Cookie + Token Auth)    Core workflows              Blob Repositories (3)
                              Approvals, Diff             DevOps Artifact Repo
Controllers/ (6)              Comments, Notifications
  Legacy MVC (Razor)          PR integration
                              AI/Copilot jobs
```

### b. Key Controllers (LeanControllers)

Most LeanControllers inherit from `BaseApiController`, which applies **cookie authentication** (`RequireCookieAuthentication`) and routes under `api/[controller]`. These serve the Angular SPA.

| Controller | Route | Responsibility |
|---|---|---|
| `APIRevisionsController` | `api/Revisions` | Revision CRUD, approval toggle, diff requests |
| `ReviewsController` | `api/Reviews` | Review listing, search, filtering |
| `CommentsController` | `api/Comments` | Comment CRUD with thread support |
| `PullRequestsController` | `api/PullRequests` | PR ↔ review linking |
| `ProjectsController` | `api/Projects` | Package/project grouping |

A separate set of controllers use **token authentication** (`RequireTokenAuthentication`) for CI pipelines and service-to-service calls:

| Controller | Route | Responsibility |
|---|---|---|
| `AutoReviewController` | `autoreview/` | CI pipeline entry points (`/upload`, `/create`) |
| `APIRevisionsTokenAuthController` | `api/apirevisions` | Revision queries for CI pipelines |
| `ReviewsTokenAuthController` | `api/reviews` | Review queries for CI pipelines |
| `CommentsTokenAuthController` | `api/comments` | Comment operations for CI pipelines |

### c. Key Managers (business logic)

| Manager | Responsibility |
|---|---|
| `ReviewManager` | Review lifecycle, approval notifications, deletion |
| `APIRevisionsManager` | Revision creation, approval toggle, carry-forward, content hashing |
| `CodeFileManager` | Token file serialization/deserialization, SHA-256 content hashing |
| `CommentsManager` | Threaded comments, reactions |
| `PullRequestManager` | GitHub PR integration via Octokit |
| `AutoReviewService` | Automatic revision creation, matching against approved revisions |
| `NotificationManager` | Email and SignalR notifications |
| `PermissionsManager` | RBAC for users and approvers |
| `APIVersionsManager` | Version entity lifecycle |

### d. Data Stores

| Store | Used For |
|---|---|
| **Azure Cosmos DB** | Reviews, API revisions, comments, pull requests, user profiles, permissions, projects, sample revisions |
| **Azure Blob Storage** | CodeFile JSON blobs, original uploaded artifacts, usage samples |
| **Azure DevOps Artifacts** | Fetching build artifacts when `/create` endpoint is used |

### e. Background Services (HostedServices/)

| Service | Purpose |
|---|---|
| `CopilotPollingBackgroundHostedService` | Polls for and processes AI review jobs |
| `ReviewBackgroundHostedService` | Re-parses reviews when parser versions are updated; auto-archives inactive revisions; purges soft-deleted revisions |
| `PullRequestBackgroundHostedService` | Asynchronous PR comment posting |
| `LinesWithDiffBackgroundHostedService` | Pre-computes diff section headings |
| `QueuedHostedService` | Generic background task executor |

### f. External Integrations

| System | Integration |
|---|---|
| **GitHub** | OAuth login, PR comment posting (Octokit), org membership checks |
| **Microsoft Entra ID** | JWT Bearer authentication for service-to-service calls |
| **Azure App Configuration** | Dynamic feature flags and config values |
| **Azure Key Vault** | Secrets (connection strings, API keys) |
| **Application Insights** | Request telemetry, custom events, error tracking |

---

## 5. Frontend Architecture (ClientSPA — Angular)

**Stack:** Angular 20 · PrimeNG 20 · Bootstrap 5.3 · SignalR · Monaco Editor · RxJS · Vite

The Angular SPA is the **primary UI**. It is built separately (`npm run build`) and output to `APIViewWeb/wwwroot/spa/`. The ASP.NET backend serves it as static files and acts purely as an API host.

> **Note:** There is a legacy Razor Pages frontend (`Pages/Assemblies/`) that predates the SPA. It is being phased out; see [legacy.md](legacy.md) for details.

### a. Routes

| Route | Component | Purpose |
|---|---|---|
| `/` | `IndexPageComponent` | Home — review list with search/filter |
| `/review/:reviewId` | `ReviewPageModule` | Main review UI: code panel, comments, diff, navigation |
| `/conversation/:reviewId` | `ConversationPageModule` | Conversation / Copilot chat view |
| `/revision/:reviewId` | `RevisionPageModule` | Revision management |
| `/samples/:reviewId` | `SamplesPageModule` | Code sample attachments |
| `/profile/:userName` | `ProfilePageComponent` | User settings and preferences |
| `/admin/permissions` | `AdminPermissionsPageComponent` | RBAC management |

All routes require authentication via `AuthGuard`.

### b. Key Components

| Component | Responsibility |
|---|---|
| `code-panel/` | Renders the token stream with syntax highlighting, line anchoring for comments, hidden-API toggling |
| `review-page/` | Orchestrates the review experience: code panel + nav tree + options bar |
| `review-page-options/` | Approval button, revision selector, diff toggle, language-specific options |
| `review-nav/` | Hierarchical navigation tree (namespaces → types → members) |
| `conversations/` | Threaded comment display and input |
| `cross-lang-view/` | Side-by-side view of the same API across multiple language SDKs |
| `revisions-list/` | Revision history with approval badges and release status |
| `reviews-list/` | Searchable, filterable table of all reviews |

### c. Services & State Management

There is **no NgRx store**. State is managed through **RxJS BehaviorSubjects** in Angular services:

| Service | Key State / Responsibility |
|---|---|
| `ConfigService` | Loads `/assets/config.json` at startup (apiUrl, hubUrl), manages theme |
| `AuthService` | `isLoggedIn$` observable, credentials check |
| `ReviewContextService` | Current `reviewId`, `language`, `languageApprovers` — avoids prop drilling |
| `SignalRService` | Manages hub connection for real-time updates (approval changes, AI job results) |
| `NotificationsService` | Notification state with IndexedDB persistence |
| `CommentsService` | Comment CRUD against `/api/comments` |
| `ReviewsService` | Review list/search against `/api/reviews` |
| `RevisionsService` | Revision management against `/api/revisions` |
| `PullRequestsService` | PR linking against `/api/pullrequests` |
| `PermissionsService` | RBAC checks |
| `UserProfileService` | User preferences (theme, layout) |

### d. Backend Communication

- **Base URL:** Configurable via `/assets/config.json`; defaults to `api/` (relative).
- **Auth:** Cookie-based with `{ withCredentials: true }`.
- **Error handling:** `HttpErrorInterceptorService` catches 403 → redirects to `/Unauthorized`.
- **Real-time:** SignalR hub at `hubs/notification` for live approval updates, AI job completion, etc.

---

## 6. Token File Format (CodeFile)

APIView's core abstraction is a **language-agnostic token model**. Every language parser emits the same JSON structure, which the frontend renders uniformly.

### a. Two Formats (both supported)

| Format | Model | Status |
|---|---|---|
| **Flat (legacy)** | `CodeFileToken[]` — linear stream with `Newline` tokens as line separators | Deprecated; auto-converted on read |
| **Tree (modern)** | `ReviewLine[]` — hierarchical lines with `ReviewToken[]` per line and `Children[]` for nesting | Active; all modern parsers emit this |

### b. Key Fields on CodeFile

```
CodeFile
├── Language            e.g., "C#", "Java", "Python"
├── PackageName         e.g., "Azure.Storage.Blobs"
├── PackageVersion      e.g., "12.14.0"
├── ReviewLines[]       Hierarchical token structure (modern)
├── Tokens[]            Flat token array (legacy, auto-converted)
├── Navigation[]        Tree of NavigationItems for the nav panel
├── Diagnostics[]       Warnings / errors from the parser
└── CrossLanguageMetadata   Maps language-specific IDs to cross-language IDs
```

### c. Token Kinds

Modern (`ReviewToken.Kind`): `Text`, `Punctuation`, `Keyword`, `TypeName`, `MemberName`, `StringLiteral`, `Literal`, `Comment`

### d. ID System

- **`LineId`** (on `ReviewLine`): Uniquely identifies an API element. Comments anchor to this ID.
- **`NavigateToId`** (on `ReviewToken`): Click-to-navigate target (e.g., a type reference jumps to its definition).
- **`CrossLanguageDefinitionId`**: Bridges the same concept across SDKs (e.g., a `BlobClient` in C# and `BlobClient` in Python share an ID).

### e. Content Hashing

Each CodeFile gets a **SHA-256 hash** of its API surface (excluding package version, documentation, and `SkipDiff` regions). This enables O(1) comparison between revisions — if the hash matches, the API surface is identical, and approval can be carried forward without downloading the blob.

---

## 7. Diff Pipeline

1. Two `CodeFile` blobs are loaded (active revision vs. selected diff revision).
2. `ReviewLine` trees are flattened to comparable line sequences.
3. **Myers' O(ND) diff algorithm** (`APIView/DIff/Diff.cs`) computes the minimal edit script.
4. Each line is tagged as `Added`, `Removed`, or `Unchanged`.
5. The frontend renders additions/removals inline with configurable context lines.
6. Section headings with diffs are pre-computed by `LinesWithDiffBackgroundHostedService` so the nav tree can highlight changed areas.

---

## 8. Approval & Release Flow

### a. Manual Approval
A reviewer clicks **Approve** in the UI → `ToggleAPIRevisionApprovalAsync` toggles the approval state, updates the `Approvers` HashSet, records in `ChangeHistory`, and broadcasts via SignalR.

### b. Automatic Carry-Forward
When a new revision is created and its **content hash matches** an already-approved revision, approval is copied automatically. The change history records `"Approval copied from revision {id}"`.

### c. Release Tagging
When a release pipeline calls `/autoreview/upload` or `/autoreview/create` with `setReleaseTag=true`, the matching revision gets `IsReleased = true` and `ReleasedOn = DateTime.UtcNow`. The UI shows it as **"Shipped"**.

---

## 9. Language Parsers

Parsers live in various locations across the `azure-sdk-tools` repo.

| Language | Repo Path | Input | Token Format | Server Can Parse? |
|---|---|---|---|---|
| C# | `tools/apiview/parsers/csharp-api-parser` | `.dll`, `.nupkg` | Tree | Yes |
| Java | `src/java/apiview-java-processor` | `.jar` | Tree | Yes |
| Python | `packages/python-packages/apiview-stub-generator` | `.whl` | Tree | Yes |
| JavaScript/TypeScript | `tools/apiview/parsers/js-api-parser` | `.api.json` | Tree | Yes |
| Go | `src/go` | `.gosource` (zip) | Tree | Yes |
| TypeSpec | `tools/apiview/emitters/typespec-apiview` | `.tsp`, `.cadl` | Tree | No  |
| Rust | `tools/apiview/parsers/rust-api-parser` | `.rust.json` | Tree | Yes |
| Swift | `src/swift/SwiftAPIView` | `.json` | Tree | No |
| Swagger/OpenAPI | `tools/apiview/parsers/swagger-api-parser` | `.swagger` | Flat (legacy) | No  |
| C++ | `tools/apiview/parsers/cpp-api-parser` | `.cppast` | Flat (legacy) | Yes |
| XML | `src/java/apiview-java-processor` | `.xml` | Flat (legacy) | Yes |
| C | *(no external parser)* | `.zip` | Flat (legacy) | Yes |
| Protocol Buffers | `packages/python-packages/protocol-stub-generator` | `.yaml` | Flat (legacy) | Yes |

---

## 10. Core Workflows

There are three ways API revisions reach APIView: **CI Automatic** (the persistent review created on merges to `main`), **CI Pull Request** (the ephemeral revision created for PR review), and **Manual** (a user uploads a file through the web UI). The first two are automated; they share the same build step that produces the artifact but diverge in how that artifact reaches the server.

### How parsing works: server-side vs. CI-side

A key variable across all workflows is **where the language parser runs**. This is determined by whether the CI build produces a pre-parsed token file (`{packageName}_{languageShort}.json`) alongside the build artifact. The shared scripts (`Create-APIReview.ps1`, `Detect-Api-Changes.ps1`) check for this file and branch accordingly:

- **No token file present** (C#, Java, Go, Rust): The build artifact (`.nupkg`, `sources.jar`, `.gosource`, `.rust.json`) is sent to APIView. APIView invokes the language parser as an external process on the server.
- **Token file present** (Python, JavaScript): The CI pipeline runs the parser to produce a `_python.json` or `_js.json` token file. Both the token file and the original artifact are sent to APIView, which stores them directly without running a parser. See [sandboxing.md](sandboxing.md) for rationale.

> **Note:** Go and Rust do CI-side *preprocessing* (zipping source into `.gosource` archives, generating `.rust.json` intermediate files), but the APIView server still runs the actual parser on those intermediate artifacts. Python and JavaScript are the only languages where the full APIView parser runs in CI.

### Workflow A — CI Automatic (non-PR internal builds)

**Trigger:** Non-PR builds on `internal` project (merges to `main`, manual runs).  
**Template:** `eng/common/pipelines/templates/steps/create-apireview.yml` → `Create-APIReview.ps1`  
**Condition:** `System.TeamProject == 'internal' && Build.Reason != 'PullRequest'`  
**Purpose:** Creates or updates the persistent review for a package — the long-lived record that tracks API evolution across versions.

```
SDK CI/CD Pipeline                                     APIView
─────────────────                                      ───────

Build artifact + (optionally) run parser
        │
        ├── Token file exists?
        │       │
        │       ├── YES (Python, JS)
        │       │   POST /autoreview/create ─────────► Download token file + original
        │       │   (build coordinates only)           artifact from DevOps
        │       │                                      Store both in Blob Storage
        │       │
        │       └── NO (C#, Java, Go, Rust)
        │           POST /autoreview/upload ─────────► Save original artifact to
        │           (multipart, binary artifact)       Blob Storage
        │                                              Invoke language parser on server
        │                                              ─────────────────────────────────
        │                                              Store token file in Blob Storage;
        │                                              metadata in Cosmos DB
        ▼
(validate-all-packages.yml)
Query approval status
  200 = approved
  201 = namespace-approved
  202 = pending
```

1. The CI pipeline builds the package artifact. For some languages, a separate step also runs the parser to produce a token file (see table below).
2. `Create-APIReview.ps1` checks for `{packageName}_{languageShort}.json` in the artifact directory.
   - **If found:** Calls `POST /autoreview/create` with DevOps build coordinates (`buildId`, `artifactName`, file paths). APIView downloads the token file and original artifact from **Azure DevOps Artifacts**, saves the original to Blob Storage, and stores the token file in Blob Storage with metadata in Cosmos DB.
   - **If not found:** Calls `POST /autoreview/upload` with the binary artifact as a multipart form upload. APIView saves the artifact to Blob Storage, invokes the **language-specific parser** on the server, and stores the resulting token file in Blob Storage and metadata in Cosmos DB.
3. A separate step (`validate-all-packages.yml`) queries APIView for approval status. The response code determines whether the package passes the release gate.

| Language | Artifact | Token file in CI? | Endpoint hit |
|---|---|---|---|
| C# | `.nupkg` | No | `/autoreview/upload` |
| Java | `*-sources.jar` | No | `/autoreview/upload` |
| Python | `.whl` + `_python.json` | **Yes** (mandatory) | `/autoreview/create` |
| JavaScript | `.api.json` + `_js.json` | **Yes** | `/autoreview/create` |
| Go | `.gosource` | No | `/autoreview/upload` |
| Rust | `.rust.json` | No | `/autoreview/upload` |

### Workflow B — CI Pull Request

**Trigger:** PR builds only (`Build.Reason == 'PullRequest'`).  
**Template:** `eng/common/pipelines/templates/steps/detect-api-changes.yml` → `Detect-Api-Changes.ps1`  
**Purpose:** Creates a PR-scoped APIView revision so reviewers can see what API changes the PR introduces. APIView posts a comment back to the GitHub PR with a link to the review.

```
SDK CI/CD Pipeline                                     APIView
─────────────────                                      ───────

Build artifact + (optionally) run parser
Publish as pipeline artifacts
        │
        ▼
GET /api/PullRequests/                    ───────────► Download artifact(s) from DevOps
  CreateAPIRevisionIfAPIHasChanges                     using buildId + artifactName
  ?buildId=...&artifactName=...                               │
  &pullRequestNumber=...&commitSha=...                        ▼
  &packageName=...&language=...                         ┌─ codeFile param present?
  [&codeFile=..._lang.json]                             │
                                                        ├── YES: store token file directly
                                                        └── NO:  invoke language parser
                                                                 on server
                                                              │
                                                              ▼
                                                        Store token file in Blob Storage;
                                                        metadata in Cosmos DB
                                                        (APIRevisionType = PullRequest)
                                                              │
                                                              ▼
                                                        Post comment on GitHub PR
                                                        with link to review
```

1. The CI pipeline builds the artifact and publishes it (and optionally a token file) as a pipeline artifact — the same steps as Workflow A.
2. `Detect-Api-Changes.ps1` iterates packages that have changes in the PR. For each, it calls `GET /api/PullRequests/CreateAPIRevisionIfAPIHasChanges` with DevOps build coordinates (`buildId`, `artifactName`, `filePath`), PR metadata (`pullRequestNumber`, `commitSha`, `repoName`), and package info (`packageName`, `language`).
   - **If a token file exists** (Python, JS): The `codeFile` query param is added. APIView downloads the parent directory as a zip, extracting both the token file and original artifact.
   - **If no token file** (C#, Java, Go, Rust): APIView downloads only the artifact file and runs the language parser on the server.
3. APIView creates a **PullRequest-type** API revision linked to the PR. It asynchronously posts a comment on the GitHub PR with a link to the review.

> **Key difference from Workflow A:** The PR flow always uses DevOps build coordinates for artifact retrieval — even for languages like C# where Workflow A does a direct multipart upload. There is no direct file upload in the PR flow.

### Workflow C — Manual upload (web UI) *(legacy — prefer CI workflows)*

**Trigger:** User action in the Angular SPA.  
**Endpoint:** `POST /api/Reviews` (multipart form, cookie authentication via GitHub OAuth).  
**Purpose:** Allows developers and architects to create reviews outside of CI — for early design review, ad-hoc inspection, or languages without CI integration (e.g., Swift).

> **Note:** Manual upload is a legacy workflow. For languages with CI integration, prefer Workflow A/B — they provide traceability to specific builds and PRs, automatic approval carry-forward, and release tagging. Manual upload should only be used for languages without CI support or one-off exploratory reviews.

```
User (Angular SPA)                                    APIView
──────────────────                                    ───────

Select language + upload artifact
        │
        ▼
POST /api/Reviews ───────────────────────────────────► Save artifact to Blob Storage
  (multipart: file, language, label)                          │
                                                              ▼
                                                       Invoke language parser on server
                                                              │
                                                              ▼
                                                       Store token file in Blob Storage;
                                                       metadata in Cosmos DB
                                                       (APIRevisionType = Manual)
                                                              │
                                                              ▼
                                                       Redirect user to review page
```

1. The user selects a language, uploads a file, and optionally adds a label.
2. If the uploaded file is a build artifact (`.nupkg`, `.whl`, `.jar`, etc.), APIView saves it to Blob Storage and invokes the language parser on the server. If the uploaded file is a pre-parsed JSON token file (`.json`), parsing is skipped — the token file is stored directly.
3. A new Review (or new revision on an existing Review for the same package) is created.
4. The user is redirected to the review page in the SPA.

---

## 11. SDK CI Pipelines (APIView Integration)

Each Azure SDK language repo has a per-service `ci.yml` pipeline (e.g. `sdk/storage/ci.yml`) that triggers on PRs and merges. These pipelines call shared archetype templates which eventually invoke two shared APIView step templates from `eng/common/` (synced from this repo):

| Template (eng/common/) | Trigger | Purpose |
|---|---|---|
| `pipelines/templates/steps/detect-api-changes.yml` | PR only | Calls `Detect-Api-Changes.ps1` → `POST /api/PullRequests/CreateAPIRevisionIfAPIHasChanges` to create a PR-scoped APIView revision |
| `pipelines/templates/steps/create-apireview.yml` | Non-PR CI (internal) | Calls `Create-APIReview.ps1` → `POST /autoreview/upload` or `/autoreview/create` to create a persistent review |
| `pipelines/templates/steps/validate-all-packages.yml` | Non-PR CI (internal) | Checks APIView approval status and validates packages |

### Per-Language Pipeline Chain

Each language repo follows a similar pattern: `ci.yml` → archetype stage → job → step template where APIView is invoked. The table below shows the language-specific template in each repo that calls the shared APIView templates.

| Language | SDK Repo | Template That Invokes APIView | Notes |
|---|---|---|---|
| C# | `Azure/azure-sdk-for-net` | [`eng/pipelines/templates/steps/build.yml`](https://github.com/Azure/azure-sdk-for-net/blob/main/eng/pipelines/templates/steps/build.yml) | Calls `create-apireview.yml` + `detect-api-changes.yml` after `dotnet pack` |
| Java | `Azure/azure-sdk-for-java` | [`eng/pipelines/templates/jobs/ci.yml`](https://github.com/Azure/azure-sdk-for-java/blob/main/eng/pipelines/templates/jobs/ci.yml) | Build job calls `create-apireview.yml` + `detect-api-changes.yml` after Maven deploy |
| Python | `Azure/azure-sdk-for-python` | [`eng/pipelines/templates/steps/analyze.yml`](https://github.com/Azure/azure-sdk-for-python/blob/main/eng/pipelines/templates/steps/analyze.yml) | Calls `create-apireview.yml` + `detect-api-changes.yml` after whl verification |
| JavaScript/TypeScript | `Azure/azure-sdk-for-js` | [`eng/pipelines/templates/steps/build.yml`](https://github.com/Azure/azure-sdk-for-js/blob/main/eng/pipelines/templates/steps/build.yml) | Runs `Generate-APIView-CodeFile.ps1` to create token file, then calls `create-apireview.yml` + `detect-api-changes.yml` |
| Go | `Azure/azure-sdk-for-go` | [`eng/pipelines/templates/steps/analyze.yml`](https://github.com/Azure/azure-sdk-for-go/blob/main/eng/pipelines/templates/steps/analyze.yml) | Custom: calls `New-APIViewArtifacts` (from `eng/scripts/apiview-helpers.ps1`) to create `.gosource` zips, then calls `detect-api-changes.yml` |
| C++ | `Azure/azure-sdk-for-cpp` | [`eng/pipelines/templates/jobs/archetype-sdk-client.yml`](https://github.com/Azure/azure-sdk-for-cpp/blob/main/eng/pipelines/templates/jobs/archetype-sdk-client.yml) | `GenerateReleaseArtifacts` job downloads `ParseAzureSdkCpp.exe`, runs `Generate-APIReview-Token-Files.ps1`, then calls `create-apireview.yml` + `detect-api-changes.yml` |
| Rust | `Azure/azure-sdk-for-rust` | [`eng/pipelines/templates/jobs/pack.yml`](https://github.com/Azure/azure-sdk-for-rust/blob/main/eng/pipelines/templates/jobs/pack.yml) | Pack job calls `create-apireview.yml` + `detect-api-changes.yml` after crate packing |
| Swift | `Azure/azure-sdk-for-ios` | *(none — manual upload)* | No automated CI → APIView integration; JSON token files are uploaded manually |
| TypeSpec | *(this repo)* | [`eng/pipelines/apiview-review-gen-typespec.yml`](../../eng/pipelines/apiview-review-gen-typespec.yml) | Manual-trigger pipeline; not per-service CI |
| Swagger/OpenAPI | *(this repo)* | [`eng/pipelines/apiview-review-gen-swagger.yml`](../../eng/pipelines/apiview-review-gen-swagger.yml) | Manual-trigger pipeline; not per-service CI |

---

## 12. Key File Paths (for agents)

| Area | Path |
|---|---|
| Solution | `APIView.sln` |
| Core models | `APIView/Model/CodeFile.cs`, `APIView/Model/V2/ReviewLine.cs`, `APIView/Model/V2/ReviewToken.cs` |
| Diff algorithm | `APIView/DIff/Diff.cs` |
| C# parser | `APIView/Languages/CodeFileBuilder.cs` |
| Web app entry | `APIViewWeb/Program.cs`, `APIViewWeb/Startup.cs` |
| REST controllers | `APIViewWeb/LeanControllers/` |
| Business logic | `APIViewWeb/Managers/` |
| Cosmos repositories | `APIViewWeb/Repositories/` |
| Language services | `APIViewWeb/Languages/` |
| Background services | `APIViewWeb/HostedServices/` |
| Angular SPA root | `ClientSPA/src/app/` |
| SPA components | `ClientSPA/src/app/_components/` |
| SPA services | `ClientSPA/src/app/_services/` |
| SPA models | `ClientSPA/src/app/_models/` |
| SPA routing | `ClientSPA/src/app/app-routing.module.ts` |
| SPA build output | `APIViewWeb/wwwroot/spa/` |
| Unit tests | `APIViewUnitTests/` |
| Pipeline | `apiview.yml` |