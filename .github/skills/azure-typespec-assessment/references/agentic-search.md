# Agentic Search

## Input

- **Semantic intent** — action, changed constructs, up to three representative
  source excerpts, aggregate operation counts, and up to three representative
  operation IDs from `model-input.json`.
- **Compliance goal** — compare the changed TypeSpec with applicable official
  guidance without inventing requirements.

## Procedure

1. **Build query profile** — derive exact terms from the changed TypeSpec and
   semantic intent. Keep symbols such as decorators, templates, base resource
   types, operation interfaces, paging/LRO constructs, and versioning
   decorators.
2. **Score catalog** — read
   [reference-document-links.md](reference-document-links.md), score every
   document with the rubric in `design.md`, and rank every URL. Break ties by
   catalog order.
3. **Fetch** — call `web_fetch` for the four URLs concurrently and extract
   markdown. If one cannot be fetched, record the failure and replace it with
   the next-ranked URL until four documents are retrieved or the catalog is
   exhausted.
4. **Search** — search each fetched document for the query-profile terms and
   nearby normative guidance. Retain the smallest relevant section, a concise
   excerpt, and directly relevant TypeSpec examples.
5. **Compare once** — synthesize applicable fetched guidance and compare it
   with the Semantic intent as one assessment unit. Do not assess each affected
   operation or build a document-by-declaration matrix. Catalog descriptions
   select documents; they are not compliance evidence.
6. **Write search evidence** — write
   `compliance-search-evidence.json` with the unchanged query profile, complete
   catalog ranking, four fetched documents, failed attempts, score components,
   selection rationale, canonical URL, section, excerpt, applicable declaration
   IDs, relevant documented code, content hash, and retrieval timestamp. Set
   accounting to the number of catalog entries scored across all intents,
   fetched documents, fetched bytes, retained excerpts, and retained excerpt
   bytes.
7. **Judge every intent** — write exactly one `complianceDecisions` entry per
   Semantic intent. Use `applicable-pass`, `applicable-fail`,
   `no-applicable-guidance`, or `not-assessed`. Cite fetched sections only when
   they contribute to the decision. When the search completes but no fetched
   guidance governs the changed behavior, return `no-applicable-guidance` with
   changed-code evidence and a clear rationale. Reserve `not-assessed` for
   incomplete or blocked Compliance.
   Every `applicable-fail` also supplies a concise finding title and `high`,
   `medium`, or `low` severity.
   Never synthesize a requirement or recommended code example.
