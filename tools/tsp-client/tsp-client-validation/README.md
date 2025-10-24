# TSP-Client Validation Automation

This directory contains automation scripts for validating tsp-client version updates across all Azure SDK repositories.

## Overview

When the tsp-client version is updated in `eng/common/tsp-client/package.json`, it creates sync PRs across all Azure SDK language repositories. This automation helps trigger and monitor the validation pipelines for these updates.

## Files

- **`Invoke-TspClientValidation.ps1`** - Main automation script (PowerShell)
- **`Example-Usage.ps1`** - Example script showing current scenario usage
- **`README.md`** - This documentation

## Prerequisites

1. **Azure CLI** with the `azure-devops` extension:

   ```bash
   az extension add --name azure-devops
   ```

2. **Azure DevOps Authentication** - Authenticate to the azure-sdk organization:

   ```bash
   az devops configure --defaults organization=https://dev.azure.com/azure-sdk
   ```

3. **Azure DevOps default project** - Set the default azure-sdk project to the `public` project id:

```bash
az devops configure --defaults project=29ec6040-b234-4e31-b139-33dc4287b756
```

4. **Permissions** - Access to trigger pipelines in the azure-sdk DevOps organization

## Usage

### Basic Usage

```powershell
.\Invoke-TspClientValidation.ps1 -PRNumber 12360 -SyncBranch "sync-eng/common-update-tsp-client-12360"
```

#### Example Script

```powershell
.\Example-Usage.ps1 -DryRun
```

### Language-Specific Validation

Validate only specific languages:

```powershell
.\Invoke-TspClientValidation.ps1 -PRNumber 12360 -SyncBranch "sync-eng/common-update-tsp-client-12360" -Languages @("Python", "NET")
```

### Dry Run

Preview what would be executed without actually triggering pipelines:

```powershell
.\Invoke-TspClientValidation.ps1 -PRNumber 12360 -SyncBranch "sync-eng/common-update-tsp-client-12360" -DryRun
```

### Monitor Existing Pipelines

Monitor the status of already-running pipelines:

```powershell
.\Invoke-TspClientValidation.ps1 -MonitorOnly
# Then enter build IDs when prompted: 5487215,5487216,5487217
```

## Parameters

| Parameter     | Required | Description                              | Example                                     |
| ------------- | -------- | ---------------------------------------- | ------------------------------------------- |
| `PRNumber`    | Yes\*    | PR number from azure-sdk-tools           | `12360`                                     |
| `SyncBranch`  | Yes\*    | Name of the sync branch                  | `"sync-eng/common-update-tsp-client-12360"` |
| `Languages`   | No       | Array of languages to validate           | `@("Python", "NET", "Java", "JS", "Go")`    |
| `DryRun`      | No       | Preview mode - doesn't trigger pipelines | `-DryRun`                                   |
| `MonitorOnly` | No       | Only monitor existing pipeline runs      | `-MonitorOnly`                              |

\*Not required when using `-MonitorOnly`

## Supported Languages

| Language   | Repository           | Pipeline ID |
| ---------- | -------------------- | ----------- |
| Python     | azure-sdk-for-python | 7519        |
| .NET       | azure-sdk-for-net    | 7516        |
| Java       | azure-sdk-for-java   | 7515        |
| JavaScript | azure-sdk-for-js     | 7518        |
| Go         | azure-sdk-for-go     | 7517        |

## Workflow

The script follows the process from `tspclient-automation-check.prompt.md`:

1. **Validates Prerequisites** - Checks Azure CLI and DevOps extension
2. **Maps Sync Branches** - Associates PR numbers with sync branch names
3. **Triggers Pipelines** - Executes SDK validation for each language
4. **Provides Monitoring** - Shows build status and URLs
5. **Generates Summary** - Reports results and monitoring commands

## Example Output

```
================================================================================
 TSP-CLIENT VALIDATION automation
================================================================================

✓ Checking prerequisites...
  ℹ Azure CLI is installed
  ℹ Azure DevOps extension is installed (version 1.0.1)

✓ Looking up sync PR information for PR #12360...
  ℹ Expected sync branch pattern: sync-eng/common-update-tsp-client-12360

================================================================================
 EXECUTION PLAN
================================================================================
PR Number: 12360
Sync Branch: sync-eng/common-update-tsp-client-12360
Languages: Python, NET, Java, JS, Go
Dry Run: False

Pipelines to trigger:
  Python: Pipeline ID 7519 (azure-sdk-for-python)
  NET: Pipeline ID 7516 (azure-sdk-for-net)

✓ Successfully triggered Python pipeline!
  ℹ Build ID: 5487215
  ℹ URL: https://dev.azure.com/azure-sdk/public/_build/results?buildId=5487215
```

## Troubleshooting

### "No pipelines are associated with this pull request"

This error occurs when trying to trigger pipelines from individual repository PRs. Use this script instead, which triggers from the central azure-sdk DevOps organization.

### "Could not queue the build because there were validation errors"

Check that:

- The sync branch exists in the azure-rest-api-specs repository
- You have proper permissions to trigger pipelines
- The pipeline IDs in `$PipelineMapping` variable are up to date

### Authentication Issues

Re-authenticate to Azure DevOps:

```bash
az login
az devops configure --defaults organization=https://dev.azure.com/azure-sdk
```

## Configuration Updates

If pipeline IDs change, update them in the `$PipelineMapping` variable. You can find current pipeline IDs with:

```bash
az pipelines list --output table | findstr "SDK Validation"
```

## Related Files

- Source prompt: `.github/prompts/tspclient-automation-check.prompt.md`
- TSP-client config: `eng/common/tsp-client/package.json`
