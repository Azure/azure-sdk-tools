# Agentic Search

## Input

- **URLs** — the set of documentation URLs already selected in Step 2.1 (matched from [reference-document-links.md](reference-document-links.md)).
- **Step 1 result** — project analysis output from [analyze-project.md](analyze-project.md): service type, existing API versions, latest version, intent, target resource/interface, and constraints.
- **User request** — the user's stated goal for this session.

## Procedure

1. **Fetch** — retrieve the content of **every** input URL using `web_fetch` tool, one at a time, and extract it as markdown. This is required — do **not** skip it, do **not** answer from prior/internal knowledge, and do **not** assume the content. If a request maps to multiple cases, you must fetch the URLs for **all** matched cases. Every authoring decision in Step 3 must be grounded in content you actually fetched here.
2. **Search** — find content matching a query derived from the user's request and Step 1 result. Choose the most effective local search tool available.
3. **Iterate** — if initial results are insufficient, refine the query or fetch additional pages (e.g. linked URLs from [reference-document-links.md](reference-document-links.md)) until the information satisfies the query.
4. **Return** — provide the extracted guidance to the caller, with the fetched source URL recorded for each piece of guidance.

> **Failure mode to avoid:** producing an authoring plan without having fetched any documentation. If you have not fetched at least one URL for the matched case(s), STOP and fetch before continuing.
