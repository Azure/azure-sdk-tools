# Azure SDK MCP Agent Evaluation Framework

## Table of Contents

- [Overview](#overview)
- [Architecture Overview](#architecture-overview)
- [Getting Started](#getting-started)
  - [Prerequisites](#prerequisites)
  - [Environment Variables](#environment-variables)
  - [Running Evaluations](#running-evaluations)
  - [Pipeline Integration](#pipeline-integration)
- [Walkthrough: Release Plan Creation Evaluation](#walkthrough-release-plan-creation-evaluation)
  - [The User Scenario](#the-user-scenario)
  - [Expected Behavior](#expected-behavior)
- [Contributing](#contributing)
- [Best Practices](#best-practices)
- [Related Documentation](#related-documentation)

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
AZURE_OPENAI_ENDPOINT=https://openai-shared.openai.azure.com/
AZURE_OPENAI_MODEL_DEPLOYMENT_NAME=gpt-5
REPOSITORY_NAME=Owner/RepoName 
COPILOT_INSTRUCTIONS_PATH_MCP_EVALS=local/path/to/.github/copilot-instructions.md
```

#### Note
- The Azure OpenAI endpoint must have a `text-embedding-3-large` deployment configured. This is required by the `ToolDescriptionSimilarityEvaluator` for embedding-based similarity tests. Without this deployment, the tests will fail.
- Must have `Azure AI User` role to access the Azure OpenAI resource.

### Running Evaluations

#### Using command line
```bash
cd local/path/to/Azure.Sdk.Tools.Cli.Evaluations
dotnet test
```

#### Using Test Explorer (Preferred)

- Visual Studio (Test Explorer):
  1. Open this repository in Visual Studio.
  2. Go to `Test > Test Explorer`.
  3. Expand `Azure.Sdk.Tools.Cli.Evaluations` and click `Run All`, or right‑click a test/class to run/debug just that scope.

### Pipeline Integration

The evaluation framework runs automatically in CI/CD pipelines when pull requests modify azsdk cli, changes to `.github/copilot-instructions.md`, or any instruction files in `eng/common/instructions/`. This ensures instruction or mcp changes don't negatively impact agent behavior. Configuration can be found at [eng\common\pipelines\ai-evals-tests.yml](https://github.com/Azure/azure-sdk-tools/blob/main/eng/common/pipelines/ai-evals-tests.yml).

| Copilot Instruction Repository | Pipeline Link |
|-------------------------------|---------------|
| Release Pipeline              | https://dev.azure.com/azure-sdk/internal/_build?definitionId=7684 |
| azure-rest-api-specs          | https://dev.azure.com/azure-sdk/internal/_build?definitionId=7985 |
| azure-sdk-for-js              | https://dev.azure.com/azure-sdk/internal/_build?definitionId=8011 |
| azure-sdk-for-go              | https://dev.azure.com/azure-sdk/internal/_build?definitionId=8010 |
| azure-sdk-for-java            | https://dev.azure.com/azure-sdk/internal/_build?definitionId=8008 |
| azure-sdk-for-net             | https://dev.azure.com/azure-sdk/internal/_build?definitionId=8009 |
| azure-sdk-for-python          | https://dev.azure.com/azure-sdk/internal/_build?definitionId=8007 |

> Note: In the release pipeline, evaluations run as one job under the `BuildTestAndPackage` stage.

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

## Best Practices

To ensure evaluations reflect the intended Copilot behavior:
- Release any azsdk-cli tool changes that impact Copilot Instructions before updating the instructions.
- Significant tool updates or newly added tools must be released for their behavior to appear in Copilot Instructions and be validated by evaluations.

### Tool Discoverability Testing

When creating new MCP tools, add test prompts to [`TestData/TestPrompts.json`](TestData/TestPrompts.json) to validate that LLM agents can discover your tool from natural language queries. See the [New Tool Development Guide](../docs/new-tool.md#add-test-prompts-for-tool-discoverability) for details.

```bash
# Run tool discoverability tests
dotnet test Azure.Sdk.Tools.Cli.Evaluations --filter "Name~Evaluate_PromptToToolMatch"

# Verify all tools have test prompts
dotnet test Azure.Sdk.Tools.Cli.Evaluations --filter "Name~AllToolsHaveTestPrompts"
```

## Related Documentation

- [Microsoft.Extensions.AI Evaluation Samples](https://github.com/dotnet/ai-samples/tree/main/src/microsoft-extensions-ai-evaluation/api)