# APIViewWeb — Agent Guidelines

## Overview

ASP.NET Core 8 backend. Uses IWebHostBuilder pattern with Azure App Configuration and Key Vault integration.

See [CONTRIBUTING.md](CONTRIBUTING.md) and [README.md](README.md) for setup details.

## Architecture

**Controller → Manager → Repository** layering:
- **Controllers/** — HTTP endpoints. Thin; delegate to managers.
- **Managers/** — Business logic (ReviewManager, CodeFileManager, CommentsManager, etc.).
- **Repositories/** — Data access. Cosmos DB repos for metadata, Blob repos for token files.
- **Languages/** — Pluggable language services (C#, Java, Python, Go, Rust, etc.). Many parsers run as external processes; some language services deserialize in-process or are pipeline-generated (e.g., TypeSpec/Swagger).
- **Services/** — Cross-cutting concerns (Copilot auth, email, GitHub client).
- **HostedServices/** — Background workers (Copilot polling, queue processing).

## Conventions

- API contracts are defined in `typespec/` using TypeSpec. See [typespec/README.md](typespec/README.md).
- DTOs live in `DTOs/`, domain models in `Models/`, lean models in `LeanModels/`.
- Use constructor injection for dependencies.
- SignalR hub in `Hubs/` handles real-time push to clients.
- Legacy Razor Pages in `Pages/` — do not add new features there.
- **After making changes, evaluate whether `README.md` or `CONTRIBUTING.md` need updates** to stay consistent with the code.
- **Also evaluate whether any files in `../docs/` need updates** to reflect architectural or behavioral changes.
