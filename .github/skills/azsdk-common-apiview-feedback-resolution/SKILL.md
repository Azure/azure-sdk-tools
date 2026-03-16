---
name: azsdk-common-apiview-feedback-resolution
license: MIT
metadata:
  version: "1.0.0"
description: "Resolve feedback from APIView reviews on Azure SDK packages. **UTILITY SKILL**. USE FOR: \"APIView comments\", \"resolve API review feedback\", \"rename type per reviewer\", \"SDK API surface changes\", \"regenerate SDK after review\". DO NOT USE FOR: general code review, non-APIView feedback, manual SDK editing. INVOKES: azure-sdk-mcp:azsdk_apiview_get_comments, azure-sdk-mcp:azsdk_typespec_customized_code_update, azure-sdk-mcp:azsdk_typespec_delegate_apiview_feedback."
compatibility:
  requires: "azure-sdk-mcp server, SDK pull request with APIView review link"
---

# APIView Feedback Resolution

## MCP Tools

| Tool | Purpose |
|------|---------|
| `azure-sdk-mcp:azsdk_apiview_get_comments` | Retrieve APIView comments |
| `azure-sdk-mcp:azsdk_typespec_customized_code_update` | Apply TypeSpec changes locally |
| `azure-sdk-mcp:azsdk_typespec_delegate_apiview_feedback` | Delegate feedback to CCA pipeline |
| `azure-sdk-mcp:azsdk_run_typespec_validation` | Validate TypeSpec changes |
| `azure-sdk-mcp:azsdk_package_generate_code` | Regenerate SDK |

## Steps

1. **Retrieve** — Get APIView URL from SDK PR, run `azure-sdk-mcp:azsdk_apiview_get_comments`.
2. **Categorize** — Group as Critical/Suggestions/Informational. See [feedback steps](references/feedback-resolution-steps.md).
3. **Resolve** — Use `azure-sdk-mcp:azsdk_typespec_customized_code_update` for TypeSpec changes (applies locally); for complex multi-file changes, delegate via `azure-sdk-mcp:azsdk_typespec_delegate_apiview_feedback`. Apply code-only fixes directly.
4. **Validate** — Run validation, regenerate SDK, build and test.
5. **Confirm** — Verify all items addressed, inform user to request re-review.

## Examples

- "Resolve the APIView comments on my SDK pull request"
- "What feedback did the API reviewer leave on my package?"

## Troubleshooting

- **No comments returned**: Verify the PR has an APIView revision link and MCP server is connected.
- **Validation fails**: Re-run `azure-sdk-mcp:azsdk_run_typespec_validation` after fixing TypeSpec errors.
- **MCP unavailable**: Review APIView comments in browser and apply fixes directly.
