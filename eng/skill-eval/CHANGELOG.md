# Changelog

## Unreleased

### Other Changes

- Upgrade both evaluator packages to Vally 0.19.0 and Node 22.14+ for the supported
  Copilot SDK stack used by scoped GitHub App-token MCP evaluations.
- Migrate six non-TypeSpec capability definitions from `config` to `defaults` and
  remove unsupported metadata from the nightly release-planner definition without
  changing prompts, models, graders or scoring.
- Add offline configured-project discovery/load checks to the existing skill CI.
  Keep Tools-specific schema coverage outside the synced shared unit tests.
- Document upgrade benefits and the unresolved compatibility blocker in the
  separately owned TypeSpec author benchmark, whose files and pipelines are unchanged.
