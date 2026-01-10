# MCP Labeler Evaluator

A console application for testing and evaluating the accuracy of the MCP issue labeler against real-world labeled GitHub issues.

## Overview

This tool tests the MCP labeler's ability to correctly predict Server and Tool labels for issues in the `microsoft/mcp` repository. It compares predictions against ground truth labels from closed, labeled issues.

## Usage Modes

### 1. Single Issue Prediction

Predict labels for one or more specific issues:

```powershell
dotnet run --issue=1004
dotnet run --issue=1004,1422,850
```

**Output:**
- Displays issue title and body
- Shows predicted Server and Tool labels with confidence scores
- No accuracy calculation (no ground truth comparison)

### 2. Batch Evaluation

Evaluate labeler accuracy against the test dataset:

```powershell
dotnet run
```

**What it does:**
- Loads test cases from `real_mcp_issues.json`
- Runs predictions for each issue
- Calculates accuracy metrics
- Exports results to `mcp_evaluation_results.csv`
- **Exit code 0** if accuracy ≥ 80%, **exit code 1** otherwise

**Metrics reported:**
- Server label accuracy
- Tool label accuracy  
- Both labels correct (strict accuracy)
- Per-label confusion analysis
- Failure report for incorrect predictions

### 3. Extract Test Dataset

Extract labeled issues from the Azure Search index to create/update test data:

```powershell
dotnet run --extract-real
```

**What it does:**
- Queries Azure Search for all indexed MCP issues with Server and Tool labels
- Saves to `real_mcp_issues.json` as test dataset
- Use this periodically to refresh ground truth data

## Configuration

### Required Settings

Create `appsettings.json`:

```json
{
  "OpenAIEndpoint": "https://<your-openai>.openai.azure.com/",
  "SearchServiceEndpoint": "https://<your-search>.search.windows.net",
  "BlobAccountUri": "https://<your-storage>.blob.core.windows.net",
  
  "microsoft/mcp:LabelPredictor": "McpOpenAI",
  "microsoft/mcp:IndexName": "microsoft-mcp-triage-index",
  "microsoft/mcp:SemanticName": "issue-semantic-config",
  "microsoft/mcp:IssueIndexFieldName": "TextVector",
  "microsoft/mcp:SourceCount": "15",
  "microsoft/mcp:ScoreThreshold": "0.75",
  "microsoft/mcp:ConfidenceThreshold": "0.9",
  "microsoft/mcp:LabelModelName": "gpt-5.1",
  "microsoft/mcp:LabelInstructions": "You are an expert at categorizing GitHub issues...",
  "microsoft/mcp:RepoOwner": "microsoft",
  "microsoft/mcp:RepoNames": "mcp",
  "microsoft/mcp:EnableLabels": "true",
  "microsoft/mcp:EnableAnswers": "false"
}
```

### Key Configuration Parameters

| Parameter | Description | Default/Example |
|-----------|-------------|-----------------|
| `LabelPredictor` | Predictor implementation to use | `McpOpenAI` |
| `IndexName` | Azure Search index name | `microsoft-mcp-triage-index` |
| `SourceCount` | Number of similar issues to retrieve | `15` |
| `ScoreThreshold` | Minimum similarity score for retrieval | `0.75` |
| `ConfidenceThreshold` | Minimum confidence for label prediction | `0.9` |
| `LabelModelName` | Azure OpenAI chat model | `gpt-5.1` |
| `LabelInstructions` | System prompt for label prediction | Custom instructions |

### Tuning for Better Accuracy

**If predictions are too conservative (missing labels):**
- Lower `ConfidenceThreshold` (e.g., from 0.9 to 0.8)
- Lower `ScoreThreshold` (e.g., from 0.75 to 0.7)
- Increase `SourceCount` (e.g., from 15 to 20)

**If predictions are incorrect (wrong labels):**
- Increase `ConfidenceThreshold` (more selective)
- Increase `ScoreThreshold` (only very similar issues)
- Refine `LabelInstructions` prompt
- Ensure RerankerThreshold in knowledge agent is appropriate (see SearchIndexCreator README)

## Test Data Format

`real_mcp_issues.json` contains test cases with ground truth:

```json
[
  {
    "IssueNumber": 1004,
    "Title": "Issue title here",
    "Body": "Issue body/description",
    "ExpectedServerLabel": "server-azure",
    "ExpectedToolLabel": "tools-Storage",
    "Notes": "Optional notes about this test case"
  }
]
```

## Output Files

### `mcp_evaluation_results.csv`

CSV export of all predictions with columns:
- IssueNumber
- Title
- ExpectedServer, PredictedServer, ServerCorrect
- ExpectedTool, PredictedTool, ToolCorrect  
- BothCorrect
- Error (if prediction failed)

Use for detailed analysis in Excel or other tools.

## Understanding Results

### Good Accuracy Indicators
- Server accuracy > 90%
- Tool accuracy > 85%
- Both correct > 85%

This evaluator uses the **same labeler logic** as the production Azure Function (`IssueLabelerService`), but runs locally for testing and operates on historical closed issues (ground truth known)

Use this tool to validate changes before deploying to production.

## Known Limitations

- Only tests MCP repository labeling (not Azure SDK repos)
- Requires issues to already be indexed in Azure Search
- Test dataset must be manually refreshed with `--extract-real`
- Cannot test issues without Server/Tool labels
- Assumes ground truth labels in search index are correct
