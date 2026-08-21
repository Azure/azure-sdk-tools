# Documentation-Grounded Azure Compliance

Assess whether the changed TypeSpec follows documented Azure TypeSpec patterns.
Use repository-native TypeSpec Validation as automated evidence, but do not run
an OpenAPI validation tool or maintain a parallel rule catalog in this skill.

## Procedure

1. Read each project's `validation` result in `evidence.json`. Stop and report
   blocking evidence when validation failed or was unavailable. Do not claim
   that a successful run covers patterns its linter does not inspect.
2. Read the semantic intent and changed TypeSpec source from `evidence.json`.
3. Run the shared
   [agentic search procedure](../../azure-typespec-author/references/agentic-search.md)
   with the semantic intent, affected service/resource/interface, constraints,
   and compliance-assessment goal as its inputs. Do not reimplement or shorten that
   procedure in this skill.
4. Retain the selected URL, matching document section, and a short verbatim
   excerpt from the fetched content. Catalog descriptions are navigation
   metadata and cannot be reported as fetched guidance.
5. Compare the fetched requirements directly with the changed TypeSpec source and
   generated behavior. For every claimed match, name the exact documented
   template or decorator and the exact construct used by the changed source.
   Similar wire behavior, a legacy helper, or a suppression is not a match for
   a documented standard template. Use generated artifacts only to confirm the
   effect of the source pattern.
6. Report:
   - `passed` when every applicable fetched pattern is followed;
   - `failed` with one source-linked finding per documented mismatch;
   - `not-assessed` when no relevant authoritative document exists or a selected
     document cannot be retrieved.

Each assessed pattern records its document title, URL, section, concise
verbatim guidance excerpt, interpretation, observed TypeSpec evidence, and
exact TypeSpec source references. Do not copy large passages or infer
undocumented requirements.
Do not mark a broad change set as passed until every changed declaration to
which a fetched pattern applies has been compared individually.
