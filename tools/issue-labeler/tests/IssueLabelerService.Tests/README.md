# MCP Labeler Accuracy Evaluation

This test harness evaluates the accuracy of the RAG-based McpOpenAiLabeler against a ground truth dataset of manually labeled issues.

## Components

### 1. Ground Truth Dataset (`McpGroundTruthDataset.cs`)
Contains 15 hand-crafted test cases covering:
- **Tool-specific issues**: Blob Storage, Key Vault, ARM, Cosmos DB, SQL, Service Bus, Event Grid, Monitoring, App Configuration
- **General server issues**: Configuration, network, startup errors (Tool = UNKNOWN)
- **Edge cases**: Multiple tools mentioned, feature requests, ambiguous scenarios

Each test case includes:
- Issue title and body
- Expected Server label (always `server-Azure.Mcp`)
- Expected Tool label (e.g., `tools-Storage`, `tools-KeyVault`, or `UNKNOWN`)
- Notes explaining the scenario

### 2. Evaluation Models (`McpTestModels.cs`)
- **McpTestCase**: Ground truth test case
- **McpPredictionResult**: Result from a single prediction
- **McpEvaluationMetrics**: Aggregated accuracy metrics

### 3. Evaluator (`McpLabelerEvaluator.cs`)
Runs predictions and calculates:
- **Server Label Accuracy**: % of correct server labels
- **Tool Label Accuracy**: % of correct tool labels  
- **Combined Accuracy**: % where both labels are correct
- **Success Rate**: % of predictions that didn't error
- **RAG Metrics**: Average RAG results, zero-result rate

### 4. Test Runner (`Program.cs`)
Standalone console app for running evaluations.

### 5. NUnit Tests (`McpLabelerAccuracyTests.cs`)
Integration tests for CI/CD pipelines.

## Usage

### Quick Start (Smoke Tests)

```bash
# Build the test project
cd tests/IssueLabelerService.Tests
dotnet build

# Run smoke tests (4 test cases)
dotnet run

# Expected output:
# Server Accuracy: 100%
# Tool Accuracy: 100%  
# Combined Accuracy: 100%
```

### Full Evaluation (15 Test Cases)

```bash
dotnet run -- --full

# Expected output:
# Server Accuracy: ≥90%
# Tool Accuracy: ≥85%
# Combined Accuracy: ≥80%
```

### Export Ground Truth to JSON

```bash
dotnet run -- --export-only

# Creates: mcp_ground_truth.json
```

### Custom Output Path

```bash
dotnet run -- --full --output=results/my_results.csv
```

## Configuration

The evaluator needs these settings (from `local.settings.json` or environment variables):

```json
{
  "microsoft/mcp:IndexName": "microsoft-mcp-triage-index",
  "microsoft/mcp:SemanticName": "issue-semantic-config",
  "microsoft/mcp:LabelModelName": "gpt-4o",
  "microsoft/mcp:SourceCount": "5",
  "microsoft/mcp:ScoreThreshold": "0.7",
  "microsoft/mcp:ConfidenceThreshold": "0.7",
  "OpenAIEndpoint": "https://your-openai.openai.azure.com/",
  "SearchServiceEndpoint": "https://your-search.search.windows.net"
}
```

## Running as NUnit Tests

```bash
# Run all tests
dotnet test

# Run specific test
dotnet test --filter "FullyQualifiedName~EvaluateSmokeTests"

# Run only integration tests (requires live Azure resources)
dotnet test --filter "Category=Integration"
```

**Note**: Integration tests are marked `[Explicit]` and require live Azure resources.

## Metrics Explained

### Server Label Accuracy
```
Server Accuracy = (Correctly predicted server labels) / (Total test cases)
```
Should be ≥90% (most issues should get `server-Azure.Mcp`)

### Tool Label Accuracy  
```
Tool Accuracy = (Correctly predicted tool labels) / (Total test cases)
```
Should be ≥85% (harder due to UNKNOWN cases and tool ambiguity)

### Combined Accuracy
```
Combined Accuracy = (Both labels correct) / (Total test cases)
```
Should be ≥80% (realistic target for production quality)

### Success Rate
```
Success Rate = (Successful predictions) / (Total test cases)
```
Should be ≥95% (predictions should rarely throw exceptions)

## Output Files

### CSV Export (`mcp_evaluation_results.csv`)
```csv
IssueNumber,Title,ExpectedServer,PredictedServer,ServerCorrect,ExpectedTool,PredictedTool,ToolCorrect,BothCorrect,RagResults,Error
1,"Blob storage tool returns 404",server-Azure.Mcp,server-Azure.Mcp,true,tools-Storage,tools-Storage,true,true,5,""
2,"Authentication fails...",server-Azure.Mcp,server-Azure.Mcp,true,tools-KeyVault,tools-KeyVault,true,true,3,""
...
```

Use for analysis in Excel, Python pandas, or visualization tools.

### JSON Export (`mcp_ground_truth.json`)
All test cases in JSON format for external tools.

## Adding New Test Cases

Edit `McpGroundTruthDataset.cs`:

```csharp
new McpTestCase
{
    IssueNumber = 16,
    Title = "Your issue title",
    Body = "Detailed description...",
    ExpectedServerLabel = "server-Azure.Mcp",
    ExpectedToolLabel = "tools-YourTool",  // or "UNKNOWN"
    Notes = "Why this is a good test case"
}
```

**Guidelines for Good Test Cases:**
- Cover common error scenarios users actually encounter
- Include enough detail for RAG to find similar issues
- Test edge cases: unclear tool, multiple tools, general errors
- Verify manually that your expected labels are correct

## Improving Accuracy

If accuracy is below thresholds:

### Low Server Accuracy (<90%)
- Check if RAG is returning ANY results (might be index issue)
- Verify semantic configuration exists in index
- Check if prompt is asking for server label correctly

### Low Tool Accuracy (<85%)
- **UNKNOWN cases**: Ensure prompt handles "no specific tool" correctly
- **Ambiguous tools**: Add more specific examples to ground truth
- **RAG precision**: Lower `ScoreThreshold` to get more context (but watch for noise)
- **LLM confusion**: Improve system prompt with clearer tool descriptions

### Low Combined Accuracy (<80%)
- Focus on improving tool accuracy (server should already be high)
- Check if failed cases have low RAG result counts (0-2 results)
- Consider tuning `SourceCount` (number of RAG results to include)

### High Failure Rate (errors/exceptions)
- Check Azure service health (Search, OpenAI)
- Verify authentication/credentials
- Check rate limiting (retry logic)

## Interpreting Results

### Example Good Result
```
Total Test Cases: 15
Successful Predictions: 15 (100%)
Server Accuracy: 93.33%
Tool Accuracy: 86.67%
Combined Accuracy: 86.67%
Average RAG Results: 4.2
```
✅ Ready for production

### Example Needs Improvement
```
Total Test Cases: 15  
Successful Predictions: 14 (93.33%)
Server Accuracy: 85.71%
Tool Accuracy: 71.43%
Combined Accuracy: 64.29%
Average RAG Results: 1.8
```
❌ Issues:
- One prediction failed (error)
- Server accuracy below 90%
- Tool accuracy below 85%
- Low RAG results suggest index might need more data

## Integration with CI/CD

Add to your pipeline:

```yaml
- name: Run MCP Labeler Accuracy Tests
  run: |
    cd tests/IssueLabelerService.Tests
    dotnet test --filter "Category=Integration" --logger "trx;LogFileName=test-results.trx"
    
- name: Upload Test Results
  uses: actions/upload-artifact@v3
  with:
    name: mcp-evaluation-results
    path: tests/IssueLabelerService.Tests/mcp_evaluation_results.csv
```

## Troubleshooting

### "Labeler must be initialized" Error
The `Program.cs` `CreateLabeler()` method is a template. You need to properly instantiate `McpOpenAiLabeler` with:
- Azure Search client
- OpenAI client
- Configuration
- Logger

Refer to `AzureSdkIssueLabelerService.cs` for dependency injection setup.

### "Unknown semantic configuration" Error
Check that `microsoft/mcp:SemanticName` matches the actual semantic config in your index:
```bash
az search index show --name microsoft-mcp-triage-index --service-name your-search
```

### RAG Returns 0 Results
- Verify index has documents: `az search index statistics`
- Check embedding model matches (`text-embedding-3-large` with 3072 dimensions)
- Test a direct search query in Azure Portal

## Next Steps

1. **Baseline Run**: Get initial accuracy metrics
2. **Tune Thresholds**: Adjust `ScoreThreshold` and `ConfidenceThreshold`
3. **Expand Dataset**: Add more test cases based on real issues
4. **Continuous Monitoring**: Run tests after each change to RAG/LLM logic
5. **A/B Testing**: Compare different prompts, models, or RAG strategies
