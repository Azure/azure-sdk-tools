# Changelog

## 0.6.31 (2026-07-24)

### Features Added

- Added `request-copilot-review` CLI command and `azsdk_apiview_request_copilot_review` MCP tool to submit API surface text for automated Copilot review. Accepts text directly via `--api-text` (raw or markdown-fenced) or fetches it automatically from an APIView URL via `--url`. Optional parameters: `--language`, `--base-api-text`, `--outline`, `--existing-comments`.
- Added `get-copilot-review` CLI command and `azsdk_apiview_get_copilot_review` MCP tool to retrieve the status and results of a Copilot review job by `--job-id`.

### Bugs Fixed

- Updated `GitHub.Copilot.SDK` to 1.0.8 so Copilot-backed commands accept ISO-8601 `ping` timestamps returned by current Copilot CLI versions.
- Fixed release plan SDK details update to avoid marking a language as missing emitter config when the TypeSpec parser did not detect any package name.
- Fixed custom-code-only SDK repair to classify feedback without requiring a local TypeSpec project path.
