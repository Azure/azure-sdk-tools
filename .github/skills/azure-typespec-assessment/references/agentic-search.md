# Agentic Search

## Input

- **Step 1 result** — project analysis output from [analyze-project.md](analyze-project.md): service type, existing API versions, latest version, intent, target resource/interface, and constraints.
- **User request** — the user's stated goal for this session.

## Procedure

1. **Select URLs** — derive exact search terms from the changed TypeSpec
   decorators, templates, base resource types, operation interfaces, and
   versioning constructs. Read
   [reference-document-links.md](reference-document-links.md) and select the
   smallest relevant URL set. Do not select the entire catalog when the exact
   symbols identify a topic.
2. **Fetch** — `web_fetch` each selected URL concurrently. Extract content as
   markdown and reuse content already fetched for the same canonical URL in the
   current run.
3. **Search** — search the fetched content for the exact TypeSpec symbols and
   surrounding guidance. Retain nearby fenced TypeSpec examples as candidates,
   preserving the page URL and section. Choose the most effective local search
   tool available.
4. **Iterate** — only if the targeted results are insufficient, refine the query
   or fetch the next most relevant catalog page. Search beyond the catalog only
   when it does not contain an applicable authoritative page.
5. **Return** — provide the extracted guidance and directly relevant documented
   code examples to the caller. State explicitly when the selected guidance has
   no applicable code example; never generate one.
