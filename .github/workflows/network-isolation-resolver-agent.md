---
description: Resolve network isolation issues
timeout-minutes: 60
on:
  push:
    branches:
      - NetworkIsolationResolverAgent
  workflow_dispatch:
    inputs:
      cluster:
        description: "Kusto cluster URL"
        required: false
        default: "https://oneespipelinesprod.westus3.kusto.windows.net/"
        type: string
      database:
        description: "Kusto database name"
        required: false
        default: "oneespipelinetemplatesprod"
        type: string
      query:
        description: "Optional KQL query override. NET_ISO_RESOLVER_QUERY secret is used by default."
        required: false
        default: ""
        type: string
      devops_org:
        description: "Azure DevOps organization"
        required: false
        default: "azure-sdk"
        type: string
permissions:
  id-token: write
  contents: read
  issues: read
  pull-requests: read
network:
  allowed:
    - defaults
    - dev.azure.com
    - visualstudio.com
    - login.microsoftonline.com
    - azure.com
tools:
  github:
    toolsets: [repos, issues, pull_requests, search]
    min-integrity: approved
    allowed-repos:
      - azure/*
    github-token: ${{ secrets.GH_AW_GITHUB_MCP_SERVER_TOKEN }}
strict: false
safe-outputs:
  create-pull-request:
    github-token: ${{ secrets.GH_AW_GITHUB_TOKEN }}
    allowed-repos:
      - azure/*
    title-prefix: "[CFS] "
    reviewers:
      - chidozieononiwu
    draft: false
    max: 10
    base-branch: main
    preserve-branch-name: true
    protected-files: fallback-to-issue
  noop:
steps:
  - name: Checkout repository
    uses: actions/checkout@v4
    with:
      persist-credentials: false

  - name: Set up Python
    uses: actions/setup-python@v5
    with:
      python-version: "3.11.9"

  - name: Install Kusto query dependencies
    run: |
      pip install --upgrade pip
      pip install azure-kusto-data==4.6.3 azure-identity==1.17.1
      pip install pytest==7.4.0

  - name: Run destructive query safety tests
    run: pytest eng/scripts/python/kusto-query-executor/kusto-query-executor-tests.py -v

  - name: Azure Login with OIDC for Kusto
    uses: azure/login@v1
    with:
      client-id: 743af79e-ad6b-4e59-a3f5-fc4996ffe1d8
      tenant-id: 72f988bf-86f1-41af-91ab-2d7cd011db47
      allow-no-subscriptions: true

  - name: Execute Kusto query
    shell: bash
    env:
      KUSTO_QUERY_INPUT: ${{ inputs.query || '' }}
      KUSTO_QUERY_SECRET: ${{ secrets.NET_ISO_RESOLVER_QUERY }}
    run: |
      query="${KUSTO_QUERY_SECRET:-$KUSTO_QUERY_INPUT}"

      if [ -z "$query" ]; then
        echo "A Kusto query must be provided by input or NET_ISO_RESOLVER_QUERY secret." >&2
        exit 1
      fi

      python eng/scripts/python/kusto-query-executor/kusto-query-executor.py \
        --cluster "${{ inputs.cluster || 'https://oneespipelinesprod.westus3.kusto.windows.net/' }}" \
        --database "${{ inputs.database || 'oneespipelinetemplatesprod' }}" \
        --query "$query" \
        --output-format json \
        --output-file kusto-query-result.json

  - name: Azure Login with OIDC for DevOps
    uses: azure/login@v1
    with:
      client-id: c277c2aa-5326-4d16-90de-98feeca69cbc
      tenant-id: 72f988bf-86f1-41af-91ab-2d7cd011db47
      allow-no-subscriptions: true

  - name: Resolve DevOps pipeline entry points
    id: resolve
    shell: pwsh
    env:
      ADO_ORG: ${{ inputs.devops_org || 'azure-sdk' }}
    run: |
      Set-StrictMode -Version 4
      $ErrorActionPreference = 'Stop'

      . ./eng/common/scripts/Invoke-DevOpsAPI.ps1

      if (-not (Test-Path -Path kusto-query-result.json)) {
        throw "Kusto query did not create kusto-query-result.json. Check the Execute Kusto query step logs."
      }

      $rows = Get-Content -Path kusto-query-result.json -Raw | ConvertFrom-Json
      if ($null -eq $rows) {
        $rows = @()
      }
      elseif ($rows -isnot [array]) {
        $rows = @($rows)
      }

      "row_count=$($rows.Count)" | Out-File -FilePath $env:GITHUB_OUTPUT -Append
      Write-Host "Kusto returned $($rows.Count) row(s)."

      $bearerToken = az account get-access-token `
        --resource "499b84ac-1321-427f-aa17-267ca6975798" `
        --query "accessToken" `
        --output tsv

      if ([string]::IsNullOrWhiteSpace($bearerToken)) {
        throw "Unable to get Azure DevOps bearer token."
      }

      $headers = Get-DevOpsApiHeaders -BearerToken $bearerToken
      $enriched = @()

      foreach ($row in $rows) {
        $project = $row.ProjectName
        $buildId = $row.BuildId
        $definitionId = $row.PipelineDefinitionId

        if ([string]::IsNullOrWhiteSpace($project) -or [string]::IsNullOrWhiteSpace($buildId) -or [string]::IsNullOrWhiteSpace($definitionId)) {
          Write-Warning "Skipping row because ProjectName, BuildId, or PipelineDefinitionId is missing."
          continue
        }

        $buildUri = "https://dev.azure.com/$env:ADO_ORG/$project/_apis/build/builds/$buildId`?api-version=7.1"
        $definitionUri = "https://dev.azure.com/$env:ADO_ORG/$project/_apis/build/definitions/$definitionId`?api-version=7.1"

        Write-Host "Fetching build $buildId and definition $definitionId from project '$project'."
        $build = Invoke-RestMethod -Method GET -Uri $buildUri -Headers $headers -MaximumRetryCount 3 -RetryIntervalSec 5
        $definition = Invoke-RestMethod -Method GET -Uri $definitionUri -Headers $headers -MaximumRetryCount 3 -RetryIntervalSec 5

        if ($build.sourceBranch -ne "refs/heads/main" -and $build.sourceBranch -ne "main") {
          Write-Host "Skipping build $buildId because sourceBranch is '$($build.sourceBranch)', not main."
          continue
        }

        $enriched += [ordered]@{
          ProjectName = $project
          BuildId = $buildId
          PipelineDefinitionId = $definitionId
          TaskName = $row.TaskName
          JobName = $row.JobName
          DomainName = $row.DomainName
          RepositoryId = $row.RepositoryId
          RepositoryName = $definition.repository.name
          SourceBranch = $build.sourceBranch
          SourceVersion = $build.sourceVersion
          PipelineName = $definition.name
          PipelinePath = $definition.path
          YamlFilename = $definition.process.yamlFilename
          BuildUrl = $build._links.web.href
        }
      }

      if ($enriched.Count -eq 0) {
        "[]" | Set-Content -Path devops-pipeline-entrypoints.json
      }
      else {
        $enriched | ConvertTo-Json -Depth 20 | Set-Content -Path devops-pipeline-entrypoints.json
      }

      "resolved_count=$($enriched.Count)" | Out-File -FilePath $env:GITHUB_OUTPUT -Append
      Write-Host "Resolved $($enriched.Count) pipeline entry point(s)."
---

# Network Isolation Resolver Agent

You are an AI agent that fixes network isolation issues found by the resolver setup steps that ran before you.

## Starting Context

Start by reading `devops-pipeline-entrypoints.json`. Treat that file as the authoritative list of work items. Each item may contain:

- `ProjectName`
- `BuildId`
- `PipelineDefinitionId`
- `TaskName`
- `JobName`
- `DomainName`
- `RepositoryId`
- `RepositoryName`
- `SourceBranch`
- `SourceVersion`
- `PipelineName`
- `PipelinePath`
- `YamlFilename`
- `BuildUrl`

If the file is missing, unreadable, malformed, or empty, call `noop` with a concise explanation and stop.

## Required Workflow

Process entries strictly one at a time. Fully finish the current entry before starting the next entry. Keep each target repository checkout in a separate directory under `/tmp/network-isolation-repos` so context from different entries cannot mix.

For each entry:

1. Validate that `ProjectName`, `BuildId`, `PipelineDefinitionId`, `JobName`, `RepositoryName` or `RepositoryId`, and `YamlFilename` are present. If required data is missing, record the skipped entry in your final summary and continue to the next entry.
2. Determine the target GitHub repository from `RepositoryName`. If `RepositoryName` is already in `owner/repo` format, use it as-is. If it is only a repository name, use `Azure/<RepositoryName>`. Use `RepositoryId` only as supporting identity when inspecting Azure DevOps metadata.
3. Locate the pipeline entry point using `YamlFilename`. Start investigation from that YAML file and follow templates, extends, jobs, stages, and included pipeline files until you identify the job corresponding to `JobName`.
4. Before making a change, search the target GitHub repository for recent pull requests with `CFS` or `network isolation` in the title. Use those examples only as implementation guidance so the fix follows the repository's established remediation pattern.
5. Diagnose why the job is failing network isolation. Use `DomainName`, `TaskName`, the located pipeline code, nearby allowlist/network-isolation patterns in the checked-out repository, and relevant prior PR examples.
6. Code the smallest targeted fix in the target repository. Preserve the repository's existing pipeline style and avoid unrelated formatting or refactors.
7. Prepare a branch name for only that entry. Use a branch name that starts with `network-isolation-cfs-` and includes the pipeline definition ID and the build ID.
8. Do not run `git push`, `gh auth login`, or `gh pr create`. Branch push and pull request creation must happen through the `create_pull_request` safe-output tool.
9. Request a pull request against the target repository's `main` branch using the `create_pull_request` safe-output tool. Include the target repository in the tool call's `repo` field using `owner/repo` format. The pull request title must include `CFS`. The pull request body must include:
  - the network isolation domain being fixed,
  - the affected job name,
  - the pipeline entry point YAML path,
  - the pipeline run URL,
  - a short explanation of the change.
10. Reviewer assignment for `chidozieononiwu` is configured on the pull request safe output. If reviewer assignment fails, include that limitation in your final summary.
11. Clear your working context for the completed entry before moving to the next one.

## Azure DevOps Guidance

The setup steps already resolved Azure DevOps pipeline metadata into `devops-pipeline-entrypoints.json`. Do not make additional Azure DevOps calls unless the entry is missing required metadata that can only be recovered from Azure DevOps.

Use the Azure DevOps organization from the workflow input when needed; the default is `azure-sdk`.

## Output

When all entries are processed and no pull requests were requested, call `noop` once with a concise final summary. Include:

- total entries read,
- entries fixed,
- pull request URLs created,
- entries skipped and why,
- entries attempted but not completed and why.

Do not call `noop` after calling `create_pull_request`; safe-output pull request creation is the final action for those entries.

Do not process entries concurrently. Do not reuse a checkout, branch, or diagnosis from one entry for another entry.