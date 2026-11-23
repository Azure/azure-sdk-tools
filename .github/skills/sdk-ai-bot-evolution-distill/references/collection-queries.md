# Collection Queries for Evolution Agent Outcomes

Collect Chatbot Evolution Agent items, linked pull requests, wiki commits, and human review comments updated since `<SINCE_DATE>` (`YYYY-MM-DD`).

## Trackers and Markers

Evolution items contain `<!-- chatbot-evolution source="..." classification="..." scope="..." -->` and `[Teams Chatbot]:` titles. Destinations come from `tools/sdk-ai-bots/azure-sdk-qa-bot-knowledge-sync/config/knowledge-config.json` (`repository.issueTracker`):

| Provider | Primary Trackers | Linked Doc Targets |
| --- | --- | --- |
| GitHub | `Azure/azure-sdk-pr` (`feedback-agent`), `Azure/azure-rest-api-specs`, `Azure/typespec-azure`, `Azure/azure-sdk-for-*`, `Azure/openapi-diff` | Upstream repo PRs and `Azure/azure-rest-api-specs.wiki.git` commits |
| Azure DevOps | `https://dev.azure.com/azure-sdk`, project `internal`, area `internal\qa-bot` | Repo `azure-sdk-docs-eng.ms` PRs (`Agency` or maintainer PRs) |

Skip `Azure/azure-sdk-pr` feature issues carrying `Teams Chatbot` without `feedback-agent` or the `chatbot-evolution` marker.

## 1. GitHub Collection (`gh`)

1. **Find updated issues:**

```powershell
gh search issues '"chatbot-evolution"' --owner Azure --updated ">=<SINCE_DATE>" --limit 200 --json repository,number,title,state,closedAt,updatedAt,labels,url
gh search issues '"[Teams Chatbot]:"' --repo Azure/azure-sdk-pr --label feedback-agent --updated ">=<SINCE_DATE>" --limit 200 --json repository,number,title,state,closedAt,updatedAt,labels,url
```

2. **Batch-read issues and linked PRs (`gh api graphql`):** Fetch `issue(number)` fields `body`, `state`, `closedAt`, `comments(first: 50)`, and `timelineItems(first: 50, itemTypes: [CROSS_REFERENCED_EVENT, CONNECTED_EVENT])` resolving `PullRequest { number state merged url repository { nameWithOwner } }`.
3. **Inspect PR feedback and final diffs:** For each linked PR, fetch `author`, `state`, `merged`, `commits(first: 50)` (initial `copilot-swe-agent` commit vs. follow-up commits), `reviews(first: 50)`, `reviewThreads(first: 50)` (`path` and `comments` nodes with `author`, `body`, `diffHunk`), and `files(first: 50)` or `gh pr diff`.
4. **Check wiki commits:** When `Azure/azure-rest-api-specs` wiki issues close without a PR, inspect `git log --since="<SINCE_DATE>" -p` on `https://github.com/Azure/azure-rest-api-specs.wiki.git`.
5. **Filter noise:** Drop comments by `github-actions`, `azure-sdk`, `microsoft-github-policy-service`, `Next Steps to Merge` banners, and automated `Validation passed/failed` notes (keep `Validation skipped` links to maintainer no-action comments).

## 2. Azure DevOps Collection (`az` + REST)

1. **Query `internal\qa-bot` work items:**

```powershell
az boards query --org https://dev.azure.com/azure-sdk -p internal --wiql "SELECT [System.Id], [System.Title], [System.State], [System.ChangedDate] FROM WorkItems WHERE [System.AreaPath] UNDER 'internal\qa-bot' AND [System.ChangedDate] >= '<SINCE_DATE>' ORDER BY [System.ChangedDate] DESC" -o json
```

2. **Fetch work-item bodies, comments, and PR links:** Get a bearer token (`az account get-access-token --resource 499b84ac-1321-427f-aa17-267ca6975798 --query accessToken -o tsv`), then call:
   - `GET https://dev.azure.com/azure-sdk/internal/_apis/wit/workitems?ids=<csv>&$expand=relations&api-version=7.1` (`System.Description` + `vstfs:///Git/PullRequestId/...` links)
   - `GET https://dev.azure.com/azure-sdk/internal/_apis/wit/workItems/<id>/comments?api-version=7.1-preview.4`
3. **Fetch `azure-sdk-docs-eng.ms` PRs and threads:** Run `az repos pr list --org https://dev.azure.com/azure-sdk -p internal -r azure-sdk-docs-eng.ms --status all --top 100 -o json`, match linked or `Agency`-authored `[Teams Chatbot]` PRs, and read `GET .../_apis/git/repositories/<repoId>/pullRequests/<prId>/threads?api-version=7.1` (`commentType == "text"` and `isDeleted != true`).
