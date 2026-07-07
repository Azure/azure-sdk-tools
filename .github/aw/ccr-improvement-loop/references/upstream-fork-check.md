# Upstream / fork check (run before any fetch or write)

CCR review history lives on the **upstream** repo, not on forks or downstream
sandbox/mirror repos. Mining the wrong repo produces themes built from synthetic
or bot-only feedback, and writing to the wrong repo is a safety violation.
Confirm the resolved target is not a fork/mirror **before** `fetch-prs.ts` reads
it and **before** PR mode writes anything.

## Checks (in order)

1. **GitHub fork metadata** — authoritative for true forks:

   ```bash
   gh repo view OWNER/REPO --json isFork,parent,source,nameWithOwner,defaultBranchRef
   ```

   - If `isFork: true`, surface `parent.nameWithOwner` (immediate upstream) and
     `source.nameWithOwner` (fork-network root), then ask:
     _"This repo is a fork of `<parent>`. Mine/propose against the upstream
     `<parent>`, this fork, or both?"_
   - Record `defaultBranchRef.name` — it is the branch automated writes must
     **never** target directly (always open a PR from a feature branch).

2. **Conventional upstream remote** — catches manually-cloned forks GitHub does
   not link:

   ```bash
   git -C <target-checkout> remote -v | grep -i '^upstream'
   ```

   If present, surface the URL and ask the same question.

## Write-mode gate (PR mode only)

Before writing any `.github/` file or opening a PR:

- The branch is created in the **target repo** (or its fork), never this workflow support package.
- **Never auto-commit to the default branch** captured in check 1 — open a PR.
- If `isFork: true` and the user chose "upstream", open the PR against the parent
  via a fork branch; otherwise abort and ask.

## Limits

GitHub fork metadata is the only reliable automatic signal — a repo created by
`git clone` + push to a new remote carries no machine-readable link to its origin.
When `isFork` is false and no `upstream` remote exists but a mirror is still
suspected, **stop and ask** rather than guessing. Never silently mine or write to a
fork or mirror.
