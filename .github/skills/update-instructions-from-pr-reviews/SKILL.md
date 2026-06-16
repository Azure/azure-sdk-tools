---
name: update-instructions-from-pr-reviews
description: 'Mine PR review comments and propose edits to .github/ customization files so Copilot''s reviewer catches recurring issues earlier. Clusters comments into themes, scopes to specific reviewers on request, and cites source PRs in every proposed rule. WHEN: "update instructions from PR reviews", "tune copilot reviewer from past PRs", "mine PR feedback into rules", "bootstrap .github from past PRs", "what should copilot-instructions say based on review history".'
---

# Update Instructions From PR Reviews

Turn a repo's past code-review feedback into Copilot/agent customization updates so future PRs get the same feedback, automatically.

## Requirements

- **Node ≥ 24** — scripts are TypeScript and run directly via the built-in type stripping in Node 24 LTS.
- **`gh` CLI** — installed and authenticated (`gh auth login`).

## Inputs to gather

Ask the user (use the ask-questions tool if available) before fetching:

| Input                   | Default                                                                               | Notes                                                                                                                                                                                                                                                       |
| ----------------------- | ------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Target repo**         | Current working directory's git remote                                                | Accept `owner/repo`, a local path, or "this repo". Resolve to `owner/repo` with `gh repo view --json nameWithOwner -q .nameWithOwner` if needed. **Before fetching, run the upstream check below — mining a fork or sandbox/mirror repo is almost always the wrong target.** |
| **PR set**              | Last 50 merged PRs                                                                    | Accept: explicit numbers, a range, `--since YYYY-MM-DD`, a label, an author, or a `gh pr list` search query.                                                                                                                                                |
| **Comment scope**       | Inline review comments + review-summary bodies + issue-style PR conversation comments | Treat all three as review signal by default.                                                                                                                                                                                                                |
| **Output target**       | Propose edits to existing files only                                                  | Optionally allow creating new `.instructions.md` files when a theme doesn't fit anywhere.                                                                                                                                                                   |
| **Local `.github/`**    | `.github/` in the current working directory                                           | The local directory containing the customization files to update. The user is iterating on their own copy of these files — always read from and write to this local path. **Never** fetch `.github/` contents from the remote repo via `gh api` or similar. |
| **Replay target repo** | No default                                                                             | **Ask explicitly when running `replay-pull-request.ts`.** The source PR repo (`--repo`) and the test PR destination are not always the same. Confirm where test PRs should be created before running validation steps.                                       |

Resolve the repo, paths, and PR set from the current workspace and the user's answers instead of assuming fixed values.

### Upstream / fork check (run before fetching)

PR review history almost always lives on the **upstream** repo, not on forks or
downstream sandbox/mirror repos, so mining the wrong repo wastes time. 

**Before** running `fetch-prs.ts`, confirm the resolved target isn't a fork or mirror by following
[upstream-fork-check.md](./references/upstream-fork-check.md). 

**Never silently mine a fork or mirror** — always confirm.

## Workflow

Before starting, create a TODO list (or write `./plan.md`) covering steps 1–6 below. Mark each item complete as you finish it. Each step has a stated completion check (a count, a ratio, a table, a diff, a summary) — don't tick the box until you can show it.

All bulk PR work runs through the bundled scripts under [scripts/](./scripts/). They shell out to `gh` and cache to disk so re-runs are cheap and don't bloat agent context. Each script has its own `--help` with full flag reference. `fetch-prs.ts` resolves the repo from the current working directory's git remote by default; pass `--repo owner/repo` to override, and confirm the resolved repo with the user if it isn't what they expected.

### 1. List and fetch PR data

```bash
node ./scripts/fetch-prs.ts --state merged --limit 50 --concurrency 8 --quiet
```

For a single PR, use `--number <n>`. Run `node ./scripts/fetch-prs.ts --help` for filters (`--since`, `--label`, `--repo`, `--cache-dir`, `--format summary`, `--force`).

Default cache path: `pr-cache/<owner>-<repo>/pr-<n>.json`. Files are skipped on re-run unless `--force` is passed.

**Completion check:** report PR count and a sample title list to the user.

### 2. Filter low-signal noise

Run [filter-comments.ts](./scripts/filter-comments.ts) to drop bots, `LGTM`/emoji/quoted-only, and short comments. Issue-style PR conversation comments and PR author self-comments are included by default.

**When you need to read comments in-context** (for clustering, or to show the user), pipe through `format-comments.ts` for compact markdown. See [comment-format.md](./references/comment-format.md) for the output template and citation rules.

```bash
node ./scripts/filter-comments.ts --glob "pr-cache/owner-repo/pr-*.json" \
  | node ./scripts/format-comments.ts
```

**For mining "what do reviewers ask for"** (the typical case for this skill),
the most useful invocation is:

```bash
node ./scripts/filter-comments.ts \
  --glob "pr-cache/owner-repo/pr-*.json" \
  --since 2026-05-08 --source inline --kind ask --min-length 30 \
  > kept.json
```

That single command applies all the filtering in-process, so you don't have to
pre-filter or post-process the cache yourself:

- `--since / --until` — skip PRs by merge date.
- `--source inline` — drop the multi-section review summaries that pollute
  keyword clustering.
- `--kind ask` — keep only reviewer-asks; drop `summary` (review overviews)
  and `reply` (author acknowledgements like "Fixed in abc123").
- Known automation accounts (`azure-sdk`, `copilot-swe-agent`,
  `copilot-pull-request-reviewer`) and boilerplate markers
  (`<!-- #comment-cli-pr -->`, `<!-- install-instructions -->`) are dropped
  automatically. Pass `--no-default-bots` to disable.

`filter-comments.ts` writes JSON to stdout; it has no `--output` flag. Redirect
stdout to `kept.json` (as shown above) before Step 3.

**Completion check:** report the kept/dropped ratio to the user, including
the new `kindFiltered`, `sourceFiltered`, and `prSkipped` counters when
those filters are in use, and confirm whether the clustering input is the
saved `kept.json` file or a direct pipe.

**When broadening coverage to many PRs**, follow the PR batch selection workflow in [pr-batch-selection.md](./references/pr-batch-selection.md) to pick high-signal candidates in batches and run them in parallel.

### 3. Cluster into themes (agent judgment)

Read `kept.json` and first do a reviewer-ask audit: the script is heuristic, so
drop any comments that survived filtering but are clearly author replies,
implementation-status updates, or justifications (for example, "Intentional",
"Not taken", "Accepted", "Agreed", "You're right", "yes, added ..."). Do not
count those comments toward the promotion threshold. If they clarify a real
reviewer concern, trace the rule back to the original reviewer ask; otherwise
mark the topic as low-signal/no rule.

Then group the audited reviewer asks by recurring topic. Useful axes:

- **File / path patterns** (e.g. `*_test.go`, `cmd/**`, `bicep/**`)
- **Phrase keywords** ("context", "cancellation", "error wrap", "nil check", "logging", "secret", "telemetry", "retry", "permission", "i18n", "accessibility")
- **Reviewer** (do certain reviewers consistently flag the same class of issue?)

Produce a table like:

| Theme                                    | Example                                                          | PRs | Suggested home                               |
| ---------------------------------------- | ---------------------------------------------------------------- | --- | -------------------------------------------- |
| Missing `ctx.Err()` check after long ops | "you should check ctx after this loop" (#7012, #7034)            | 4   | `.github/instructions/go.instructions.md`    |
| Error returned without wrapping          | "wrap with `fmt.Errorf(\"...: %w\", err)`" (#6998, #7021, #7045) | 6   | `.github/copilot-instructions.md`            |
| Bicep secrets passed as plain string     | "use `@secure()`" (#7001, #7029)                                 | 2   | `.github/instructions/bicep.instructions.md` |

**Promotion threshold:** a theme is worth a rule when it appears in **≥ 2 PRs** from **≥ 2 distinct reviewers**, or when a single reviewer flags it ≥ 3 times.

### 4. Inventory existing customizations

Scan the **local** `.github/` directory (the path gathered in the Inputs step) for existing customization files. Use `file_search`, `read_file`, `grep_search`, or `list_dir` on the local filesystem — **never** use `gh api`, `git show`, or any other method to read these files from the remote repo. The user is iterating on a local copy of these files, and that local copy is the source of truth.

| Path                                     | Purpose                              |
| ---------------------------------------- | ------------------------------------ |
| `.github/copilot-instructions.md`        | Always-on, repo-wide                 |
| `AGENTS.md` (repo root)                  | Always-on alt for non-VS Code agents |
| `.github/instructions/*.instructions.md` | Scoped by `applyTo` glob             |
| `.github/prompts/*.prompt.md`            | On-demand prompts                    |
| `.github/agents/*.agent.md`              | Subagent definitions                 |

### 5. Propose edits

For each promoted theme:

1. **Already covered?** → skip or _strengthen_ the wording. Show before/after.
2. **Has an obvious scoped home?** (Go-only → `go.instructions.md` with `applyTo: '**/*.go'`) → edit that file.
3. **Otherwise** → add to `.github/copilot-instructions.md` under a clearly-labeled section.

Present a single consolidated diff-style proposal **before** writing, and always cite source PRs in a trailing italic line so future maintainers can audit _why_ a rule exists.

### 6. Apply on approval

Apply edits with `replace_string_in_file` / `create_file`. Do **not** auto-commit or auto-push. Summarize at the end:

| File | Sections touched | Themes added | Source PRs |
| ---- | ---------------- | ------------ | ---------- |

### 7. Optimization

The default comments are going to be a bit verbose. Offer to let the user manually optimize the instructions, or for you to do an automated
pass where you go through and try to distill the instructions (that you've added) even further. Preserve the links to the PRs and comments
since the user will still need them to verify what's happened.

### Optional validation (only when requested)

If the user asks to validate before/after Copilot behavior, follow the targeted workflow in [validation.md](./references/validation.md).

## Edge cases

- **Empty `.github/` folder** → Offer to bootstrap `copilot-instructions.md` from the top themes. Reference the `agent-customization` skill for file-format details.
- **Very large PR set** (> 200) → Sample first; ask the user before fetching everything.
- **No `gh` CLI** → All scripts require it. Tell the user to install `gh` ([cli.github.com](https://cli.github.com)) and run `gh auth login` before retrying.
- **Private repo** → Confirm `gh auth status` shows a token with `repo` scope before fetching.
- **Themes that contradict existing instructions** → Surface the conflict; let the user resolve, don't silently overwrite.

## Anti-patterns

- ❌ Writing one giant rule per individual comment. **Cluster first.**
- ❌ Citing zero source PRs in the proposed edit. Reviewers must be able to audit _why_.
- ❌ Putting language-specific rules in `copilot-instructions.md` when a scoped `*.instructions.md` with `applyTo` fits better.
- ❌ Rewriting existing instructions wholesale. Prefer minimal, additive edits.
- ❌ Hardcoding `owner/repo` or absolute paths anywhere in the workflow.
- ❌ Declaring success from comment count alone without checking comment quality.
- ❌ Listing `node_modules` or otherwise probing installed dependencies before
  invoking the scripts. Checking `node --version` is fine (Node ≥ 24 is
  required); dependency issues will surface naturally if a script fails.

## Output checklist

Before declaring done:

- [ ] Repo + PR set + comment counts shown to user
- [ ] Signal-to-noise ratio reported (kept / dropped)
- [ ] Kept comments audited so promoted themes are based on reviewer asks, not author replies
- [ ] Themes table presented with source PR citations
- [ ] Existing `.github/` inventoried before proposing
- [ ] Diff-style proposal shown and approved
- [ ] Edits applied with source citations preserved
- [ ] No commits or pushes made automatically
- [ ] If validation was requested: results summarized per [validation.md](./references/validation.md)
