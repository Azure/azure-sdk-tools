# APIView — Complete Developer & User Guide

> **📖 Start Here:** This is the main entry point to APIView documentation. It provides a comprehensive overview of the system, workflows, and operations. For specialized deep-dive documents and external resources, see [Related Documentation](#related-documentation) at the end of this guide.

---

## Contents

1. [What Is APIView?](#what-is-apiview)
2. [Development Setup](#development-setup)
3. [Architecture Overview](#architecture-overview)
4. [Data Model](#data-model)
5. [Core Workflows](#core-workflows)
6. [Approval & Release Gating](#approval--release-gating)
7. [Language Support & Parsers](#language-support--parsers)
8. [Advanced Topics](#advanced-topics)
9. [Troubleshooting & FAQ](#troubleshooting--faq)
10. [Key File Paths & References](#key-file-paths--references)
11. [Related Documentation](#related-documentation)

---

## What Is APIView?

APIView is the Azure SDK team's **API review platform**. It:

- **Ingests** SDK artifacts (DLLs, JARs, wheels, etc.) or pre-parsed JSON token files representing the public API surface
- **Renders** the API in a web UI where architects and reviewers can comment, diff across versions, and track approval
- **Supports 16+ languages** (C#, Java, Python, JavaScript/TypeScript, Go, Rust, C, C++, Swift, Protocol Buffers, Swagger/OpenAPI, TypeSpec, etc.) through a common token model
- **Integrates with CI/CD** pipelines and GitHub PRs to automatically create reviews and gate releases

### What APIView Does

✓ Parses SDK artifacts into a language-agnostic token model  
✓ Renders public API surfaces with syntax highlighting and hierarchical navigation  
✓ Diffs any two revisions using Myers' O(ND) algorithm  
✓ Hosts threaded comments anchored to specific API elements  
✓ Tracks approval state with change history and audit trail  
✓ Carries forward approvals automatically when API surface is unchanged  
✓ Integrates with GitHub PRs and posts review links as comments  
✓ Supports AI-powered reviews via Copilot background service  
✓ Links APIs across languages through `CrossLanguageDefinitionId`  
✓ Marks revisions as released when release pipelines call with `setReleaseTag=true`  

### What APIView Does NOT Do

✗ Does not compile code — parsers consume already-built binaries  
✗ Does not run tests — it is a review tool, not a CI runner  
✗ Does not host packages — it is not a package registry  
✗ Does not enforce release gates directly — reports status via HTTP codes for CI/CD to act on  
✗ Does not perform runtime analysis — inspects public API statically from metadata  

---

## Development Setup

Get APIView running locally in 3 steps. No need to provision Azure resources — the backend can run in-memory/mock mode.

### Prerequisites

- **Visual Studio** 2022+ (for .NET 8+ development)
- **Node.js** 18+ (for Angular/Vite build)
- **Angular CLI** (`npm install -g @angular/cli`)
- **GitHub** membership in the Azure SDK organization
- **Local GitHub OAuth App** (create for development)

### Step 1: Create Local GitHub OAuth App

1. Go to GitHub Settings > Developer Settings > OAuth Apps
2. Create new app with:
   - **Authorization callback URL:** `http://localhost:5000/signin-github` (or your port)
3. Copy Client ID and Secret to **User Secrets** (`dotnet user-secrets set ...`)

### Step 2: Build Frontend Assets

```bash
cd ClientSPA
npm install
npm run build        # Outputs to APIViewWeb/wwwroot/spa/
# OR for development:
npm run dev          # Auto-rebuilds on changes
```

### Step 3: Run Backend

```bash
cd APIViewWeb
dotnet run           # Launches at https://localhost:5001
```

- Backend changes require server restart
- Angular changes auto-reload (with npm dev running)

### Testing & Contributing

- **Backend Unit Tests:** `APIViewUnitTests/` — run via Visual Studio or `dotnet test`
- **Angular Tests:** `ClientSPA/` — run via `npm test`
- **Contributing:** Follow standard GitHub PR workflow; backend changes modify `APIViewWeb/` or `APIView/`, frontend changes modify `ClientSPA/src/app/`
- PR requires CLA validation and passing checks

---

## Architecture Overview

> **📚 Deep Dive:** For comprehensive architecture details, component interactions, and API integration patterns, see **[overview.md](overview.md)**.

### Solution Structure

```
APIView.sln
├── APIView/              Core library — token models, rendering, diff, C# parser
├── APIViewWeb/           ASP.NET Core backend API + Angular SPA host
├── APIViewUnitTests/     Unit and integration tests
├── APIViewJsonUtility/   CLI tool for inspecting token JSON files
├── ClientSPA/            Angular frontend (built separately, output to wwwroot/spa/)
└── docs/                 Documentation
```

### Technology Stack

| Component | Technology |
|-----------|-----------|
| **Backend API** | ASP.NET Core 8+, C# |
| **Data Storage** | Cosmos DB (reviews, revisions, comments) + Azure Blob Storage (token files, artifacts) |
| **Frontend** | Angular 20, PrimeNG 20, Bootstrap 5.3, Monaco Editor |
| **Real-Time** | SignalR for approval notifications and AI job completion |
| **Authentication** | GitHub OAuth + Microsoft Entra ID (JWT Bearer for service-to-service) |
| **Build Tools** | Vite for SPA, .NET for backend |

### Backend Architecture

**Layers:**

```
HTTP Layer                  Business Logic              Data Access
─────────────────────────────────────────────────────────────────────
LeanControllers/ (14)  ──► Managers/ (14+)  ─────────► Repositories (11+)
  REST (Cookie + Token)     Core workflows             Cosmos DB (8)
  /api/[controller]         • Approvals               Blob Storage (3)
                            • Comments                DevOps Artifacts
Controllers/ (6)            • Diff
  Legacy MVC (Razor)        • Notifications
                            • PR integration
                            • AI/Copilot jobs
```

#### Key REST Controllers (LeanControllers)

| Controller | Route | Responsibility |
|---|---|---|
| `APIRevisionsController` | `api/Revisions` | Revision CRUD, approval, diff |
| `ReviewsController` | `api/Reviews` | Review listing, search, filtering |
| `CommentsController` | `api/Comments` | Comment CRUD with threads |
| `PullRequestsController` | `api/PullRequests` | PR linking |
| `ProjectsController` | `api/Projects` | Package grouping |

#### CI/CD Integration Controllers (Token Auth)

| Controller | Route | Responsibility |
|---|---|---|
| `AutoReviewController` | `autoreview/` | Upload/create endpoints for CI |
| `APIRevisionsTokenAuthController` | `api/apirevisions` | Revision queries for CI |
| `ReviewsTokenAuthController` | `api/reviews` | Review queries for CI |
| `CommentsTokenAuthController` | `api/comments` | Comment operations for CI |

#### Key Managers (Business Logic)

| Manager | Responsibility |
|---|---|
| `ReviewManager` | Review lifecycle, notifications, deletion |
| `APIRevisionsManager` | Revision creation, approval, carry-forward, hashing |
| `CodeFileManager` | Token serialization/deserialization, hashing |
| `CommentsManager` | Threaded comments, reactions |
| `PullRequestManager` | GitHub PR integration |
| `AutoReviewService` | Automatic revision creation and matching |
| `NotificationManager` | Email and SignalR |
| `PermissionsManager` | RBAC |

#### Background Services

| Service | Purpose |
|---|---|
| `CopilotPollingBackgroundHostedService` | Polls for and processes AI review jobs |
| `ReviewBackgroundHostedService` | Re-parses on version updates; auto-archives inactive revisions |
| `PullRequestBackgroundHostedService` | Asynchronous PR comment posting |
| `LinesWithDiffBackgroundHostedService` | Pre-computes diff section headings |
| `QueuedHostedService` | Generic background task executor |

### Frontend Architecture

The Angular SPA (`ClientSPA/`) is built separately and output to `APIViewWeb/wwwroot/spa/`. The backend serves it as static files and provides pure REST API.

#### Main Routes

| Route | Component | Purpose |
|---|---|---|
| `/` | `IndexPageComponent` | Home — review list with search/filter |
| `/review/:reviewId` | `ReviewPageModule` | Main review UI: code panel, comments, diff, nav |
| `/conversation/:reviewId` | `ConversationPageModule` | Copilot chat view |
| `/revision/:reviewId` | `RevisionPageModule` | Revision management |
| `/samples/:reviewId` | `SamplesPageModule` | Code sample attachments |
| `/profile/:userName` | `ProfilePageComponent` | User settings |
| `/admin/permissions` | `AdminPermissionsPageComponent` | RBAC |

#### Key Components

| Component | Responsibility |
|---|---|
| `code-panel/` | Renders tokens with syntax highlighting, line anchoring, hidden-API toggle |
| `review-page/` | Orchestrates experience: code panel + nav tree + options |
| `review-nav/` | Hierarchical navigation tree |
| `conversations/` | Threaded comment UI |
| `cross-lang-view/` | Side-by-side multi-language view |
| `revisions-list/` | Revision history |
| `reviews-list/` | Searchable review table |

#### State Management

No NgRx — uses **RxJS BehaviorSubjects** in Angular services:

| Service | Responsibility |
|---|---|
| `ConfigService` | Runtime config, theme |
| `AuthService` | Login state |
| `ReviewContextService` | Current review/language context (avoids prop drilling) |
| `SignalRService` | Real-time hub connection |
| `NotificationsService` | Notification state with IndexedDB persistence |
| `CommentsService`, `ReviewsService`, `RevisionsService` | API calls |

---

## Data Model

> **📚 Deep Dive:** For detailed CodeFile structure, token format specifications, and ID system implementation, see **[overview.md](overview.md#6-token-file-format-codefile)**.

### Hierarchy

```
Review (ReviewListItemModel)
│   One per package (e.g., "Azure.Storage.Blobs" for C#)
│   Fields: id, PackageName, Language, CreatedBy, IsApproved
│
└──► APIRevision (APIRevisionListItemModel)   [1 → many]
    │   One per upload / version
    │   Fields: ReviewId, APIRevisionType, IsApproved, Approvers, IsReleased, ReleasedOn
    │   ChangeHistory[] audit trail
    │
    └──► CodeFile (APICodeFileModel)   [1 → many]
         The parsed token stream (in Blob Storage)
         Fields: Language, PackageVersion, PackageName, ContentHash
```

### CodeFile Structure

```
CodeFile
├── Language            e.g., "C#", "Java", "Python"
├── PackageName         e.g., "Azure.Storage.Blobs"
├── PackageVersion      e.g., "12.14.0"
├── ReviewLines[]       Hierarchical token structure (modern)
├── Tokens[]            Flat token array (legacy, auto-converted)
├── Navigation[]        Tree for nav panel
├── Diagnostics[]       Parser warnings/errors
├── ContentHash         SHA-256 of API surface (excludes version, docs, SkipDiff regions)
└── CrossLanguageMetadata   Maps IDs across language SDKs
```

### Token Formats

| Format | Structure | Status | Usage |
|--------|-----------|--------|-------|
| **Tree (Modern)** | `ReviewLine[]` with `ReviewToken[]` per line and `Children[]` for nesting | Active | Modern parsers, Angular SPA |
| **Flat (Legacy)** | `CodeFileToken[]` — linear stream with `Newline` tokens | Deprecated | Auto-converted on read, legacy Razor pages |

**Token Kinds:** `Text`, `Punctuation`, `Keyword`, `TypeName`, `MemberName`, `StringLiteral`, `Literal`, `Comment`

> **📚 Legacy System:** For details on the flat-token system, Razor Pages rendering, and migration path to tree tokens, see **[legacy.md](legacy.md)**.

### ID System

- **`LineId`** — Uniquely identifies an API element. Comments anchor to this ID.
- **`NavigateToId`** — Click-to-navigate target (e.g., type reference jumps to definition).
- **`CrossLanguageDefinitionId`** — Bridges the same concept across SDKs.

### Content Hashing

Each CodeFile gets a **SHA-256 hash** of its API surface. This enables O(1) comparison — if the hash matches, the API is identical and approval can be carried forward.

---

## Core Workflows

> **📚 Deep Dive:** For detailed SDK CI pipeline integration, architecture context, and language-specific parser information, see **[overview.md](overview.md#10-core-workflows)**.

There are **three ways** API revisions reach APIView:

### Workflow A — CI Automatic (Persistent Reviews)

**Trigger:** Non-PR builds on `internal` project (merges to `main`, manual runs)  
**Template:** `eng/common/pipelines/templates/steps/create-apireview.yml` → `Create-APIReview.ps1`  
**Purpose:** Creates or updates the persistent review for a package — the long-lived record across versions.

```
SDK CI/CD Pipeline                          APIView
─────────────────                           ───────

Build artifact + (optionally) run parser
        │
        ├── Token file exists?
        │       │
        │       ├── YES (Python, JS)
        │       │   POST /autoreview/create ────► Download token file + artifact
        │       │   (build coordinates only)     from DevOps
        │       │
        │       └── NO (C#, Java, Go, Rust)
        │           POST /autoreview/upload ────► Save artifact to Blob Storage
        │           (multipart binary)           Invoke language parser on server
        │
        ▼
validate-all-packages.yml
Query approval status
  200 = approved
  201 = namespace-approved
  202 = pending
```

**Decision:** Whether to upload the binary artifact or send build coordinates depends on whether a token file exists:

| Language | Artifact | Token File? | Endpoint |
|---|---|---|---|
| C# | `.nupkg` | No | `/autoreview/upload` |
| Java | `*-sources.jar` | No | `/autoreview/upload` |
| Python | `.whl` + `_python.json` | **Yes** | `/autoreview/create` |
| JavaScript | `.api.json` + `_js.json` | **Yes** | `/autoreview/create` |
| Go | `.gosource` | No | `/autoreview/upload` |
| Rust | `.rust.json` | No | `/autoreview/upload` |

### Workflow B — CI Pull Request

**Trigger:** PR builds only (`Build.Reason == 'PullRequest'`)  
**Template:** `eng/common/pipelines/templates/steps/detect-api-changes.yml` → `Detect-Api-Changes.ps1`  
**Purpose:** Creates a PR-scoped APIView revision so reviewers see API changes the PR introduces. APIView posts a comment with the review link.

```
SDK CI/CD Pipeline                          APIView
─────────────────                           ───────

Build artifact + (optionally) run parser
Publish as pipeline artifacts
        │
        ▼
GET /api/PullRequests/
  CreateAPIRevisionIfAPIHasChanges
        │
        ├── Token file param present?
        │       YES → Store token file directly
        │       NO → Invoke language parser on server
        │
        ▼
Store in Blob Storage + Cosmos DB
(APIRevisionType = PullRequest)
        │
        ▼
Post comment on GitHub PR with link
```

### Workflow C — Manual Upload (Web UI)

**Trigger:** User action in Angular SPA  
**Endpoint:** `POST /api/Reviews` (multipart form, cookie auth via GitHub OAuth)  
**Purpose:** Ad-hoc upload for early design review or languages without CI integration (e.g., Swift).

> **Note:** Manual upload is legacy. Prefer CI workflows (Workflow A/B) for traceability, auto carry-forward, and release tagging.

---

## Approval & Release Gating

> **📚 Deep Dive:** For detailed approval workflows, code references, and release gate behavior, see **[release_approval.md](release_approval.md)**.

### Approval Levels

APIView tracks approval at three levels:

#### 1. API Revision Approval (Primary)

- **`IsApproved`** (bool) — whether the revision's API surface has been approved
- **`Approvers`** (HashSet<string>) — users who have approved
- **`ChangeHistory`** — append-only audit trail

A revision is approved when `Approvers` is non-empty. Multiple reviewers toggle independently; removing all approvers reverts the revision.

#### 2. Review-Level Approval (Legacy)

A review-scoped flag on `ReviewListItemModel.IsApproved` for first-release approval. Being phased out in favor of namespace approval.

#### 3. Namespace Approval (TypeSpec)

For TypeSpec packages, tracks approval across all SDK languages in a review group. When every language revision is approved, the namespace is auto-approved.

### GA vs. Preview Classification

Every revision is classified as **GA (stable)** or **preview (prerelease)** based on version string:

**Rules:**
- A version is **preview** if:
  1. It has a prerelease label (e.g., `-beta.1`, `-alpha`, `-rc.1`)
  2. Major version is `0` (e.g., `0.x.y`)
- Otherwise it's **GA**

**Sort Order** (descending):
```
2.0.0             ← GA
2.0.0-beta.10     ← preview
2.0.0-beta.2      ← preview
1.0.0             ← GA
```

**Distinction Matters:**

| Area | GA | Preview |
|------|----|---------| 
| **Copilot review gate** | Must have Copilot review before approval | Exempt |
| **Auto-archive** | Last approved GA revision always preserved | Most recent preview always preserved |

### Approval Prerequisites (UI Guards)

Before approving, the UI enforces (in priority order):

| # | Guard | Effect |
|---|-------|--------|
| 1 | **Missing package version** | Blocks approval |
| 2 | **Unresolved "Must Fix" comments** | Blocks approval — all must-fix must be resolved first |
| 3 | **Copilot review required** | For supported languages, must be Copilot-reviewed first (preview versions exempt) |

Backend additionally checks that user has **approver** role.

### Approval Toggle Flow

When reviewer clicks **Approve**:

1. **UI** → `POST /api/APIRevisions/{reviewId}/{apiRevisionId}` with `{ "approve": true/false }`
2. **Backend** checks authorization, calls `ToggleAPIRevisionApprovalAsync`
3. **ChangeHistoryHelpers** computes toggle:
   - If user has more "Approved" than "ApprovalReverted" entries → emit `ApprovalReverted`, remove from `Approvers`
   - Otherwise → emit `Approved`, add to `Approvers`
4. Change history entry appended (with timestamp and notes)
5. Persisted to Cosmos DB
6. **SignalR** broadcasts `ReceiveApproval` to all connected clients

### Automatic Carry-Forward

When a new automatic revision is created, its API surface is compared against existing approved revisions:

- **Fast path:** If both have `ContentHash`, comparison is O(1)
- **Slow path:** If no hash, download from Blob Storage and compare structurally; hash is back-filled

If surfaces match and source is approved, `CarryForwardRevisionDataAsync` copies the approval. Change history records `"Approval copied from revision {sourceId}"`.

### Release Gating (CI/CD Integration)

APIView does **not** enforce gates directly. Instead, it reports approval status via HTTP codes that CI/CD pipelines query:

#### Endpoint

```
GET /AutoReview/GetReviewStatus?language={lang}&packageName={pkg}
    [&packageVersion={ver}][&firstReleaseStatusOnly={bool}]
```

#### Response Codes

| HTTP | Meaning | Action |
|-----|---------|--------|
| **200** | API revision is approved | Proceed to release |
| **201** | Namespace / first-release approved | Proceed (with conditions) |
| **202** | Approval pending | Block release, wait for approval |
| **404** | No review exists | Fail — package not registered |

#### Resolution Logic

1. If `packageVersion` provided, look for matching automatic revision (exact or same major.minor)
2. If matching revision has `IsApproved == true` → **200**
3. If parent review has `IsApproved == true` OR `IsNamespaceApprovedAsync()` returns true → **201**
4. Otherwise → **202**

### Marking a Revision as Released

When release pipeline publishes, it calls:

```
POST /api/auto-reviews/upload
    setReleaseTag=true
    packageVersion={released-version}
```

This:
1. Finds existing approved revision with matching API surface
2. Sets `IsReleased = true` and `ReleasedOn = DateTime.UtcNow`
3. Revision displays as **"Shipped"** in UI

### End-to-End Lifecycle

```
CI Build Pipeline
  │  POST /autoreview/upload (artifact)
  ▼
APIView creates/matches APIRevision
  │  Carry-forward approval if unchanged
  ▼
Reviewers approve in Angular SPA
  │  Guards: version present, no must-fix, Copilot reviewed
  ▼
Release Pipeline
  │  GET /AutoReview/GetReviewStatus → 200 OK
  ▼
Package published
  │  POST /autoreview/upload (setReleaseTag=true)
  ▼
Revision marked "Shipped"
```

---

## Language Support & Parsers

### Supported Languages

APIView supports **16+ languages** through per-language parsers:

| Language | Repo Path | Input | Token Format | Server Parse? |
|---|---|---|---|---|
| **C#** | `tools/apiview/parsers/csharp-api-parser` | `.dll`, `.nupkg` | Tree | Yes |
| **Java** | `src/java/apiview-java-processor` | `.jar` | Tree | Yes |
| **Python** | `packages/python-packages/apiview-stub-generator` | `.whl` | Tree | Yes |
| **JavaScript/TypeScript** | `tools/apiview/parsers/js-api-parser` | `.api.json` | Tree | Yes |
| **Go** | `src/go` | `.gosource` (zip) | Tree | Yes |
| **Rust** | `tools/apiview/parsers/rust-api-parser` | `.rust.json` | Tree | Yes |
| **Swift** | `src/swift/SwiftAPIView` | `.json` | Tree | No |
| **TypeSpec** | `tools/apiview/emitters/typespec-apiview` | `.tsp`, `.cadl` | Tree | No |
| **Swagger/OpenAPI** | `tools/apiview/parsers/swagger-api-parser` | `.swagger` | Flat | No |
| **C++** | `tools/apiview/parsers/cpp-api-parser` | `.cppast` | Flat | Yes |
| **C** | *(in-process)* | `.zip` | Flat | Yes |
| **XML** | `src/java/apiview-java-processor` | `.xml` | Flat | Yes |
| **Protocol Buffers** | `packages/python-packages/protocol-stub-generator` | `.yaml` | Flat | Yes |
| **JSON (generic)** | *(in-process)* | `.json` | Flat | Yes |

### Parser Styles

Modern parsers (C#, Java, Python, JS, Go, Rust, Swift, TypeSpec) emit **tree-style tokens** (`ReviewLine[]`) and are rendered in the Angular SPA.

Legacy parsers (C, C++, Swagger, Protocol Buffers, XML, JSON) emit **flat-style tokens** (`CodeFileToken[]`) and fall back to legacy Razor rendering or are being migrated.

> **📚 Legacy System & Migration:** For the flat-token rendering pipeline, Razor Pages system, and migration roadmap to tree tokens, see **[legacy.md](legacy.md)**.

### Sandboxing (Deprecated)

TypeSpec and Swagger use **sandboxing**: the parser runs in an Azure DevOps pipeline instead of on the APIView server.

> **⚠️ Deprecated Pattern:** For details on sandboxing implementation, limitations, and why this pattern is no longer recommended for new languages, see **[sandboxing.md](sandboxing.md)**.

---

## Advanced Topics

### Diff Algorithm

The diff pipeline:

1. Two `CodeFile` blobs are loaded (active vs. selected diff revision)
2. `ReviewLine` trees are flattened to comparable line sequences
3. **Myers' O(ND) algorithm** (`APIView/DIff/Diff.cs`) computes minimal edit script
4. Each line tagged as `Added`, `Removed`, or `Unchanged`
5. Frontend renders inline with configurable context lines
6. Section headings with diffs pre-computed by `LinesWithDiffBackgroundHostedService`

### Cross-Language Support

**`CrossLanguageDefinitionId`** maps the same API concept across SDKs. For example, a `BlobClient` class in C#, Java, and Python share a cross-language ID, enabling the **"Cross-Language View"** component to show them side-by-side.

### Real-Time Notifications

**SignalR Hub** (`hubs/notification`) broadcasts:
- Approval state changes (`ReceiveApproval`)
- AI job completion (`ReceiveAIJobResult`)
- New comments (`ReceiveCommentUpdate`)

Connected clients update in real-time without polling.

### AI-Powered Reviews (Copilot)

- `CopilotPollingBackgroundHostedService` polls for pending AI review jobs
- Supported languages can submit revisions for Copilot review
- Copilot review is a prerequisite for approving GA revisions (see [GA vs. Preview Classification](#ga-vs-preview-classification))
- AI comments are tracked in change history

### Storage Strategy

| Data | Store | Rationale |
|------|-------|-----------|
| Reviews, revisions, comments, users, permissions | Cosmos DB | Query & transactional consistency |
| CodeFile JSON blobs, uploaded artifacts, samples | Azure Blob Storage | Large files, binary storage |
| Build artifacts (CI workflow) | Azure DevOps Artifacts | Integration with SDK pipelines |

---

## Troubleshooting & FAQ

### Q: How do I test my parser changes locally?

**A:** 
1. Build your parser (language-specific binary)
2. Manually upload a test artifact through the web UI
3. APIView invokes your parser and shows results
4. Check the revision's diagnostics for parser errors

### Q: Why didn't my approval carry forward?

**A:**
- Approval only carries forward if the API surface is **identical** (same content hash)
- If any public member changed, approval is **not** carried forward — a new approval is required
- Check the revision's change history to see why hash didn't match

### Q: Can I approve a revision without Copilot review?

**A:**
- **GA versions:** No — Copilot review is required (unless your language doesn't support it)
- **Preview versions:** Yes — preview is exempt from Copilot requirement

### Q: How do I mark a revision as released?

**A:**
- Release pipelines call `POST /autoreview/upload?setReleaseTag=true`
- Only happens when a package is actually published to a registry
- Once marked, the revision displays as **"Shipped"**

### Q: How can I debug CI/APIView integration?

**A:**
1. Check the CI build logs for the `Create-APIReview.ps1` or `Detect-Api-Changes.ps1` step
2. The output shows the HTTP POST call to APIView and the response status
3. Verify your revision appears in APIView (may take a few seconds)
4. Check the revision's diagnostics for parser errors

### Q: What does "Namespace Approved" mean?

**A:**
- For TypeSpec projects, the namespace (e.g., `Azure.Storage.Blobs`) is approved across all SDK languages
- Returned as HTTP 201 from `/AutoReview/GetReviewStatus`
- Allows some pipelines to proceed with release if namespace is approved, even if individual revision isn't

### Q: Why is my parser timing out?

**A:**
- Check the revision's diagnostics for partial output
- Parser may be consuming too much memory or hitting resource limits on the server
- For CI-based parsers, check the pipeline logs
- For server-side parsers, check Application Insights for server errors

---

## Key File Paths & References

### Core Models & Logic

| Path | Purpose |
|---|---|
| `APIView/Model/CodeFile.cs` | Token file model |
| `APIView/Model/V2/ReviewLine.cs` | Modern hierarchical line model |
| `APIView/Model/V2/ReviewToken.cs` | Token definition |
| `APIView/DIff/Diff.cs` | Myers' diff algorithm |
| `APIView/Languages/CodeFileBuilder.cs` | C# parser |

### Backend — Controllers

| Path | Purpose |
|---|---|
| `APIViewWeb/LeanControllers/` | REST controllers for SPA (14) |
| `APIViewWeb/Controllers/` | Legacy MVC controllers |
| `APIViewWeb/LeanControllers/AutoReviewController.cs` | CI endpoints |

### Backend — Business Logic

| Path | Purpose |
|---|---|
| `APIViewWeb/Managers/` | Business logic (14+ managers) |
| `APIViewWeb/Repositories/` | Data access (Cosmos, Blob, DevOps) |
| `APIViewWeb/HostedServices/` | Background services |
| `APIViewWeb/Languages/` | Language-specific services |

### Frontend — SPA

| Path | Purpose |
|---|---|
| `ClientSPA/src/app/` | Angular root |
| `ClientSPA/src/app/_components/` | UI components |
| `ClientSPA/src/app/_services/` | API services |
| `ClientSPA/src/app/_models/` | Data models |
| `APIViewWeb/wwwroot/spa/` | Built SPA output |

### Configuration & Build

| Path | Purpose |
|---|---|
| `APIView.sln` | Solution file |
| `APIViewWeb/Program.cs` | Backend entry point |
| `APIViewWeb/appsettings.json` | Configuration template |
| `ClientSPA/vite.config.ts` | Vite build config |
| `ClientSPA/package.json` | Node.js dependencies |

### Pipelines & Scripts

| Path | Purpose |
|---|---|
| `eng/common/pipelines/templates/steps/create-apireview.yml` | Workflow A: automatic review |
| `eng/common/pipelines/templates/steps/detect-api-changes.yml` | Workflow B: PR review |
| `eng/common/pipelines/templates/steps/validate-all-packages.yml` | Approval status check |
| `eng/scripts/` | Helper scripts |

### Tests

| Path | Purpose |
|---|---|
| `APIViewUnitTests/` | Backend tests |
| `ClientSPA/src/app/_tests/` | Frontend tests |

---

## Related Documentation

This guide references the following documents and resources:

### Local Documentation Files

| Document | Purpose | When to Use |
|----------|---------|-------------|
| **[overview.md](overview.md)** | Comprehensive architecture reference with code paths, component deep-dives, and technical implementation details | Deep technical investigation, adding new features, understanding internal APIs |
| **[release_approval.md](release_approval.md)** | Detailed approval workflows, approval gates, and release pipeline integration | Implementing approval logic, troubleshooting release gates, understanding approval state |
| **[legacy.md](legacy.md)** | Legacy Razor Pages system, flat-token rendering pipeline, and migration to tree tokens | Working with legacy languages (C, C++, Swagger), understanding backward compatibility |
| **[sandboxing.md](sandboxing.md)** | Pipeline-based parser execution pattern and deprecation status | Understanding TypeSpec/Swagger generation, learning what NOT to do for new languages |

### External Resources

| Resource | Purpose | Audience |
|----------|---------|----------|
| **[GitHub Wiki](https://github.com/Azure/azure-sdk-tools/wiki/APIView)** | Setup, development workflow, local development environment | Developers setting up locally |
| **[Internal Wiki](https://dev.azure.com/azure-sdk/internal/_wiki/wikis/internal.wiki/356/ApiView)** | Internal processes, team guidelines, service operations | Microsoft-internal team members |
| **[DevEx Docs](https://eng.ms/docs/products/azure-developer-experience/support/apiview)** | User guide and feature documentation | APIView users |
| **[DevEx Troubleshooting](https://eng.ms/docs/products/azure-developer-experience/support/troubleshoot/apiview-troubleshoot)** | Common issues and solutions | Users experiencing problems |

---

**Last Updated:** 2026-04-14  
**Applies to:** APIView Angular SPA system (modern tree-token architecture)
