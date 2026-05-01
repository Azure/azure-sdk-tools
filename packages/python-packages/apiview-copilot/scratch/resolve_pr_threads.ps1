# Replies + resolves unresolved review threads on PR #15374, mapping by keyword.
# Requires $env:GH_TOKEN to be set (gh CLI uses it automatically).

$ErrorActionPreference = 'Stop'
$gh = "$env:LOCALAPPDATA\ghcli\bin\gh.exe"

# Ordered keyword -> reply mapping. First regex match wins.
# Each reply ends with the relevant commit sha for traceability.
$rules = @(
    @{ pattern = 'Where is the description listed';
       reply  = "Resolved in 9533e8d25 - the new prompty requires a `## Summary` section synthesised from the user's description, plus a `## Details` section that quotes the original wording when useful." },

    @{ pattern = 'doesn.t enforce the same size limits|MAX_DESCRIPTION_LENGTH|size limits as the FastAPI';
       reply  = "Resolved in 0f724e9a6 - the core enforces `MAX_DESCRIPTION_LENGTH = 5000` and `MAX_COMMENT_FIELD_LENGTH = 10000`, mirroring the FastAPI request model. Validated by tests in `TestHandleReportIssueRequestValidation`." },

    @{ pattern = 'How is the prompt going to receive category|prompt.*determine the category';
       reply  = "Resolved in 9533e8d25 - the request no longer carries `category`. The LLM classifies the report as `apiview` or `parser` and emits the choice in its strict-JSON response (`generate_issue_schema.json`)." },

    @{ pattern = 'this is likely something the prompt will have to generate';
       reply  = "Resolved in 9533e8d25 - the LLM now emits `language` (nullable) in its response when category is `parser`, and the server uses it to derive the `[<Language> APIView]` prefix." },

    @{ pattern = 'describing the inputs.*output section';
       reply  = "Resolved in 9533e8d25 - the schema description for `title` is now phrased purely as an output contract (no prefix; server prepends one). Inputs and outputs are no longer mixed." },

    @{ pattern = 'If this is not provided.*language-specific';
       reply  = "Resolved in 9533e8d25 - `language` is optional in the request. When omitted, the LLM (which sees the description and any comment context) is responsible for deciding whether the report is language-specific and which language applies." },

    @{ pattern = 'APIView dialog to have a selector';
       reply  = "Resolved in 9533e8d25 - the dialog only needs to collect a free-text description. The server now derives category and language from the LLM, and can hydrate comment context server-side via `--comment-id` / `commentId`. No selector required." },

    @{ pattern = 'github_issue_result_schema';
       reply  = "The two schemas serve different jobs. The mention workflow already knows the category and language up-front (one is hardcoded per workflow, the other comes from the @mention argument), so its schema only needs `title` and `body`. `/report-issue` has the LLM derive `category` and `language` too, hence the wider schema. Happy to invert that if you'd prefer the dialog to pick category and have the schema match the mention workflow exactly." },

    @{ pattern = 'problem with copilot itself|more actionable feedback mechanisms';
       reply  = "Resolved in 9533e8d25 - the `copilot` category was dropped entirely. APIView Copilot suggestion problems should go through the existing thumbs-up/down feedback path, not this endpoint." },

    @{ pattern = 'shouldn.t mirror.*GithubManager|REPORT_ISSUE_REPO and any other';
       reply  = "Resolved in b25a48fd6 - `LANGUAGE_LABELS`, `resolve_owner`, `language_label`, and `build_issue_labels` now live on `GithubManager`. The mention parser/guidelines workflows and `_github_issue_helpers` consume the shared logic instead of duplicating it. `REPORT_ISSUE_REPO` was left as a module constant since it is specific to this endpoint - happy to move it onto `GithubManager` too if you prefer." },

    @{ pattern = 'might make more sense as an enum';
       reply  = "Resolved in b80a65d2d - the canonical labels are now a typed `LanguageLabel(str, Enum)` on `GithubManager`. `LANGUAGE_LABELS` is the alias->enum mapping; `language_label()` returns the enum; `build_issue_labels()` returns plain strings so callers and the GitHub API stay unaware of the wrapper." },

    @{ pattern = 'Should be shared logic in GithubManager';
       reply  = "Resolved in b25a48fd6 - moved into `GithubManager.resolve_owner()` and consumed by both this endpoint and the mention workflows." },

    @{ pattern = 'Unknown APIView.*not helpful';
       reply  = "Resolved in 9533e8d25 - unknown languages now fall back to plain `[APIView]` instead of `[Unknown APIView]`. Verified by `TestTitlePrefix.test_parser_unknown_language_falls_back_to_apiview`." },

    @{ pattern = 'Rust should result in tags';
       reply  = 'Resolved in 9533e8d25 - Rust is not in `LANGUAGE_LABELS`, so a parser report with `language=rust` now produces labels `["APIView"]` and the title prefix `[APIView]`. The grey-label issue you flagged is gone.' },

    @{ pattern = 'fallback title with .\.\.\.';
       reply  = "Resolved in 9533e8d25 - the prompt now tells the LLM to produce a concise title up-front. The deterministic fallback (only used when the LLM fails entirely) takes the first line of the description capped at 14 words, with no trailing `...`." },

    @{ pattern = 'this should just be ..APIView..';
       reply  = "Resolved in 9533e8d25 - same fix as the `[Unknown APIView]` thread; we now emit `[APIView]` in the no-language and unknown-language cases." },

    @{ pattern = 'Ideally we should not require this';
       reply  = "Kept `--description` required for now since without it the report has no signal beyond the comment. Open to revisiting if telemetry shows users routinely skipping it - happy to make it optional if you'd rather." },

    @{ pattern = 'simply accept a CommentId';
       reply  = "Resolved in 9533e8d25 - the request now accepts a top-level `commentId`. When provided, the server calls `get_comment_with_context(comment_id)` to hydrate text / code snippet / language / element id / source. Caller can still pass an explicit `commentContext` if it wants to override. Wired through the CLI as `--comment-id` in 967506b19." },

    @{ pattern = 'should probably be .avc agent report-issue';
       reply  = "Resolved in 967506b19 - the command moved from `avc issue report` to `avc agent report-issue`, alongside `avc agent mention` / `avc agent chat` / `avc agent resolve-thread`. `docs/cli.md` updated in the same commit." },

    @{ pattern = 'we don.t give it a short code';
       reply  = "Resolved in 967506b19 - dropped both `-c` (`--category`, which itself is now gone) and `-d` (`--description`). All other options on `avc agent report-issue` are long-form only." },

    @{ pattern = 'unless .-d. is used for .--description';
       reply  = "Resolved in 967506b19 - `-d` was removed from `--description`. No other command uses `-d` for the same concept, so dropping the short alias is consistent with the convention you noted." },

    @{ pattern = 'optional .--comment-id.* lookup in the command';
       reply  = "Resolved in 9533e8d25 + 967506b19 - the CLI now accepts `--comment-id` and the server-side `_lookup_comment_context` calls `get_comment_with_context` to hydrate the rest. Documented in `docs/cli.md`." }
)

$threads = (Get-Content $env:TEMP\threads.json -Raw | ConvertFrom-Json).data.repository.pullRequest.reviewThreads.nodes
$unresolved = $threads | Where-Object { -not $_.isResolved }
Write-Host "Unresolved threads: $($unresolved.Count)"

$replyMutation = @'
mutation($threadId:ID!, $body:String!) {
  addPullRequestReviewThreadReply(input:{pullRequestReviewThreadId:$threadId, body:$body}) {
    comment { id }
  }
}
'@

$resolveMutation = @'
mutation($threadId:ID!) {
  resolveReviewThread(input:{threadId:$threadId}) {
    thread { id isResolved }
  }
}
'@

$results = @()
foreach ($t in $unresolved) {
    $body = $t.comments.nodes[0].body
    $first = ($body -replace "`r?`n", " ").Substring(0,[Math]::Min(80, $body.Length))
    $matchedRule = $null
    foreach ($r in $rules) {
        if ($body -match $r.pattern) { $matchedRule = $r; break }
    }
    if (-not $matchedRule) {
        Write-Host "[SKIP] No rule for: $first" -ForegroundColor Yellow
        $results += [PSCustomObject]@{ ThreadId=$t.id; Status='no-match'; Snippet=$first }
        continue
    }
    Write-Host "[POST] $first" -ForegroundColor Cyan

    $replyOut = & $gh api graphql -F "threadId=$($t.id)" -F "body=$($matchedRule.reply)" -f query=$replyMutation 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  reply failed: $replyOut" -ForegroundColor Red
        $results += [PSCustomObject]@{ ThreadId=$t.id; Status='reply-failed'; Snippet=$first; Error="$replyOut" }
        continue
    }

    $resolveOut = & $gh api graphql -F "threadId=$($t.id)" -f query=$resolveMutation 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  resolve failed: $resolveOut" -ForegroundColor Red
        $results += [PSCustomObject]@{ ThreadId=$t.id; Status='resolve-failed'; Snippet=$first; Error="$resolveOut" }
        continue
    }

    Write-Host "  ok" -ForegroundColor Green
    $results += [PSCustomObject]@{ ThreadId=$t.id; Status='done'; Snippet=$first }
}

Write-Host "`n=== Summary ==="
$results | Group-Object Status | ForEach-Object { Write-Host "$($_.Name): $($_.Count)" }
$results | Where-Object { $_.Status -ne 'done' } | Format-Table -AutoSize -Wrap
