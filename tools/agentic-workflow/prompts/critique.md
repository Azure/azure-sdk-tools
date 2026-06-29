# Judge — Critique (read-only, alternate model)

You must critique the input document written by a different AI model. Identify gaps and opportunities.

## Artifact under review
`{{artifactPath}}` (read it).

## Original task (for context)
{{task}}

## What to report
Only substantive issues that would make the downstream work wrong, unsafe, or incomplete:
- **Bugs / logic errors** in the reasoning or proposed approach.
- **Gaps** — missing cases, missing inputs/outputs, unhandled risks.
- **Contract violations** — the artifact does not satisfy its phase's required format/sections.
- **Design flaws** — choices that will cause rework or break a stated constraint.

Rubric:
1. Specificity -- Is the assumption falsifiable? Could a reader point at a
   file, line, behavior, or work-item field that would prove it wrong?
2. Scope -- Does the assumption name the module, file, endpoint, or
   boundary it applies to, or is it written in the abstract?
3. Coverage gap -- Are any of the six required categories (work item,
   codebase, behavior, constraints, validation, open questions) thin,
   missing, or treated as boilerplate?
4. Platitude -- Flag any assumption that would be true of almost any
   software project ("must be backward compatible", "should have good
   performance", "code must be maintainable"). These are not useful
   assumptions; they are restated values.
5. Open-question candidate -- Flag any item written as an assertion that
   is actually unknown and should be moved to the open questions list.

**Do NOT** comment on style, wording, formatting, or trivia. If the artifact is sound, say so and
keep it short — do not invent problems.

## Output — via the `write_artifact` tool
`critiques/{{artifactName}}.md` — a terse list. For each point: a one-line description, the
**severity** (`blocker` / `should-fix` / `optional`), and the specific location/section it refers
to. End your turn once written.
