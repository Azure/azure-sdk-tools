# Azure Pipeline Analysis Integration

This document describes the integration between the Azure SDK QA Bot Backend and the Azure SDK Tools CLI for analyzing Azure DevOps pipeline failures.

## Overview

The integration automatically detects Azure DevOps pipeline links in the `AdditionalInfo` provided to the completion endpoint and uses the `azsdk` CLI tool to analyze pipeline failures.

## How It Works

1. **Link Detection**: When a user provides a link in the `additional_infos` field, the system checks if it's an Azure DevOps pipeline link using the pattern:
   ```
   https://dev.azure.com/{org}/{project}/_build/results?buildId={buildId}
   ```

2. **Pipeline Analysis**: If a pipeline link is detected, the system calls the `azsdk` CLI tool:
   ```bash
   azsdk azp analyze {buildId} --agent
   ```

3. **Result Integration**: The analysis results (failed tests, failed tasks, error messages) are formatted and included in the context sent to the LLM, replacing the generic link content.

## Pipeline Analysis Features

The CLI tool provides:
- **Failed Test Detection**: Lists all failed tests grouped by test suite
- **Failed Task Analysis**: Analyzes logs from failed pipeline tasks
- **AI-Powered Insights**: Uses RAG (Retrieval-Augmented Generation) to provide intelligent analysis of errors
- **Error Categorization**: Identifies common error patterns and provides actionable suggestions

## Docker Integration

The `azsdk` CLI tool is built into the Docker image during the build process:

```dockerfile
# Stage 1: Build the .NET CLI tool
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS dotnet-builder
# ... builds the CLI tool ...

# Stage 3: Final runtime image includes both Go backend and .NET CLI
COPY --from=dotnet-builder /azsdk-cli/publish /usr/local/bin/azsdk-cli
COPY --from=1 /app/go-backend /app/go-backend
```

## Usage Example

### API Request
```json
{
  "messages": [
    {
      "role": "user",
      "content": "Why did this pipeline fail?"
    }
  ],
  "additional_infos": [
    {
      "type": "link",
      "link": "https://dev.azure.com/azure-sdk/internal/_build/results?buildId=5530426",
      "content": ""
    }
  ]
}
```

### Processing Flow
1. System detects the pipeline link
2. Calls `azsdk azp analyze 5530426 --agent`
3. Receives structured analysis with:
   - Failed tests
   - Failed task logs
   - AI-generated error analysis
4. Formats results and includes in LLM context
5. LLM provides informed response based on actual pipeline failure data

## Configuration

### Environment Variables
The CLI tool uses Azure authentication for accessing DevOps. Ensure the following are configured:
- Azure credentials (via Azure Identity, managed identity, or service principal)
- DevOps PAT token (if required)

### Required Permissions
The service account running the backend must have:
- Read access to Azure DevOps pipelines
- Access to pipeline logs
- (Optional) AI Foundry project access for enhanced analysis

## File Structure

```
utils/
  pipeline_analyzer.go       # Main pipeline analysis utilities
  pipeline_analyzer_test.go  # Unit tests for pipeline detection

service/agent/
  service.go                 # Integration point in the agent service

Dockerfile                   # Multi-stage build including CLI tool
```

## Testing

Run the unit tests to verify pipeline link detection:
```bash
go test ./utils -v -run TestIsPipelineLink
go test ./utils -v -run TestExtractBuildID
```

## Troubleshooting

### CLI Not Found
If the `azsdk` command is not found:
- Ensure the Dockerfile builds the CLI tool correctly
- Verify the PATH environment variable includes `/usr/local/bin/azsdk-cli`
- Check that the .NET runtime is installed in the final image

### Analysis Fails
If pipeline analysis fails:
- Check authentication/authorization to Azure DevOps
- Verify the build ID is valid and accessible
- Check logs for detailed error messages from the CLI tool

### Performance Considerations
- Pipeline analysis adds processing time (typically 5-15 seconds)
- Consider implementing caching for frequently accessed pipelines
- The `--agent` flag uses AI analysis which may take longer but provides better insights

## Future Enhancements

Potential improvements:
- Cache pipeline analysis results
- Support for partial log analysis (specific task logs)
- Integration with other DevOps platforms (GitHub Actions, GitLab CI)
- Custom query support for targeted analysis
