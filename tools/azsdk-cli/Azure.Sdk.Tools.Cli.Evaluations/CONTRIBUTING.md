# Contributing to Azure SDK Tools CLI Evaluations

Welcome to the Azure SDK Tools CLI Evaluations project! This framework tests and validates the behavior of AI agents interacting with Azure SDK tools through the Model Context Protocol (MCP). This guide will help you understand how to contribute effectively.

## Table of Contents

- [Understanding Evaluation Scenarios](#understanding-evaluation-scenarios)
- [Creating New Scenarios](#creating-new-scenarios)
- [Repository-Specific Tests](#repository-specific-tests)
- [Custom Evaluators](#custom-evaluators)

## Understanding Evaluation Scenarios

### Scenario Components

Each evaluation scenario comprises of:

#### 1. Chat History
The conversation context leading up to the test, including system instructions and any prior user-assistant exchanges.

#### 2. Next Message
The user prompt or request that triggers the agent behavior being tested.

#### 3. Expected Outcome
The anticipated agent response, focusing primarily on tool usage rather than textual responses. Key evaluation areas include:

- **Correct Tool Invocation**: Validates that the agent calls the appropriate tools
- **Proper Sequencing**: Ensures tools are used in the correct order when sequencing matters  
- **Parameter Accuracy**: Verifies that tool input parameters are correct and complete

### Scenario Structure

Each scenario has three main components:

```csharp
var scenarioData = new ScenarioData
{
    ChatHistory = historyMessages,      // Context leading to the test
    NextMessage = userPrompt,           // The user request being tested
    ExpectedOutcome = expectedMessages  // Expected agent response with tool calls
};
```

## Creating New Scenarios

This guide walks you through creating evaluation scenarios for the Azure SDK MCP agent.

### Step 1: Choose Your Approach

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
```csharp
[Test]
public async Task Evaluate_MyNewScenario()
{
    var filePath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", "example.json");
    var scenarioData = await SerializationHelper.LoadScenarioFromChatMessagesAsync(filePath);
    // ... rest of evaluation logic
}
```

### Step 2: Create a New Test Method

1. **Add a new test method** to an existing scenario class or create a new one:

```csharp
[Test]
[Category("azure-rest-api-specs")]  // Optional: Add category for repository-specific tests
public async Task Evaluate_YourScenarioName()
{
    const string prompt = "Generate SDK for my TypeSpec project";
    
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

### Step 3: Create Tool Mocks (If Needed)

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

### Step 4: Test Your Scenario

```bash
# Run your specific test
dotnet test --filter "TestName~YourScenarioName"

# Run all tests
dotnet test
```

## Repository-Specific Tests

You can create tests that only run for specific repositories using NUnit categories. This is useful when testing features that are only relevant to certain Azure SDK repositories.

### Adding Repository Categories

Use the `[Category]` attribute to specify which repositories a test should run for:

```csharp
[Test]
[Category("azure-rest-api-specs")]
public async Task Evaluate_SpecificToRestApiSpecs()
{
    // This test only runs when REPOSITORY_NAME contains "azure-rest-api-specs"
}

[Test]
[Category("azure-sdk-for-net")]
[Category("azure-sdk-for-python")]
public async Task Evaluate_MultipleRepos()
{
    // This test runs for both .NET and Python SDK repositories
}

[Test]
public async Task Evaluate_RunsEverywhere()
{
    // Tests without categories run for all repositories
}
```

## Custom Evaluators

### Available Evaluators

#### Current Custom Evaluators

- **ExpectedToolInputEvaluator**: Validates that the agent calls the correct tools with the proper input parameters in the expected sequence.

#### Built-in Library Evaluators

The Microsoft.Extensions.AI.Evaluation library provides several built-in evaluators. Here are the top 5 most useful for our scenarios:

- **ToolCallAccuracyEvaluator**: Evaluates an AI system's effectiveness at using the tools supplied to it
- **RelevanceEvaluator**: Measures how relevant the response is to the user's query
- **GroundednessEvaluator**: Checks if the response is grounded in the provided context
- **CoherenceEvaluator**: Assesses how well the response flows logically
- **IntentResolutionEvaluator**: Evaluates an AI system's effectiveness at identifying and resolving user intent

For a complete list of built-in evaluators, see the [Microsoft.Extensions.AI.Evaluation.Quality Namespace documentation](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.evaluation.quality?view=net-9.0-pp).

### Creating Custom Evaluators

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
        
        // Example: Check if the response contains certain keywords
        bool hasExpectedContent = modelResponse.Choices
            .Any(choice => choice.Message.Text?.Contains("expected keyword") == true);
            
        metric.Value = hasExpectedContent;
        return new ValueTask<EvaluationResult>(new EvaluationResult(metric));
    }
}
```

Additional custom evaluators can be created to explore different evaluation areas, such as:
- Evaluating the responses returned by MCP tools
- Custom overall confidence of response
- etc...