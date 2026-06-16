# Upstream / fork check (run before fetching)

PR review history almost always lives on the **upstream** repo, not on forks or
downstream sandbox/mirror repos. Mining the wrong repo wastes time and produces
themes built from synthetic or bot-only feedback. **Before** running
`fetch-prs.ts`, check whether the resolved target is a fork or mirror.

## Checks

1. **GitHub fork metadata** (authoritative for true forks):
   ```bash
   gh repo view --json isFork,parent,source,nameWithOwner
   ```
   - If `isFork: true`, surface `parent.nameWithOwner` (the immediate upstream)
     and `source.nameWithOwner` (the root of the fork network) and ask:
     *"This repo is a fork of `<parent>`. Mine the upstream `<parent>` instead, this fork, or both?"*

2. **Conventional upstream remote** (catches manually-cloned forks that
   GitHub doesn't link):
   ```bash
   git remote -v | grep -i '^upstream'
   ```
   If present, surface the URL and ask the same question.

## Limits

GitHub fork metadata is the only reliable signal for an automatic upstream
lookup — repos created by `git clone` + push to a new remote carry no
machine-readable link back to their origin, so there is no dependable way to
infer a manual mirror from the repo name alone. When `isFork` is false and no
`upstream` remote exists but you still have reason to suspect a mirror, **stop
and ask the user** to confirm the upstream rather than guessing.

If the user names an upstream, re-run `fetch-prs.ts` with `--repo <upstream>`.
**Never silently mine a fork or mirror** — always confirm.
