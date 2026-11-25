---
applyTo: "tools/sdk-ai-bots/**"
---

# Chatbot Package Versioning, Env Tags, and Changelog

When modifying or reviewing packages under `tools/sdk-ai-bots/`, bump only the version, `DOCKER_IMAGE_TAG`, and `CHANGELOG.md` entries for the package(s) that actually have changes.

## Rules

1. **`azure-sdk-qa-bot-agent` (backend)**: When modifying `tools/sdk-ai-bots/azure-sdk-qa-bot-agent/`, bump `_version.py` and add an entry to `CHANGELOG.md`.
2. **`azure-sdk-qa-bot` (frontend)**: Only bump `package.json` and `DOCKER_IMAGE_TAG` in `env/.env.dev`, `env/.env.preprod`, and `env/.env.prod` when `tools/sdk-ai-bots/azure-sdk-qa-bot/` itself has changes.
3. **PR Reviews**: When reviewing chatbot PRs, flag missing version bumps, env tag updates, or `CHANGELOG.md` entries only for the modified package(s).
