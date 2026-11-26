# Collection Queries for Evolution Agent Outcomes

Enumerate tracked items directly from Chatbot Evolution `qa-records` updated since `<SINCE_DATE>` (`YYYY-MM-DD`), then fetch full provider comments and linked PR diffs.

## 1. Enumerate Issue URLs from Evolution Records

Every Evolution Agent item is persisted in `QARecord.feedback.issue_url` (`models/qa_record.py`) as a canonical GitHub issue URL (`https://github.com/<owner>/<repo>/issues/<number>`) or Azure Boards URL (`https://dev.azure.com/<org>/<project>/_workitems/edit/<id>`). Do not search across repositories.

- **Via QA Dashboard API (`server.py`):** Page through `GET <DASHBOARD_BASE_URL>/api/dashboard/qa-records?updated_from=<SINCE_DATE>T00:00:00Z&page=<1..N>` until `page * page_size >= total`, keep records with non-null `feedback.issue_url` (focusing on closed outcomes `validation_passed`, `validation_failed`, `validation_skipped`), and deduplicate by `feedback.issue_url`.
- **Via Cosmos DB (`utils/azure_cosmosdb.py`):** Query container `qa-records` in database `azure-sdk-qa-bot`:

```sql
SELECT c.id, c.tenant_id, c.feedback.issue_url, c.feedback.status, c.feedback.classification, c.updated_at
FROM c
WHERE IS_DEFINED(c.feedback.issue_url) AND NOT IS_NULL(c.feedback.issue_url)
  AND c.updated_at >= @since
```

Parse each unique `issue_url` with `models.feedback.parse_issue_reference` (or URL host) into GitHub vs. Azure DevOps batches.

## 2. Fetch GitHub Issue and PR Evidence (`gh`)

1. **Read issue and linked PRs (`gh api graphql`):** For each `https://github.com/<owner>/<repo>/issues/<number>`, fetch `body`, `state`, `closedAt`, `comments(first: 100, after: $cursor)`, and `timelineItems(first: 100, after: $cursor, itemTypes: [CROSS_REFERENCED_EVENT, CONNECTED_EVENT])` resolving `PullRequest { number state merged url repository { nameWithOwner } }`. Follow `pageInfo { hasNextPage endCursor }` until `hasNextPage` is `false`.
2. **Fetch PR review threads and full patch diff:** For each linked PR, paginate `commits(first: 100, after: $cursor)`, `reviews(first: 100, after: $cursor)`, and `reviewThreads(first: 100, after: $cursor)` (`path` and paginated `comments(first: 100, after: $commentCursor)` nodes with `author { login }`, `body`, `diffHunk`), then fetch the actual patch content via `gh pr diff <pr_number> --repo <owner>/<repo>` (GraphQL `files` exposes metadata only, not patch hunks).
3. **Check wiki commits:** When a wiki-backed issue closes without a linked PR, inspect `git log --since="<SINCE_DATE>" -p` on `https://github.com/<owner>/<repo>.wiki.git`.

## 3. Fetch Azure DevOps Work Item and PR Evidence (`az` + REST)

Acquire a bearer token once via `az account get-access-token --resource 499b84ac-1321-427f-aa17-267ca6975798 --query accessToken -o tsv`:

1. **Read work item and PR links:** Call `GET https://dev.azure.com/<org>/<project>/_apis/wit/workitems?ids=<csv>&$expand=relations&api-version=7.1` to read `System.Description`, `System.State`, and `vstfs:///Git/PullRequestId/...` relations.
2. **Paginate all work-item comments:** Call `GET https://dev.azure.com/<org>/<project>/_apis/wit/workItems/<id>/comments?api-version=7.1-preview.4`, repeating with `&continuationToken=<token>` until `continuationToken` is absent.
3. **Read linked PR threads and diffs:** For each linked PR, fetch human threads (`GET .../_apis/git/repositories/<repoId>/pullRequests/<prId>/threads?api-version=7.1`, `commentType == "text"` and `isDeleted != true`) and inspect changed file content across the PR's source/target commits.

## 4. Noise Filtering

Ignore comments authored by `github-actions`, `azure-sdk`, `microsoft-github-policy-service`, `Next Steps to Merge` banners, and automated `Validation passed/failed` comments (retain `Validation skipped` citations pointing to a human no-action comment).
