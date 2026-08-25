# Markdown Assessment Template

Render the report from `assessment.json` with `scripts/render-assessment.mjs`.
The Markdown is assessment-first and preserves complete evidence in appendices.
Prefix the report title and each second-level section heading with its
renderer-defined icon so the major sections are easier to scan. Keep
third-level detail headings unadorned.

## Required order

1. **Executive Summary** — dimension status table, confidence, total assessment
   time, intent count, operation count, and project count. Keep dimension-level
   timing out of individual PR reports and record it in the aggregate execution
   timing report. Mark approximate total timing explicitly. Do not
   derive or display a single top-level assessment decision. List Semantic
   Understanding first, followed by REST compatibility, downstream
   compatibility, and Azure compliance.
   Display the derived `🟢 High`/`🟡 Medium`/`🔴 Low` overall code-safety
   indicator and `🟢 high`/`🟡 medium`/`🔴 low` overall confidence. Prefix
   dimension results with pass, fail, or not-assessed icons.
2. **Action Required** — severity-ordered findings with concise code and
   guidance links. State that no action is required when empty.
3. **Semantic Understanding** — show one compact entry per semantic intent.
   Classify every structured change as `➕ Added`, `✏️ Modified`, or
   `➖ Removed`. Put the change icon/type, aspect, Before, and After values in
   one summary table, then show the actual TypeSpec source hunk in a
   fenced `diff` block.
   Immediately before the diff, add one concise **TypeSpec change** sentence
   explaining what the source edit does. Do not add a separate diff label: the
   fenced block and its `---` and `+++` headers already identify the content
   and file. Show at most two relevant hunks per semantic change and only a
   short source-range-centered excerpt from a large hunk. Retain every complete
   hunk in JSON and state when additional hunks were omitted. Add `Impact`
   links for linked REST breaking, downstream breaking, or compliance findings;
   otherwise omit effect text. Place exact source links immediately after the
   diff.
   After all key changes, directly tell the reader how to retrieve complete
   REST details and emit one copyable prompt for every affected operation. Use
   PR context when available; otherwise identify the baseline and current
   working tree or head commit. Do not add a separate details section or render
   complete operation cards in Markdown.
4. **Compatibility Assessment** — REST findings first, then REST-compatible
   downstream findings, or explicit evidence-backed empty results.
5. **Azure Compliance** — status and source-linked compliance findings. Keep
   **Gap** visible. Put **Expected** and **Actual** in separate default-closed
   `<details>` sections. Expected contains the requirement, matching guidance
   link, and exact documented TypeSpec example when available. Actual contains
   observed behavior and one or two focused, source-linked snippets. State when
   guidance has no applicable example; never generate expected code. Do not
   repeat the fetched excerpt outside the appendix.
6. **Appendix** — assessment errors, code-to-guidance evidence,
   emitters/libraries used, artifact evidence, and no repeated changed-source
   inventory. The code-to-guidance table contains the fetched excerpt, observed
   TypeSpec, result, source, and URL. Tooling presentation lists names only; do
   not expose per-run emitter status or output. The HTML report additionally
   places its detailed execution-time breakdown in the Appendix; Markdown
   continues to show only total assessment time in the header.

The JSON is the machine-readable source of truth and retains complete operation
and source evidence. Markdown intentionally omits exhaustive REST details.
