# Prompt 02 — Update skill markdown (only if needed)

You maintain the `azure-typespec-author` skill in `.github/skills/azure-typespec-author/`.

## Task

Given the reference documents updated in step 1, review the skill instructions and update
them **only where the reference changes require it**:

- `SKILL.md` — the skill entry point and instructions.
- `references/intake.md` — case numbering / routing (the 8 cases in Prompt 01).
- `references/authoring-plan.md` — case-specific call-outs (e.g. spread-member `@@added`,
  ARM `Extension.*` templates, `$top`/`$skip` list parameters, warning suppression).
- Other `references/*.md` referenced by `SKILL.md`.

Rules:

- Make **minimal, surgical** changes. Do not rewrite sections that are already correct.
- Keep case numbering consistent with `reference-document-links.md` (8 cases).
- Do not touch eval fixtures, `.vally.yaml`, or pipeline files.
- If nothing needs changing, make no edits and say so explicitly.
- Do **not** commit, stage, or push. Leave your edits unstaged in the working tree so
  the user can review them and decide whether to commit.

## Validation

- `SKILL.md` still parses (valid front matter + headings).
- Any case numbers/names referenced match the 8 cases in `reference-document-links.md`.
