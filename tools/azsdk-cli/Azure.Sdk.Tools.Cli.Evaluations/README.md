# Azure SDK MCP Agent Evaluation Framework

## Overview

The Azure SDK MCP (Model Context Protocol) Agent Evaluation Framework is a comprehensive testing and evaluation system designed to assess the performance and reliability of our MCP agent. This framework enables automated evaluation of agent behavior by simulating real copilot interactions and validating expected outcomes.

The primary objective is to provide a robust evaluation system that can quantify the impact of instruction changes on copilot's behavior, ensuring continued agent reliability and improvement.

## Architecture Overview

The framework consists of several key components:

- **Scenarios**: Test cases that define user prompts and expected tool calls
- **Evaluators**: Components that assess whether the agent's behavior matches expectations
- **Tool Mocks**: Simulated tool responses for consistent testing
- **Chat Completion**: LLM integration for agent behavior simulation
- **Reporting**: HTML reports showing evaluation results and metrics

## Getting Started

### Prerequisites

1. **.NET 8.0 SDK** - Required for building and running the evaluation framework
2. **Azure OpenAI Service** - For LLM integration
3. **MCP Server** - The Azure SDK MCP server for tool interaction
4. **PowerShell** - Required for MCP server execution

### Environment Variables

Configure the following environment variables for the evaluation framework:

#### Required Variables

```bash
AZURE_OPENAI_ENDPOINT=https://your-openai-resource.openai.azure.com/
AZURE_OPENAI_MODEL_DEPLOYMENT_NAME=your-deployment-name
REPOSITORY_NAME=Owner/RepoName 
COPILOT_INSTRUCTIONS_PATH_MCP_EVALS=path/to/.github/copilot-instructions.md
```

**Note**: The Azure OpenAI endpoint must have a `text-embedding-3-large` deployment configured. This is required by the `ToolDescriptionSimilarityEvaluator` for embedding-based similarity tests. Without this deployment, the tests will fail.

### Running Evaluations

```bash
# Run all evaluation scenarios
dotnet test
```

The framework automatically generates HTML reports in the `reports/` directory after test execution.

### Pipeline Integration

The evaluation framework runs automatically in CI/CD pipelines when pull requests modify azsdk cli. Changes to `.github/copilot-instructions.md` or any instruction files in `eng/common/instructions/` would also be triggered but in progress. This ensures instruction or mcp changes don't negatively impact agent behavior before merging. The evaluations run alongside other PR validation tests and must pass for the PR to be merged.

**Pipeline**: [release pipeline](https://dev.azure.com/azure-sdk/internal/_build?definitionId=7684) - Configuration in `eng/common/pipelines/copilot-instruction-evals.yml`

## Walkthrough: Release Plan Creation Evaluation

To understand how the evaluation framework works, let's walk through a complete example that tests the AI agent's ability to create a release plan from a user request.

### The User Scenario

A developer needs to create a release plan for a new Azure service. They provide all the necessary details in a natural language request:

```text
Create a release plan for the Contoso Widget Manager, no need to get it afterwards only create. 
Here is all the context you need: TypeSpec project located at 
"c:\azure-rest-api-specs\specification\contosowidgetmanager\Contoso.WidgetManager". 
Use service tree ID "a7f2b8e4-9c1d-4a3e-b6f9-2d8e5a7c3b1f", 
product tree ID "f1a8c5d2-6e4b-4f7a-9c2d-8b5e1f3a6c9e", 
target release timeline "December 2025", 
API version "2022-11-01-preview", 
SDK release type "beta", 
and link it to the spec pull request "https://github.com/Azure/azure-rest-api-specs/pull/38387".
```

### Expected Behavior

The evaluation framework expects the AI agent to:

1. **Parse the request** and extract all the necessary parameters
2. **Call the correct tool**: `azsdk_create_release_plan`
3. **Use proper parameters**:
   ```json
   {
     "typeSpecProjectPath": "c:\\azure-rest-api-specs\\specification\\contosowidgetmanager\\Contoso.WidgetManager",
     "targetReleaseMonthYear": "December 2025",
     "serviceTreeId": "a7f2b8e4-9c1d-4a3e-b6f9-2d8e5a7c3b1f",
     "productTreeId": "f1a8c5d2-6e4b-4f7a-9c2d-8b5e1f3a6c9e",
     "specApiVersion": "2022-11-01-preview",
     "specPullRequestUrl": "https://github.com/Azure/azure-rest-api-specs/pull/38387",
     "sdkReleaseType": "beta",
     "isTestReleasePlan": false
   }
   ```
## Contributing

Want to add new evaluation scenarios or improve existing ones? See our detailed [Contributing Guide](CONTRIBUTING.md) for:

- Step-by-step instructions for creating new scenarios
- Best practices for tool mock development
- Framework architecture details
- Testing and debugging guidelines

## Related Documentation

- [Microsoft.Extensions.AI Evaluation Samples](https://github.com/dotnet/ai-samples/tree/main/src/microsoft-extensions-ai-evaluation/api)