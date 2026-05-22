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
  bash: [":*"]
  github:
    toolsets: [repos, issues, pull_requests, search]
    min-integrity: approved
    allowed-repos:
      - azure/*
    github-token: ${{ secrets.GH_AW_GITHUB_MCP_SERVER_TOKEN }}
strict: false
safe-outputs:
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

  - name: Stage pull request guidance
    shell: pwsh
    run: |
      Set-StrictMode -Version 4
      $ErrorActionPreference = 'Stop'

      $sourcePath = ".github/workflows/network-isolation-pr-guidance.json"
      $workingPath = "network-isolation-pr-guidance.json"

      if (-not (Test-Path -Path $sourcePath)) {
        "{}" | Set-Content -Path $workingPath
        Write-Host "No pull request guidance file found at $sourcePath. Wrote empty guidance to $workingPath."
        return
      }

      $guidance = Get-Content -Path $sourcePath -Raw | ConvertFrom-Json -Depth 20
      $guidance | ConvertTo-Json -Depth 20 | Set-Content -Path $workingPath
      Write-Host "Staged pull request guidance from $sourcePath to $workingPath."
post-steps:
  - name: Azure Login for GitHub App token
    if: always()
    uses: azure/login@v1
    with:
      client-id: 5786d1fb-187e-4ca9-9a81-ab89ea278986
      tenant-id: 72f988bf-86f1-41af-91ab-2d7cd011db47
      subscription-id: a18897a6-7e44-457d-9260-f2854c0aca42

  - name: Login to GitHub as Azure SDK automation
    if: always()
    uses: ./eng/common/actions/login-to-github
    with:
      token-owners: Azure

  - name: Push remediation branches and create pull requests
    if: always()
    shell: pwsh
    env:
      REVIEWER: chidozieononiwu
      ASSIGNEE: chidozieononiwu
    run: |
      Set-StrictMode -Version 4
      $ErrorActionPreference = 'Stop'

      function Invoke-CheckedNativeCommand {
        param(
          [Parameter(Mandatory = $true)]
          [string] $Command,

          [Parameter(ValueFromRemainingArguments = $true)]
          [string[]] $Arguments
        )

        & $Command @Arguments
        if ($LASTEXITCODE -ne 0) {
          throw "Command failed with exit code ${LASTEXITCODE}: $Command"
        }
      }

      $manifestPath = "/tmp/network-isolation-remediations/remediations.json"
      if (-not (Test-Path -Path $manifestPath)) {
        Write-Host "No remediation manifest found at $manifestPath. Nothing to push."
        return
      }

      if ([string]::IsNullOrWhiteSpace($env:GH_TOKEN)) {
        throw "GH_TOKEN is empty. The login-to-github post-step did not mint an Azure GitHub App token."
      }

      $remediations = Get-Content -Path $manifestPath -Raw | ConvertFrom-Json
      if ($null -eq $remediations) {
        Write-Host "Remediation manifest is empty. Nothing to push."
        return
      }
      elseif ($remediations -isnot [array]) {
        $remediations = @($remediations)
      }

      $askPassPath = Join-Path $env:RUNNER_TEMP ("network-isolation-git-askpass-{0}.sh" -f ([Guid]::NewGuid().ToString('N')))
      $tokenFilePath = Join-Path $env:RUNNER_TEMP ("network-isolation-git-token-{0}.txt" -f ([Guid]::NewGuid().ToString('N')))
      Set-Content -Path $tokenFilePath -Value $env:GH_TOKEN -NoNewline
      Set-Content -Path $askPassPath -Value @'
      #!/bin/sh
      case "$1" in
        *Username*) printf '%s\n' 'x-access-token' ;;
        *) cat "$GIT_ASKPASS_TOKEN_FILE" ;;
      esac
      '@
      Invoke-CheckedNativeCommand chmod 600 $tokenFilePath
      Invoke-CheckedNativeCommand chmod 700 $askPassPath

      $previousGitAskPass = $env:GIT_ASKPASS
      $previousGitAskPassTokenFile = $env:GIT_ASKPASS_TOKEN_FILE
      $previousGitTerminalPrompt = $env:GIT_TERMINAL_PROMPT

      $env:GIT_ASKPASS = $askPassPath
      $env:GIT_ASKPASS_TOKEN_FILE = $tokenFilePath
      $env:GIT_TERMINAL_PROMPT = '0'

      try {
        foreach ($remediation in $remediations) {
          $repo = [string]$remediation.repo
          $branch = [string]$remediation.branch
          $workingDirectory = [string]$remediation.workingDirectory
          $title = [string]$remediation.title
          $body = [string]$remediation.body

          if ($repo -notmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$') {
            throw "Invalid repo value '$repo' in remediation manifest. Expected owner/repo."
          }
          if ($branch -notmatch '^[A-Za-z0-9._/-]+$') {
            throw "Invalid branch value '$branch' in remediation manifest."
          }
          if ([string]::IsNullOrWhiteSpace($workingDirectory) -or -not (Test-Path -Path $workingDirectory)) {
            throw "Working directory '$workingDirectory' does not exist for $repo."
          }
          if ([string]::IsNullOrWhiteSpace($title) -or [string]::IsNullOrWhiteSpace($body)) {
            throw "Remediation for $repo/$branch must include title and body."
          }

          Push-Location $workingDirectory
          try {
            $currentBranch = git branch --show-current
            if ($LASTEXITCODE -ne 0) {
              throw "Failed to determine the current branch in $workingDirectory."
            }
            if ($currentBranch -ne $branch) {
              Invoke-CheckedNativeCommand git checkout $branch
            }

            $serverUrl = "https://github.com/$repo.git"
            Invoke-CheckedNativeCommand git remote set-url origin $serverUrl
            Invoke-CheckedNativeCommand git push --set-upstream origin $branch

            $existingPr = gh pr list --repo $repo --head $branch --state open --json url --jq '.[0].url'
            if ($LASTEXITCODE -ne 0) {
              throw "Failed to list existing pull requests for ${repo}:${branch}."
            }
            if ([string]::IsNullOrWhiteSpace($existingPr)) {
              $bodyPath = Join-Path $env:RUNNER_TEMP ("network-isolation-pr-{0}.md" -f ([Guid]::NewGuid().ToString('N')))
              Set-Content -Path $bodyPath -Value $body
              $prUrl = gh pr create --repo $repo --base main --head $branch --title $title --body-file $bodyPath
              if ($LASTEXITCODE -ne 0) {
                throw "Failed to create pull request for ${repo}:${branch}."
              }
            }
            else {
              $prUrl = $existingPr
              Write-Host "Open pull request already exists for ${repo}:${branch}: $prUrl"
            }

            try {
              & gh pr edit $prUrl --add-reviewer $env:REVIEWER
              if ($LASTEXITCODE -ne 0) {
                Write-Warning "Pull request was created, but reviewer assignment failed for $env:REVIEWER."
              }
            }
            catch {
              Write-Warning "Pull request was created, but reviewer assignment failed for $env:REVIEWER. $($_.Exception.Message)"
            }

            try {
              & gh pr edit $prUrl --add-assignee $env:ASSIGNEE
              if ($LASTEXITCODE -ne 0) {
                Write-Warning "Pull request was created, but assignee assignment failed for $env:ASSIGNEE."
              }
            }
            catch {
              Write-Warning "Pull request was created, but assignee assignment failed for $env:ASSIGNEE. $($_.Exception.Message)"
            }

            Write-Host "Created or updated pull request for ${repo}:${branch}: $prUrl"
          }
          finally {
            Pop-Location
          }
        }
      }
      finally {
        $env:GIT_ASKPASS = $previousGitAskPass
        $env:GIT_ASKPASS_TOKEN_FILE = $previousGitAskPassTokenFile
        $env:GIT_TERMINAL_PROMPT = $previousGitTerminalPrompt
        Remove-Item -Path $askPassPath,$tokenFilePath -Force -ErrorAction SilentlyContinue
      }

      foreach ($remediation in $remediations) {
        $workingDirectory = [string]$remediation.workingDirectory
        $repo = [string]$remediation.repo
        if (-not [string]::IsNullOrWhiteSpace($workingDirectory) -and (Test-Path -Path $workingDirectory) -and $repo -match '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$') {
          Push-Location $workingDirectory
          try {
            git remote set-url origin "https://github.com/$repo.git" 2>$null
          }
          finally {
            Pop-Location
          }
        }
      }

  - name: Delete pull request guidance working copy
    if: always()
    shell: pwsh
    run: |
      Remove-Item -Path network-isolation-pr-guidance.json -Force -ErrorAction SilentlyContinue
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

Also read `network-isolation-pr-guidance.json` if it exists. It is a JSON object keyed by domain name. Each domain entry may contain one or more examples with `repo` or `repository`, optional `repositoryId`, and `pullRequests`, `pullRequestNumbers`, or `prs` containing pull request numbers. If an example only has `repositoryId`, use it when it matches the current entry's `RepositoryId`, and look up the pull requests in the target GitHub repository determined from the current entry. Use this file only as lookup guidance for prior examples; do not treat it as a work item source.

Example guidance input:

```json
{
  "registry.npmjs.org": [
    {
      "RepositoryId": "Azure/autorest.go",
      "pullRequests": [1234, 5678]
    }
  ]
}
```

## Required Workflow

Process entries strictly one at a time. Fully finish the current entry before starting the next entry. Keep each target repository checkout in a separate directory under `/tmp/network-isolation-repos` so context from different entries cannot mix.

For each entry:

1. Validate that `ProjectName`, `BuildId`, `PipelineDefinitionId`, `JobName`, `RepositoryName` or `RepositoryId`, and `YamlFilename` are present. If required data is missing, record the skipped entry in your final summary and continue to the next entry.
2. Determine the target GitHub repository from `RepositoryName`. If `RepositoryName` is already in `owner/repo` format, use it as-is. If it is only a repository name, use `Azure/<RepositoryName>`. Use `RepositoryId` only as supporting identity when inspecting Azure DevOps metadata.
3. Locate the pipeline entry point using `YamlFilename`. Start investigation from that YAML file and follow templates, extends, jobs, stages, and included pipeline files until you identify the job corresponding to `JobName`.
4. Before making a change, look up prior pull request examples only from `network-isolation-pr-guidance.json` for the entry's `DomainName`. Match the domain key exactly first, then case-insensitively if needed. For each matching example, use the specified repository and pull request numbers to inspect those pull requests. If the example specifies only `RepositoryId`, use it only when it matches the current entry's `RepositoryId`, and inspect the specified pull request numbers in the current target GitHub repository. Do not perform broad repository searches for examples. If no guidance exists for the domain, continue without prior PR examples and include that in your final summary.
5. Diagnose why the job is failing network isolation. Use `DomainName`, `TaskName`, the located pipeline code, nearby allowlist/network-isolation patterns in the checked-out repository, and relevant prior PR examples.
6. Code the smallest targeted fix in the target repository. Preserve the repository's existing pipeline style and avoid unrelated formatting or refactors.
  - Do not resolve the issue by setting `networkIsolationPolicy: Permissive`, changing an existing policy to `Permissive`, disabling network isolation, or otherwise bypassing enforcement. A permissive policy is not an acceptable remediation. Find and implement the narrow allowlist, endpoint, service connection, feed, or pipeline configuration change that preserves network isolation.
7. Create a new branch in the local target repository checkout for only that entry. Use a branch name that starts with `network-isolation-cfs-` and includes the pipeline definition ID and the build ID.
8. Commit the fix locally on that branch. Do not run `git push`, `gh auth login`, `gh pr create`, or any GitHub write operation. GitHub Actions post-steps will push the branch, create the pull request, and request the reviewer.
9. Append a remediation object to `/tmp/network-isolation-remediations/remediations.json`. Create the directory and file if needed. The file must be a JSON array. Each object must include `repo`, `workingDirectory`, `branch`, `title`, and `body`. Use `repo` in `owner/repo` format, set `workingDirectory` to the local checkout path containing the committed branch, and make the pull request title include `CFS`. The pull request body must include:
  - the network isolation domain being fixed,
  - the affected job name,
  - the pipeline entry point YAML path,
  - the pipeline run URL,
  - a short explanation of the change.
10. Reviewer and assignee assignment for `chidozieononiwu` is handled by GitHub Actions post-steps. If you know either assignment is likely to fail, include that limitation in your final summary.
11. Clear your working context for the completed entry before moving to the next one.

## Azure DevOps Guidance

The setup steps already resolved Azure DevOps pipeline metadata into `devops-pipeline-entrypoints.json`. Do not make additional Azure DevOps calls unless the entry is missing required metadata that can only be recovered from Azure DevOps.

Use the Azure DevOps organization from the workflow input when needed; the default is `azure-sdk`.

## Output

When all entries are processed, call `noop` once with a concise final summary. Include:

- total entries read,
- entries with local committed remediations staged for GitHub Actions,
- target repositories and branches staged,
- entries skipped and why,
- entries attempted but not completed and why.

Do not create pull requests yourself. GitHub Actions post-steps consume `/tmp/network-isolation-remediations/remediations.json` after the agent exits and handle push, pull request creation, and reviewer assignment.

The post-steps use the repository's `login-to-github` action to mint an Azure GitHub App token for pull request creation after logging in with the AzureSDKEngKeyVault federated identity. Do not use `GH_AW_GITHUB_TOKEN` for pull request creation.

Never use `networkIsolationPolicy: Permissive` as the remediation. Treat permissive mode, disabling network isolation, or weakening the enforcement policy as an invalid fix unless the user explicitly requests that bypass in a future run.

Do not process entries concurrently. Do not reuse a checkout, branch, or diagnosis from one entry for another entry.