# APIView — Agent Guidelines

## What This Is

APIView is an API review platform for the Azure SDK team. It parses SDK artifacts into a language-agnostic token model, renders them in a web UI for review/diff/approval, and integrates with GitHub PRs and CI/CD pipelines.

See [docs/overview.md](docs/overview.md) for full architecture. See [docs/legacy.md](docs/legacy.md) for legacy vs modern boundaries. See [docs/release-approval.md](docs/release-approval.md) for API approval and release gating.

## Solution Structure

| Project | Purpose |
|---------|---------|
| `APIView/` | Core library — token models, diff, rendering, language parsers |
| `APIViewWeb/` | ASP.NET Core 8 backend (Controllers → Managers → Repositories) |
| `ClientSPA/` | Angular 20 frontend SPA |
| `APIViewUnitTests/` | xUnit test suite with Moq and FluentAssertions |
| `APIViewJsonUtility/` | CLI tool for inspecting token JSON files |

## Build & Run

```powershell
# Install all dependencies and build
./install-all.ps1

# Backend
dotnet build APIView.sln
dotnet run --project APIViewWeb

# Frontend (outputs to APIViewWeb/wwwroot/spa)
cd ClientSPA
npm install
npm run build        # production build
npm start            # dev server with SSL
```

## Key Rules

- **New features target Angular SPA and tree-token model only.** Do not add features to legacy Razor Pages or flat-token parser.
- Data is stored in **Cosmos DB** (metadata) + **Azure Blob Storage** (token files).
- Language parsers use a **mixed model**: most run as **external processes** on the server host, while some (e.g., C, C++, JSON, Swagger) are parsed/deserialized **in-process**.
- The common data format is the **CodeFile** JSON token model.
- The `APIView/` core library has two namespaces: **`APIView`** (shared models, diff, utilities) and **`APIViewLegacy`** (deprecated flat-token rendering). New code goes in `APIView`.
- Follow existing Controller → Manager → Repository layering in `APIViewWeb/`.
- **After making changes, evaluate whether any files in `docs/` need updates** to stay consistent with the code.
