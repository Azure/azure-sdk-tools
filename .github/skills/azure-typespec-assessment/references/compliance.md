# Documentation-Grounded Azure Compliance

Assess whether the changed TypeSpec follows documented Azure TypeSpec patterns.
Use Agent search to retrieve authoritative reference documentation, then compare
that guidance directly with changed TypeSpec source. Do not use automated API
or lint checks, generated artifacts, or a parallel rule catalog as compliance
evidence.

## Procedure

1. Read the semantic intent and changed TypeSpec source from `evidence.json`.
2. Run the local [agentic search procedure](agentic-search.md) with the semantic
   intent, affected service/resource/interface, constraints, and
   compliance-assessment goal as its inputs.
3. Retain the selected URL, matching document section, and a short verbatim
   excerpt from the fetched content. Catalog descriptions are navigation
   metadata and cannot be reported as fetched guidance.
4. Compare the fetched requirements directly with the changed TypeSpec source.
   For every claimed match, name the exact documented template or decorator and
   the exact construct used by the changed source. Similar wire behavior, a
   legacy helper, or a suppression is not a match for a documented standard
   template.
5. Report:
   - `passed` when every applicable fetched pattern is followed;
   - `failed` with one source-linked finding per documented mismatch;
   - `not-assessed` when no relevant authoritative document exists or a selected
     document cannot be retrieved.

The bounded input contains one review item for each retained material
declaration hunk and fetched document. Return exactly one explicit decision for
every item:

- `applicable-pass` when the guidance governs the declaration and it follows
  the documented pattern;
- `applicable-fail` when the guidance governs the declaration and the source
  contradicts it;
- `not-applicable` when the document does not govern that declaration;
- `not-assessed` when retrieval or evidence is insufficient.

Each failed decision maps to one unique, declaration-specific finding with the
same document URL and exact source-change ID. Never combine mismatches from
multiple declarations into one finding.

Each assessed pattern records its document title, URL, section, concise
verbatim guidance excerpt, interpretation, observed TypeSpec evidence, and
exact TypeSpec source references. Do not copy large passages or infer
undocumented requirements.
For each failed finding, make the comparison explicit:

- **Expected** is the matching document's concise `applicableGuidance`.
- **Actual** is the matching document's observed TypeSpec `evidence`.
- **Gap** is the finding `summary` explaining the mismatch.

When the fetched page contains a directly relevant TypeSpec example, retain one
or two exact excerpts as `expectedCodeSnippets`, including the matching
document URL and section. If the requirement is a deletion, prohibition, or
otherwise has no applicable example, record `expectedCodeStatus:
not-present` and explain why. Never synthesize recommended TypeSpec.

Include one or two `codeSnippets`, each no longer than 12 lines, containing only
the declarations, decorators, base types, or operation templates that
demonstrate the mismatch. Do not copy a complete model or interface when fewer
lines prove the finding.
Do not mark a broad change set as passed until every changed declaration to
which a fetched pattern applies has been compared individually.

## Efficient Search

Reduce compliance-assessment time without replacing documentation with
hard-coded rules:

1. Derive exact search terms from changed TypeSpec symbols first: decorators,
   templates, base resource types, operation interfaces, and versioning
   constructs.
2. Use `reference-document-links.md` as the initial navigation index. Search the
   selected official library reference or how-to page before broadening to
   additional official documentation.
3. Reuse fetched content by canonical URL within the assessment run. Record the
   URL and content hash so multiple intents or PRs referencing the same page do
   not fetch and parse it repeatedly. The cache is evidence storage, not a
   compliance rule catalog.
4. Fetch independent selected URLs concurrently, then search their content
   locally for the exact changed symbols and surrounding guidance.
5. Group declarations using the same documented pattern for document search,
   then record a separate review decision for every affected declaration.
   Do not repeat the document search for each operation or collapse failed
   declaration comparisons into one finding.
6. Broaden or refine the agentic search only when the targeted pages do not
   answer the code-derived query. Stop once the fetched guidance is sufficient
   to support `passed`, `failed`, or `not-assessed`.

Record compliance time separately from toolchain setup, emitter preparation,
semantic understanding, and compatibility assessment so search improvements
can be measured.
