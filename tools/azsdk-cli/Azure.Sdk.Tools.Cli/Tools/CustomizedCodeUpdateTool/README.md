# TspClientUpdate Tool

This tool provides a CLI and MCP entrypoint to automate updating customized client code when TypeSpec-generated code changes. It is language-agnostic — the system supports all languages via pluggable `IUpdateLanguageService` implementations.

---

## Overview

This tool runs the `customized-update` workflow: regenerate TypeSpec output, diff old vs new generated code, map API changes to customization files, propose patches, and (optionally) apply them.

---

## Quick CLI examples

Run from the repository root (uses the `Azure.Sdk.Tools.Cli` project):

```powershell
# Notes
- When invoking via `dotnet run`, put `--` before the application arguments so they are forwarded to the app.
- The tool is registered under the `tsp` command group; the full invocation is `tsp customized-update`.
- The tool currently requires `--stage` (one of: `regenerate`, `diff`, `apply`, `all`).

# Full workflow: regenerate -> diff -> map -> propose -> apply (dry-run)
dotnet run --project Azure.Sdk.Tools.Cli/Azure.Sdk.Tools.Cli.csproj -- tsp customized-update path/to/spec.tsp --stage all --package-path ./sdk/yourpkg --new-gen ./tmpgen

# Run a specific stage (diff only)
dotnet run --project Azure.Sdk.Tools.Cli/Azure.Sdk.Tools.Cli.csproj -- tsp customized-update path/to/spec.tsp --stage diff --package-path ./sdk/yourpkg --new-gen ./tmpgen

# Dry-run apply then finalize
dotnet run --project Azure.Sdk.Tools.Cli/Azure.Sdk.Tools.Cli.csproj -- tsp customized-update path/to/spec.tsp --stage apply --package-path ./sdk/yourpkg --new-gen ./tmpgen --finalize
```

---

## Options

- `spec-path` (argument): Path to the .tsp specification.
- `--stage`: `regenerate` | `diff` | `apply` | `all`.
- `--new-gen`: Directory to write new TypeSpec generation output (default: `./tmpgen`).
- `--resume`: Resume existing in-memory session state.
- `--finalize`: When applying, perform final (non-dry-run) apply if a dry-run occurred.

---

## Programmatic / MCP usage

The MCP entrypoint is `azsdk_tsp_update`, which calls `UnifiedUpdateAsync(specPath, IUpdateLanguageService, ...)`. Language-specific logic is provided via `IUpdateLanguageService` implementations.

## Architecture

The tool composes three small services at runtime:

- `ILanguageRepoService` — detects language, locates package files, and provides repo-scoped helpers (paths, read/write).
- `IUpdateLanguageService` — language-specific pipeline: build symbol models, map diffs, propose patches, dry-run apply, and validate.
- `Func<string, IUpdateLanguageService>` resolver — DI-registered factory that uses the repo service's `Language` to instantiate the right update service (via DI / ActivatorUtilities).

Keep the responsibilities separated: repo services give metadata and file helpers; update services own mapping, patch generation, and validation.

### Adding a new language (quick checklist)

1. Implement `ILanguageRepoService` for the language and override `Language => "yourlang"`.
2. Implement `IUpdateLanguageService` (derive from a base if available) and accept the repo service in the constructor.
3. Register the concrete update service in `ServiceRegistrations.cs` so the DI resolver can create it.
4. Add a branch in the resolver switch that maps the repo service's `Language` to the concrete update service type.

Testing note: unit tests can inject a `Func<string, IUpdateLanguageService>` that returns a mocked service for a package path; integration tests should run the full pipeline against small sample repos.

---

## Session model

The tool keeps an in-memory `UpdateSessionState` during a run. Key fields:

- `SessionId`, `SpecPath`, `OldGeneratedPath`, `NewGeneratedPath`
- `ApiChanges`, `ImpactedCustomizations`, `ProposedPatches` (stage outputs)
- `LastStage`, `Status`, `RequiresFinalize`

These fields are used to coordinate stages and to provide a response payload for inspection or automation.

---

## Troubleshooting

- Ensure the `--new-gen` path is writable and exists.
- If language detection fails, confirm `SharedOptions.PackagePath` points to a valid package root.
- For tests, see `Azure.Sdk.Tools.Cli.Tests` in the test project.
