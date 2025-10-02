# Azure SDK MCP Agent Evaluation Framework

## Overview

The Azure SDK MCP (Model Context Protocol) Agent Evaluation Framework is a comprehensive testing and evaluation system designed to assess the performance and reliability of our MCP agent. This framework enables automated evaluation of agent behavior by simulating real copilot interactions and validating expected outcomes.

The primary objective is to provide a robust evaluation system that can quantify the impact of instruction changes on copilot's behavior, ensuring continued agent reliability and improvement.

## Scenario Components

Each evaluation scenario comprises of:

### 1. Copilot Chat History
A transcript of a copilot session that serves as the context for evaluation. These histories are selected from effective interactions and represent ideal scenarios for agent behavior simulation. **For now** as (Copilot Chat History Exports)[https://github.com/Azure/azure-sdk-tools/issues/11920#issue-3377002616] is worked on. 

**Example Structure:**
```json
[
  {
    "role": "system",
    "authorName": "GitHub_Copilot",
    "messageId": "msg_001",
    "contents": ["System instructions and context"]
  },
  {
    "role": "user", 
    "authorName": "developer",
    "messageId": "msg_002",
    "contents": ["User request or question"]
  },
  {
    "role": "assistant",
    "authorName": "GitHub_Copilot", 
    "messageId": "msg_003",
    "contents": ["Assistant response with tool calls"]
  }
]
```

### 2. Expected Outcome
Focused evaluation criteria that primarily assess the agent's tool usage rather than textual responses. Key evaluation areas include:

- **Correct Tool Invocation**: Validates that the agent calls the appropriate tools
- **Proper Sequencing**: Ensures tools are used in the correct order when sequencing matters  
- **Parameter Accuracy**: Verifies that tool input parameters are correct and complete

## Getting Started

### Environment Variables

The following environment variables need to be configured for the evaluation framework:

```bash
# Azure OpenAI Configuration
AZURE_OPENAI_ENDPOINT=https://your-openai-resource.openai.azure.com/
AZURE_OPENAI_MODEL_DEPLOYMENT_NAME=your-deployment-name

# MCP Server Configuration
LOCAL_MCP_POWERSHELL_SCRIPT_PATH=path/to/your/mcp-script.ps1

# Instruction File Paths
COPILOT_INSTRUCTIONS_PATH=path/to/copilot-instructions.md
AZSDK_TOOLS_INSTRUCTIONS_PATH=path/to/azsdk-tools/instructions
```

## Running Evaluations

```bash
dotnet test
```

## Extending the Framework

### Adding New Scenarios

1. Create scenario data in `TestData/` directory
2. Implement test method in appropriate scenario class
3. Define expected outcomes and evaluation criteria
4. Add any custom evaluators if needed or use built in through Microsoft.Extensions.Ai.Quality

### Custom Evaluators

Implement the `IEvaluator` interface to create custom evaluation logic:

```csharp
public class CustomEvaluator : IEvaluator
{
    public IReadOnlyCollection<string> EvaluationMetricNames => ["CustomMetric"];
    
    public ValueTask<EvaluationResult> EvaluateAsync(
        IEnumerable<ChatMessage> messages,
        ChatResponse modelResponse,
        // ... additional parameters
    )
    {
        // Custom evaluation logic
    }
}
```

Samples available [here](https://github.com/dotnet/ai-samples/tree/main/src/microsoft-extensions-ai-evaluation/api).