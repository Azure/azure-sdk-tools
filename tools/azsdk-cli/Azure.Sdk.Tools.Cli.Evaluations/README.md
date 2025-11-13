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

## Scenario Components

Each evaluation scenario comprises of:

### 1. Chat History
The conversation context leading up to the test, including system instructions and any prior user-assistant exchanges.

### 2. Next Message
The user prompt or request that triggers the agent behavior being tested.

### 3. Expected Outcome
The anticipated agent response, focusing primarily on tool usage rather than textual responses. Key evaluation areas include:

- **Correct Tool Invocation**: Validates that the agent calls the appropriate tools
- **Proper Sequencing**: Ensures tools are used in the correct order when sequencing matters  
- **Parameter Accuracy**: Verifies that tool input parameters are correct and complete

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
# Azure OpenAI Configuration
AZURE_OPENAI_ENDPOINT=https://your-openai-resource.openai.azure.com/
AZURE_OPENAI_MODEL_DEPLOYMENT_NAME=your-deployment-name
COPILOT_INSTRUCTIONS_REPOSITORY_NAME=azure-sdk-tools
COPILOT_INSTRUCTIONS_REPOSITORY_OWNER=Azure
```

**Optional Local File Path**
```bash
COPILOT_INSTRUCTIONS_PATH=path/to/copilot-instructions.md
```


### Running Evaluations

```bash
# Run all evaluation scenarios
dotnet test
```

The framework automatically generates HTML reports in the `reports/` directory after test execution.

## Creating New Scenarios

This guide walks you through creating evaluation scenarios for the Azure SDK MCP agent.

### Step 1: Understand the Scenario Structure

Each scenario has three main components:

```csharp
var scenarioData = new ScenarioData
{
    ChatHistory = historyMessages,      // Context leading to the test
    NextMessage = userPrompt,           // The user request being tested
    ExpectedOutcome = expectedMessages  // Expected agent response with tool calls
};
```

### Step 2: Choose Your Approach

There are two ways to create scenarios:

#### Option A: From Simple Prompt
```csharp
[Test]
public async Task Evaluate_MyNewScenario()
{
    const string prompt = "Your user request here";
    string[] expectedTools = ["tool1", "tool2"]; // Tools you expect to be called
    
    var scenarioData = await ChatMessageHelper.LoadScenarioFromPrompt(prompt, expectedTools);
    // ... rest of evaluation logic
}
```

#### Option B: From Chat History JSON (For complex scenarios)
Load pre-recorded chat conversations from JSON files in the `TestData/` directory.

### Step 3: Create a New Test Method

1. **Add a new test method** to an existing scenario class or create a new one:

```csharp
[Test]
public async Task Evaluate_YourScenarioName()
{
    // Define the user prompt
    const string prompt = "Generate SDK for my TypeSpec project";
    
    // Specify expected tools (in order they should be called)
    string[] expectedTools = 
    [
        "azsdk_typespec_check_project_in_public_repo",
        "azsdk_run_sdk_generation"
    ];

    // Build scenario data from prompt
    var scenarioData = ChatMessageHelper.LoadScenarioFromPrompt(prompt, expectedTools);

    // Configure input validation (set to false to skip parameter checking)
    bool checkInputs = true;

    // Run the evaluation
    var result = await EvaluationHelper.RunScenarioAsync(
        scenarioName: this.ScenarioName,
        scenarioData: scenarioData,
        chatCompletion: s_chatCompletion!,
        chatConfig: s_chatConfig!,
        executionName: s_executionName,
        reportingPath: ReportingPath,
        toolNames: s_toolNames!,
        evaluators: [new ExpectedToolInputEvaluator()],
        enableResponseCaching: true,
        additionalContexts: new EvaluationContext[]
        {
            new ExpectedToolInputEvaluatorContext(scenarioData.ExpectedOutcome, s_toolNames!, checkInputs)
        });

    // Assert the results
    EvaluationHelper.ValidateToolInputsEvaluator(result);
}
```

### Step 4: Create Tool Mocks (If Needed)

If your scenario uses tools that don't have mocks yet:

1. **Create a new mock class** implementing `IToolMock`:

```csharp
public class MyNewToolMock : IToolMock
{
    public string ToolName => "my_new_tool";
    public string CallId => "tooluse_UniqueId123";
    
    public ChatMessage GetMockCall()
    {
        return new ChatMessage(
            ChatRole.Assistant,
            [
                new FunctionCallContent(
                    CallId,
                    ToolName,
                    new Dictionary<string, object?>
                    {
                        { "parameter1", "expected_value" },
                        { "parameter2", 123 }
                    }
                )
            ]
        );
    }

    public ChatMessage GetMockResponse(string callid)
    {
        return new ChatMessage(
            ChatRole.Tool,
            [
                new FunctionResultContent(
                    callid,
                    """{"result": "Mock tool response"}"""
                )
            ]
        );
    }
}
```

2. **Register the mock** in `ToolMocks.cs`:

```csharp
private static void RegisterMocks()
{
    var mockInstances = new List<IToolMock>
    {
        // Existing mocks...
        new MyNewToolMock(), // Add your new mock here
    };
    // ...
}
```

## Custom Evaluator: ExpectedToolInputEvaluator

### Input Validation Control

Control whether tool parameters are validated:

```csharp
bool checkInputs = false; // Skip parameter validation for complex scenarios
var additionalContexts = new EvaluationContext[]
{
    new ExpectedToolInputEvaluatorContext(scenarioData.ExpectedOutcome, s_toolNames!, checkInputs)
};
```

### Features

The `ExpectedToolInputEvaluator` performs the following validations:

1. **Tool Call Presence**: Ensures the agent made the expected tool calls
2. **Tool Call Count**: Validates the correct number of tools were called
3. **Tool Call Sequence**: Verifies tools were called in the expected order
4. **Parameter Validation**: Compares actual tool parameters with expected values (when enabled)

### Custom Evaluators

You can also create custom evaluation logic by implementing `IEvaluator`:

```csharp
public class CustomEvaluator : IEvaluator
{
    public IReadOnlyCollection<string> EvaluationMetricNames => ["CustomMetric"];
    
    public ValueTask<EvaluationResult> EvaluateAsync(
        IEnumerable<ChatMessage> messages,
        ChatResponse modelResponse,
        ChatConfiguration? chatConfiguration = null,
        IEnumerable<EvaluationContext>? additionalContext = null,
        CancellationToken cancellationToken = default)
    {
        // Your custom evaluation logic here
        var metric = new BooleanMetric("CustomMetric");
        // ... evaluation logic
        return new ValueTask<EvaluationResult>(new EvaluationResult(metric));
    }
}
```


## Related Documentation

- [Microsoft.Extensions.AI Evaluation Samples](https://github.com/dotnet/ai-samples/tree/main/src/microsoft-extensions-ai-evaluation/api)