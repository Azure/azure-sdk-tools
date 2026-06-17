# Validation workflow (optional)

Use this only when the user explicitly asks to validate instruction changes against historical PRs.

This workflow compares Copilot behavior before vs. after `.github/` instruction edits on the same PR replay setup.

## Preconditions

- PR data is already fetched (`fetch-prs.ts`).
- You have at least one candidate PR to replay.
- You confirmed the destination test repo path (`--test-repo`) with the user.

## Single-PR validation

Pick one representative PR and replay review rounds with `replay-pull-request.ts`.

```bash
node ./scripts/replay-pull-request.ts \
  --repo owner/repo \
  --number 1234 \
  --test-repo /path/to/local/clone \
  --github /path/to/candidate/.github \
  --branch step-validate
```

If you only want a single review round:

```bash
node ./scripts/replay-pull-request.ts \
  --repo owner/repo \
  --number 1234 \
  --test-repo /path/to/local/clone \
  --github /path/to/candidate/.github \
  --branch step-validate \
  --round 1
```

## Batch validation (broader coverage)

Use candidate picking plus batch replay for a broader check after a successful single-PR run.

```bash
node ./scripts/pick-pr-candidates.ts \
  --glob "pr-cache/owner-repo/pr-*.json" \
  --exclude-file ./tested-prs.txt \
  --limit 6 \
  --format json \
  | node ./scripts/replay-pull-request.ts \
      --repo owner/repo \
      --test-repo /path/to/local/clone \
      --github /path/to/candidate/.github \
      --input-json-stdin \
      --concurrency 3
```

## What to report

When validation is requested, include all of the following in your summary:

- At least one validation run was executed after instruction edits.
- Before/after Copilot behavior was compared on at least one target PR.
- Before/after quality comparison included signal quality, not only raw comment counts.

Use a concise table format where possible:

| PR    | Baseline behavior         | Updated behavior                                  | Quality delta                            |
| ----- | ------------------------- | ------------------------------------------------- | ---------------------------------------- |
| #1234 | Missed cancellation check | Flagged cancellation check with file+line context | Higher precision, fewer generic comments |
