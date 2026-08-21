# Markdown Assessment Template

Render the report from `assessment.json` with `scripts/render-assessment.mjs`.
The Markdown is assessment-first and preserves complete evidence in appendices.
Prefix the report title and each second-level section heading with its
renderer-defined icon so the major sections are easier to scan. Keep
third-level detail headings unadorned.

## Required order

1. **Executive Summary** — dimension status table, confidence, time, intent
   count, operation count, and project count. Do not derive or display a single
   top-level assessment decision. List Semantic Understanding first, followed
   by REST compatibility, downstream compatibility, and Azure compliance.
   Display the derived `🟢 High`/`🟡 Medium`/`🔴 Low` overall code-safety
   indicator and `🟢 high`/`🟡 medium`/`🔴 low` overall confidence. Prefix
   dimension results with pass, fail, or not-assessed icons.
2. **Action Required** — severity-ordered findings with concise code and
   guidance links. State that no action is required when empty.
3. **Semantic Understanding** — begin with a Change Overview table containing
   one row per semantic intent, operation count, method/LRO/paging shape, API
   versions, linked finding count, and detail anchor. Then group complete
   operation details under an **Operation Details** heading, organized by
   intent and including confidence, concise REST summary, parameters, payloads,
   responses, service behavior, LRO, paging, and TypeSpec source. Do not render
   the internal transformation chain.
4. **Compatibility Assessment** — REST findings first, then REST-compatible
   downstream findings, or explicit evidence-backed empty results.
5. **Azure Compliance** — status and source-linked compliance findings.
6. **Appendix** — assessment errors, code-to-guidance evidence, timing,
   emitters/libraries used, repository validation, artifact evidence, and
   changed sources grouped by file. The code-to-guidance table contains the
   fetched excerpt, observed TypeSpec, result, source, and URL. Tooling
   presentation lists names only; do not expose per-run emitter status or
   output.

The summary links to details rather than duplicating long evidence. Keep the
JSON as the machine-readable source of truth. Never omit operation or source
evidence to shorten Markdown.
