# APIView — Architecture Overview

## What Is APIView?

APIView is the Azure SDK team's **API review platform**. It ingests pre-built SDK artifacts (DLLs, JARs, Python wheels, etc.), extracts a structured token representation of the public API surface, and presents that surface in a web UI where architects and reviewers can comment, diff across versions, approve, and track the lifecycle of an API from first draft through release.

It supports **16+ languages** (C#, Java, Python, JavaScript/TypeScript, Go, Rust, C, C++, Swift, Protocol Buffers, Swagger/OpenAPI, TypeSpec, and more) through a common token model that normalizes every language into the same reviewable format.

> **Development policy:** New features should target the Angular SPA and tree-token model only. The legacy Razor Pages frontend and flat-token parser are not receiving new investment. See [legacy.md](legacy.md) for details on what remains and the migration path.

### Core Workflow

```
SDK CI/CD Pipeline                        APIView
─────────────────                         ───────
Build artifact (.dll, .jar, .whl, …)
        │
        ▼
POST /autoreview/upload ────────────────► Language parser (external process)
  or /autoreview/create                         │
                                                ▼
                                          JSON token file (CodeFile)
                                                │
                                                ▼
                                          Store in Cosmos DB + Blob Storage
                                                │
                                                ▼
                                          Render in Angular SPA
                                          (diff, comment, approve)
```

1. An Azure SDK CI/CD pipeline builds a package and POSTs the binary artifact to APIView.
2. APIView invokes a **language-specific parser** (external process, not in-process) that converts the artifact into a JSON token file (`CodeFile`).
3. The token file is stored in **Azure Blob Storage**; metadata goes to **Azure Cosmos DB**.
4. Reviewers open the review in the **Angular SPA**, where they can diff versions, leave comments, and approve.
5. The CI pipeline queries back for approval status (HTTP 200 = approved, 201 = namespace-approved, 202 = pending).

### What APIView Does

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

### What APIView Does NOT Do

- **Does not compile code.** Parsers consume already-built binaries, not source files.
- **Does not run tests.** It is a review tool, not a CI runner.
- **Does not host packages.** It is not a package registry or artifact feed.
- **Does not generate code.** It analyzes existing APIs; it does not produce SDK source.
- **Does not enforce release gates directly.** It reports approval status via HTTP status codes; the actual block/allow decision lives in the external CI/CD pipeline that queries it.
- **Does not perform runtime analysis.** It inspects the public API surface statically from metadata, not from executing the code.

---

## Solution Structure

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

## Data Model Hierarchy

```
Review (ReviewListItemModel)
│   One per package (e.g., "Azure.Storage.Blobs" for C#).
│   Fields: id, PackageName, Language, CreatedBy, IsApproved
│
└──► APIRevision (APIRevisionListItemModel)   [1 → many per Review]
    │   One per upload / version. Type is Manual, Automatic, or PullRequest.
    │   Fields: ReviewId, APIRevisionType, IsApproved, Approvers, IsReleased, ReleasedOn
    │   Carries change history (ChangeHistory[]) for audit.
    │
    └──► CodeFile (APICodeFileModel)   [1 → many per APIRevision]
             The parsed token stream stored in Blob Storage.
             Fields: Language, PackageVersion, PackageName, ContentHash
```

A **Review** is the long-lived container for a package.  
An **APIRevision** is a snapshot of the API surface at a point in time.  
A **CodeFile** is the serialized parser output (tokens + navigation + diagnostics).

---

## Backend Architecture (APIViewWeb)

**Stack:** ASP.NET Core · Cosmos DB · Azure Blob Storage · SignalR · Application Insights

### Layers

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

### Key Controllers (LeanControllers)

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

### Key Managers (business logic)

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

### Data Stores

| Store | Used For |
|---|---|
| **Azure Cosmos DB** | Reviews, API revisions, comments, pull requests, user profiles, permissions, projects, sample revisions |
| **Azure Blob Storage** | CodeFile JSON blobs, original uploaded artifacts, usage samples |
| **Azure DevOps Artifacts** | Fetching build artifacts when `/create` endpoint is used |

### Background Services (HostedServices/)

| Service | Purpose |
|---|---|
| `CopilotPollingBackgroundHostedService` | Polls for and processes AI review jobs |
| `PullRequestBackgroundHostedService` | Asynchronous PR comment posting |
| `LinesWithDiffBackgroundHostedService` | Pre-computes diff section headings |
| `QueuedHostedService` | Generic background task executor |

### External Integrations

| System | Integration |
|---|---|
| **GitHub** | OAuth login, PR comment posting (Octokit), org membership checks |
| **Microsoft Entra ID** | JWT Bearer authentication for service-to-service calls |
| **Azure App Configuration** | Dynamic feature flags and config values |
| **Azure Key Vault** | Secrets (connection strings, API keys) |
| **Application Insights** | Request telemetry, custom events, error tracking |

---

## Frontend Architecture (ClientSPA — Angular)

**Stack:** Angular 20 · PrimeNG 20 · Bootstrap 5.3 · SignalR · Monaco Editor · RxJS · Vite

The Angular SPA is the **primary UI**. It is built separately (`npm run build`) and output to `APIViewWeb/wwwroot/spa/`. The ASP.NET backend serves it as static files and acts purely as an API host.

> **Note:** There is a legacy Razor Pages frontend (`Pages/Assemblies/`) that predates the SPA. It is being phased out; see [legacy.md](legacy.md) for details.

### Routes

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

### Key Components

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

### Services & State Management

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

### Backend Communication

- **Base URL:** Configurable via `/assets/config.json`; defaults to `api/` (relative).
- **Auth:** Cookie-based with `{ withCredentials: true }`.
- **Error handling:** `HttpErrorInterceptorService` catches 403 → redirects to `/Unauthorized`.
- **Real-time:** SignalR hub at `hubs/notification` for live approval updates, AI job completion, etc.

---

## Token File Format (CodeFile)

APIView's core abstraction is a **language-agnostic token model**. Every language parser emits the same JSON structure, which the frontend renders uniformly.

### Two Formats (both supported)

| Format | Model | Status |
|---|---|---|
| **Flat (legacy)** | `CodeFileToken[]` — linear stream with `Newline` tokens as line separators | Deprecated; auto-converted on read |
| **Tree (modern)** | `ReviewLine[]` — hierarchical lines with `ReviewToken[]` per line and `Children[]` for nesting | Active; all modern parsers emit this |

### Key Fields on CodeFile

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

### Token Kinds

Modern (`ReviewToken.Kind`): `Text`, `Punctuation`, `Keyword`, `TypeName`, `MemberName`, `StringLiteral`, `Literal`, `Comment`

### ID System

- **`LineId`** (on `ReviewLine`): Uniquely identifies an API element. Comments anchor to this ID.
- **`NavigateToId`** (on `ReviewToken`): Click-to-navigate target (e.g., a type reference jumps to its definition).
- **`CrossLanguageDefinitionId`**: Bridges the same concept across SDKs (e.g., a `BlobClient` in C# and `BlobClient` in Python share an ID).

### Content Hashing

Each CodeFile gets a **SHA-256 hash** of its API surface (excluding package version, documentation, and `SkipDiff` regions). This enables O(1) comparison between revisions — if the hash matches, the API surface is identical, and approval can be carried forward without downloading the blob.

---

## Diff Pipeline

1. Two `CodeFile` blobs are loaded (active revision vs. selected diff revision).
2. `ReviewLine` trees are flattened to comparable line sequences.
3. **Myers' O(ND) diff algorithm** (`APIView/DIff/Diff.cs`) computes the minimal edit script.
4. Each line is tagged as `Added`, `Removed`, or `Unchanged`.
5. The frontend renders additions/removals inline with configurable context lines.
6. Section headings with diffs are pre-computed by `LinesWithDiffBackgroundHostedService` so the nav tree can highlight changed areas.

---

## Approval & Release Flow

### Manual Approval
A reviewer clicks **Approve** in the UI → `ToggleAPIRevisionApprovalAsync` toggles the approval state, updates the `Approvers` HashSet, records in `ChangeHistory`, and broadcasts via SignalR.

### Automatic Carry-Forward
When a new revision is created and its **content hash matches** an already-approved revision, approval is copied automatically. The change history records `"Approval copied from revision {id}"`.

### Release Tagging
When a release pipeline calls `/autoreview/upload` or `/autoreview/create` with `setReleaseTag=true`, the matching revision gets `IsReleased = true` and `ReleasedOn = DateTime.UtcNow`. The UI shows it as **"Shipped"**.

---

## Language Parsers

Many parsers run as **external processes** (via `System.Diagnostics.Process.Start()` with a 90-second timeout), but some parsing and deserialization happens **in-process** depending on the language service. Parsers live in various locations across the `azure-sdk-tools` repo.

The **Runs On** column indicates who executes the parser:
- **Server** — APIView server invokes the parser (as an external process or in-process).
- **Pipeline** — An Azure DevOps pipeline runs the parser and calls back to APIView (see [sandboxing.md](sandboxing.md)).
- **Manual** — A person or team runs the parser locally and uploads the resulting JSON token file.

| Language | Runs On | Repo Path | Input |
|---|---|---|---|
| C# | Server (external process) | `tools/apiview/parsers/csharp-api-parser` | `.dll`, `.nupkg` |
| Java | Server (external process) | `src/java/apiview-java-processor` | `.jar` |
| Python | Pipeline | `packages/python-packages/apiview-stub-generator` | `.whl` |
| JavaScript/TypeScript | Server (external process) | `tools/apiview/parsers/js-api-parser` | `.api.json` |
| Go | Server (external process) | `src/go` | `.gosource` (zip) |
| Rust | Server (external process) | `tools/apiview/parsers/rust-api-parser` | `.rust.json` |
| Swift | Manual (pre-parsed JSON uploaded) | `src/swift/SwiftAPIView` | `.json` |
| TypeSpec | Pipeline | `tools/apiview/emitters/typespec-apiview` | `.json` |
| Swagger/OpenAPI | Pipeline | `tools/apiview/parsers/swagger-api-parser` | `.json` |
| Protocol Buffers | Server (external process) | `packages/python-packages/protocol-stub-generator` | `.proto` |
| C++ | Server (in-process) | `tools/apiview/parsers/cpp-api-parser` | Compressed archive with XML AST |
| XML | Server (external process) | `src/java/apiview-java-processor` | `.xml` |
| C | Server (in-process) | *(no external parser)* | Compressed archive |
| Json | Server (in-process) | *(no external parser)* | `.json` |

---

## APIViewJsonUtility (CLI Tool)

A standalone CLI for inspecting and converting token files outside the web app:

```
APIViewJsonUtility --dumpApiText <input.json>       # Print human-readable API text
APIViewJsonUtility --convertToTree <input.json>      # Convert flat tokens → tree model
```

Useful for debugging parser output without running the full web application.

---

## Key File Paths (for agents)

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