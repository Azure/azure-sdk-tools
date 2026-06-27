# Agentic Search

## Input

- **Step 1 result** — project analysis output from [analyze-project.md](analyze-project.md): service type, existing API versions, latest version, intent, target resource/interface, and constraints.
- **User request** — the user's stated goal for this session.

## Procedure

1. **Select URLs** — read [reference-document-links.md](reference-document-links.md). Select only the URLs relevant to the user's request and Step 1 result. If unsure, select all.
2. **Fetch** — `web_fetch` each selected URL. Extract content as markdown.
   - **Timeout**: Allow at most 30 seconds per URL fetch. If a fetch times out or fails, skip it and continue with the remaining URLs.
   - **Total budget**: Complete all fetching within 2 minutes. If the budget is exceeded, stop fetching and proceed with whatever content has been collected.
3. **Search** — find content matching a query derived from the user's request and Step 1 result. Choose the most effective local search tool available.
4. **Iterate** — if initial results are insufficient, refine the query or fetch additional pages/URLs until the information satisfies the query. Respect the total time budget.
5. **Return** — provide the extracted guidance to the caller.

## Fallback

If all URL fetches fail or the total budget expires with no useful content:
- Proceed without agentic search results.
- Rely on the MCP tool (`azsdk_typespec_generate_authoring_plan`) and built-in knowledge.
- Do NOT block the workflow waiting for unreachable URLs.
