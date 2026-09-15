# Agentic Search

## Input

- **Semantic intent** — action, changed constructs, up to three representative
  source excerpts, aggregate operation counts, and up to three representative
  operation IDs from `model-input.json`.
- **Azure Guidelines goal** — compare the changed TypeSpec with applicable official
  guidance without inventing requirements.

## Procedure

1. **Build query profile** — derive exact terms from the changed TypeSpec and
   semantic intent. Keep symbols such as decorators, templates, base resource
   types, operation interfaces, paging/LRO constructs, and versioning
   decorators.
   For changes to an existing versioned API, consider **Evolving APIs** first
   for versioning implications, even when no version decorator changed.
   Distinguish ARM list templates from data-plane paging decorators; do not
   transfer template requirements between service planes.
2. **Score catalog** — read
   [reference-document-links.md](reference-document-links.md), score every
   document with the rubric in `design.md`, and rank every URL. Break ties by
   catalog order.
   Obtain canonical titles, URLs, and `catalogOrder` values with the exported
   `readComplianceCatalog()` in `scripts/compliance-assessment.mjs`; preserve
   them exactly instead of reconstructing metadata or renumbering entries.
   This helper only reads catalog metadata; scoring and judgment remain Agent
   work.
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
   select documents; they are not Azure Guidelines evidence.
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
   an incomplete or blocked Azure Guidelines assessment.
   Every `applicable-fail` also supplies a concise finding title and `high`,
   `medium`, or `low` severity.
   Never synthesize a requirement or recommended code example.

## Suppressions

For a changed construct with `#suppress` in the supplied source evidence,
retain the diagnostic code and justification as context. A suppression only
silences a diagnostic; it neither proves compliance nor automatically creates
a finding. Compare the construct with fetched guidance, including any
documented exception and its conditions. Cite an unmet requirement for a
failure; do not treat a justification alone as an exemption. If the relevant
source or guidance is unavailable, record that limitation rather than infer
approval. Do not run lint or search unrelated source to judge suppressions.
