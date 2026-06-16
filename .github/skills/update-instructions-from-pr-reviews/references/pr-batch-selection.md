# PR batch selection — broadening PR coverage

Use this when you've already validated the skill on a handful of PRs and want to expand coverage across many more without re-evaluating every one by hand. The PR batch selection workflow picks high-signal candidates from cache and runs them in parallel.

Prerequisite: PRs already fetched into `pr-cache/<owner>-<repo>/` via `fetch-prs.ts`.

## Pick the next batch (cache-only, no API calls)

`pick-pr-candidates.ts` ranks cached PRs by human inline comment count, then total human comments, and skips any PRs you've already tested.

```bash
node ./scripts/pick-pr-candidates.ts \
  --glob "pr-cache/Azure-azure-dev/pr-*.json" \
  --exclude-file ./tested-prs.txt \
  --limit 6 \
  --format json
```

`--exclude` accepts a space-separated list inline; `--exclude-file` reads one PR number per line.

## Run the batch in parallel

Pipe the JSON candidate list straight into `replay-pull-request.ts`. Keep `--concurrency` low enough that you can still inspect each PR as it finishes.

```bash
node ./scripts/pick-pr-candidates.ts \
  --glob "pr-cache/Azure-azure-dev/pr-*.json" \
  --exclude-file ./tested-prs.txt \
  --limit 6 \
  --format json \
  | node ./scripts/replay-pull-request.ts \
      --repo Azure/azure-dev \
      --test-repo /home/ripark/src/_projects/ccr/ccrtesting \
      --github /path/to/candidate/.github \
      --input-json-stdin \
      --concurrency 3
```

`replay-pull-request.ts` handles both single PRs and batches — a batch of one behaves exactly like a single run, so there's no separate single-PR script.

**Always confirm `--test-repo` with the user before running.** The source PR repo (`--repo`) and the test PR destination repo are not always the same.
