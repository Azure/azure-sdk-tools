# Step 1 — Analyze user telemetry, update the skill, push a branch

You are improving the `azure-typespec-author` skill **remotely** — you have no local
checkout. Read the current files from GitHub (use the GitHub tools / `read_repo_file`)
and **push your changes to a new branch — do NOT open a pull request**. A **draft** PR
is opened later by the workflow, *only if* the benchmark clears its pass-rate threshold.

The **primary target** of this step is
`.github/skills/azure-typespec-author/references/reference-document-links.md`. **Avoid**
editing other skill markdown files (`SKILL.md`, other `references/*.md`) — touch them
**only if it is genuinely needed and necessary**, and then keep changes to the absolute
minimum.

## Step 1a — Analyze the user telemetry (if provided)

If a list of real user prompts is appended below (user telemetry, exported via WorkIQ),
**cluster them into common use cases** first, then map each cluster onto the 8 case
categories. Prioritize reference-doc additions/updates that cover the **most frequent**
user needs. If no prompts are provided, proceed from the existing reference doc alone.

A use case counts as **common only if similar prompts appear at least 5 times** in the
telemetry. **Only update the reference doc for a common case when it is not already
covered** by `reference-document-links.md`. Ignore one-off or rare prompts (fewer than 5
similar occurrences), and do not touch cases that are already adequately covered — if no
common case clears the threshold and is uncovered, change nothing.

## Step 1b — Update reference documents

Sync `.github/skills/azure-typespec-author/references/reference-document-links.md`.

The 8 case categories MUST exist, in this order:

1. Add Resource Type (ARM)
2. Add Resource Operations (ARM)
3. API Versioning
4. Long-Running Operations (LRO)
5. Paging
6. Models and Enums
7. Decorators
8. Warnings

Rules:
- Categorize every document under one of the 8 cases. Do not add a new category if the
  topic fits an existing case. Do **not** include "Migrate Swagger to TypeSpec" material.
- Keep the existing markdown/table structure. Every URL must be authoritative
  (`https://azure.github.io/typespec-azure/` or `https://typespec.io/docs/`) and reachable.

## Step 1c — Only if necessary: minimal edits to other skill markdown

**Avoid** editing other skill markdown files. Only if it is **genuinely needed and
necessary** given your reference-doc changes, make **minimal, surgical** edits to
`SKILL.md` or other skill markdown files it points at — and only where the reference
changes truly require it. Keep case numbering consistent with the 8 cases. Do **not**
touch eval fixtures, `.vally.yaml`, or pipeline files. If nothing needs changing, change
nothing.

## Output — push to a branch (NO PR)

Call **`push_skill_changes`** exactly once with:
- `branch`: `self-evolve/<short-topic>` (e.g. `self-evolve/paging-topskip`).
- `files_json`: a JSON array of **every** changed file with its **full new content**
  (`{"path": "...", "content": "..."}`).

**Do NOT call `open_skill_pull_request` or `open_draft_pr`.** The workflow runs the
benchmark against your branch and opens the draft PR itself only when the pass rate
exceeds the threshold. Report the branch name and the files you committed.

