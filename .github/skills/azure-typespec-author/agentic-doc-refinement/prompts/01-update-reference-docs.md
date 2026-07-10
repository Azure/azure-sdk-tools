# Prompt 01 — Update reference documents

You are updating the `azure-typespec-author` skill's reference documentation so that
every eval scenario maps to an authoritative TypeSpec document.

## Task

Sync `.github/skills/azure-typespec-author/references/reference-document-links.md`.

Cases (these 8 categories MUST exist, in this order):

1. Add Resource Type (ARM)
2. Add Resource Operations (ARM)
3. API Versioning
4. Long-Running Operations (LRO)
5. Paging
6. Models and Enums
7. Decorators
8. Warnings

Rules:

- Categorize every document under one of the 8 cases above.
- Do **not** include the "Migrate Swagger to TypeSpec" material.
- Avoid adding a new case/category if the topic fits an existing case. Fold niche ARM
  scenarios (private endpoints/links, network security perimeter, common types, change
  provider namespace, portal defaults, content negotiation) into the closest core case.
- Keep the existing markdown format and table structure.
- Do **not** commit, stage, or push. Leave your edits unstaged in the working tree so
  the user can review them and decide whether to commit.

## Sources (authoritative only)

1. https://azure.github.io/typespec-azure/
2. https://typespec.io/docs/

## Validation

- Every URL you add or keep must return HTTP 200.
- The 8 case headings must be present and non-empty.
