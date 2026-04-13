# Changelog

## Unreleased

### Features Added

- Added `request-copilot-review` CLI command and `azsdk_apiview_request_copilot_review` MCP tool to submit API surface text for automated Copilot review. Accepts text directly via `--api-text` (raw or markdown-fenced) or fetches it automatically from an APIView URL via `--url`. Optional parameters: `--language`, `--base-api-text`, `--outline`, `--existing-comments`.
- Added `get-copilot-review` CLI command and `azsdk_apiview_get_copilot_review` MCP tool to retrieve the status and results of a Copilot review job by `--job-id`.
