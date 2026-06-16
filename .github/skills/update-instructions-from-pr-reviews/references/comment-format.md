# Comment formatting reference

`format-comments.ts` reads filter output (JSON, from `filter-comments.ts`) and emits compact markdown — one block per comment. Use this when you need the agent (or the user) to actually read comments in-context, since markdown is far cheaper in tokens than raw JSON.

## Canonical pipeline

```bash
node ./scripts/filter-comments.ts --glob "pr-cache/owner-repo/pr-*.json" \
  | node ./scripts/format-comments.ts
```

Run `node ./scripts/format-comments.ts --help` for flags (alternate input source, hunk suppression, hunk line cap).

## Output template

Each comment is rendered as:

```
## PR #123

Comment: [https://github.com/owner/repo/pull/123#discussion_r123456789](https://github.com/owner/repo/pull/123#discussion_r123456789)

### [carol] client.go:42

> -  return err
> +  return fmt.Errorf("client dispatch: %w", err)

Wrap this error with %w so callers can errors.Is it.

---
```

## Rules when citing comments to the user

- **Always include the full comment URL** (inline discussion link or issue-comment link), not only the PR URL. The PR URL alone forces the reader to hunt.
- Only changed lines (`-` / `+`) from each diff hunk are shown — `@@` headers and unchanged context are stripped to minimize tokens.
- Review-summary comments omit `path:line` because they're top-level on the PR, not anchored to a file.
