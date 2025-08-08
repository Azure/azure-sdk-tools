# Example Tool Documentation

The ExampleTool is a comprehensive demonstration of the azsdk-cli framework features and service integrations. It serves as a reference implementation for developers creating new tools.

## Overview

The ExampleTool showcases:
- Integration with multiple Azure and external services
- Command line interface with multiple sub-commands
- Model Context Protocol (MCP) server methods for LLM agents
- Proper error handling patterns
- Response model design and formatting
- Comprehensive logging practices
- Dependency injection patterns

## Command Structure

The tool is accessible via: `azsdk example demo [subcommand]`

### Sub-commands

#### 1. Azure Service Demo (`azure`)
**Usage:** `azsdk example demo azure <input> [--verbose]`

**Purpose:** Demonstrates Azure service integration and credential handling.

**Parameters:**
- `input` (required): Tenant information or identifier
- `--verbose, -v` (optional): Enable detailed logging

**Example:**
```bash
azsdk example demo azure "default-tenant" --verbose
```

**What it demonstrates:**
- Azure credential retrieval using `IAzureService`
- Token acquisition and validation
- Credential type identification
- Secure handling of authentication tokens

#### 2. DevOps Service Demo (`devops`)
**Usage:** `azsdk example demo devops <project-info> [--item-id <id>] [--verbose]`

**Purpose:** Shows Azure DevOps service integration patterns.

**Parameters:**
- `project-info` (required): Project identifier or name
- `--item-id, -i` (optional): Work item ID for specific operations
- `--verbose, -v` (optional): Enable detailed logging

**Example:**
```bash
azsdk example demo devops "azure-sdk-project" --item-id 12345 --verbose
```

**What it demonstrates:**
- DevOps service method patterns
- Work item handling
- Project information retrieval
- Service method simulation

#### 3. GitHub Service Demo (`github`)
**Usage:** `azsdk example demo github <operation> [--repo <owner/repo>] [--item-id <id>] [--verbose]`

**Purpose:** Demonstrates GitHub API integration capabilities.

**Parameters:**
- `operation` (required): Operation type (`user`, `pullrequest`/`pr`, `issue`)
- `--repo, -r` (optional): Repository in format `owner/repo`
- `--item-id, -i` (optional): Item ID (PR number, issue number)
- `--verbose, -v` (optional): Enable detailed logging

**Examples:**
```bash
# Get current user information
azsdk example demo github user --verbose

# Get pull request details
azsdk example demo github pullrequest --repo "azure/azure-sdk-tools" --item-id 123

# Get issue information
azsdk example demo github issue --repo "azure/azure-sdk-tools" --item-id 456
```

**What it demonstrates:**
- GitHub API client usage
- User authentication and info retrieval
- Pull request data access
- Issue tracking integration
- Repository content access patterns

#### 4. AI Service Demo (`ai`)
**Usage:** `azsdk example demo ai <user-prompt> [--prompt <custom-prompt>] [--verbose]`

**Purpose:** Shows Azure OpenAI service integration.

**Parameters:**
- `user-prompt` (required): User input for AI processing
- `--prompt, -p` (optional): Custom system prompt
- `--verbose, -v` (optional): Enable detailed logging

**Example:**
```bash
azsdk example demo ai "Explain dependency injection" --prompt "You are a coding mentor" --verbose
```

**What it demonstrates:**
- Azure OpenAI client usage
- Chat completion requests
- Token usage tracking
- Response handling and formatting
- AI service configuration patterns

#### 5. Error Handling Demo (`error`)
**Usage:** `azsdk example demo error <scenario> [--force-failure]`

**Purpose:** Demonstrates proper error handling patterns.

**Parameters:**
- `scenario` (required): Error scenario (`argument`, `timeout`, `notfound`, or any string)
- `--force-failure, -f` (optional): Force different error types

**Examples:**
```bash
# Normal successful operation
azsdk example demo error "normal-case"

# Simulated argument error
azsdk example demo error "argument" --force-failure

# Simulated timeout error
azsdk example demo error "timeout" --force-failure
```

**What it demonstrates:**
- Exception handling patterns
- Different error types and responses
- Logging for error scenarios
- Error response formatting
- Recovery patterns

## MCP Server Methods

The tool exposes several methods to LLM agents via the Model Context Protocol:

### `example_azure_service`
Demonstrates Azure authentication and credential management.
- **Parameters:** `tenantInfo` (string), `verbose` (bool, optional)
- **Returns:** `ExampleServiceResponse`

### `example_devops_service`
Shows DevOps service integration patterns.
- **Parameters:** `projectInfo` (string), `workItemId` (int, optional), `verbose` (bool, optional)
- **Returns:** `ExampleServiceResponse`

### `example_github_service`
Demonstrates GitHub API usage.
- **Parameters:** `operation` (string), `repository` (string, optional), `itemId` (int, optional), `verbose` (bool, optional)
- **Returns:** `ExampleServiceResponse`

### `example_ai_service`
Shows AI service integration.
- **Parameters:** `userPrompt` (string), `customPrompt` (string, optional), `verbose` (bool, optional)
- **Returns:** `ExampleAIResponse`

### `example_error_handling`
Demonstrates error handling patterns.
- **Parameters:** `scenario` (string), `forceFailure` (bool, optional)
- **Returns:** `DefaultCommandResponse`

## Response Models

### ExampleServiceResponse
Used for service demonstration responses.

**Properties:**
- `ServiceName` (string): Name of the demonstrated service
- `Operation` (string): Operation that was performed
- `Result` (string): Human-readable result summary
- `Details` (Dictionary<string, string>): Structured details

### ExampleAIResponse
Used for AI service demonstration responses.

**Properties:**
- `Prompt` (string): The user's input prompt
- `ResponseText` (string): AI-generated response
- `Model` (string): AI model used
- `TokenUsage` (Dictionary<string, int>): Token consumption details

## Key Patterns Demonstrated

### 1. Dependency Injection
```csharp
public ExampleTool(
    ILogger<ExampleTool> logger,
    IOutputService output,
    IAzureService azureService,
    IDevOpsService devOpsService,
    IGitHubService gitHubService,
    IAzureAgentServiceFactory agentServiceFactory,
    AzureOpenAIClient openAIClient
) : base()
```

### 2. Command Structure with Sub-commands
```csharp
public override Command GetCommand()
{
    var parentCommand = new Command("demo", "Comprehensive demonstration");

    var azureCmd = new Command("azure", "Azure service demo");
    // ... configure command

    parentCommand.Add(azureCmd);
    return parentCommand;
}
```

### 3. Error Handling Pattern
```csharp
try
{
    // Service operation
    var result = await someService.DoWork(input, ct);
    return new ResponseType { Result = result };
}
catch (Exception ex)
{
    logger.LogError(ex, "Error context: {Input}", input);
    SetFailure();
    return new ResponseType
    {
        ResponseError = $"Operation failed: {ex.Message}"
    };
}
```

### 4. Logging Best Practices
```csharp
// Structured logging with parameters
logger.LogInformation("Starting operation with input: {Input}", input);

// Error logging with exception
logger.LogError(ex, "Error in operation with context: {Context}", context);

// Conditional verbose logging
if (verbose) logger.LogInformation("Detailed information: {Details}", details);
```

### 5. Response Formatting
```csharp
public override string ToString()
{
    var output = new List<string>();

    if (!string.IsNullOrEmpty(ServiceName))
        output.Add($"Service: {ServiceName}");

    // ... add other fields

    var formatted = string.Join(Environment.NewLine, output);
    return ToString(formatted); // Base method handles error formatting
}
```

## Testing Patterns

The tool includes comprehensive unit tests demonstrating:

### Mock Setup
```csharp
[SetUp]
public void Setup()
{
    mockLogger = new Mock<ILogger<ExampleTool>>();
    mockAzureService = new Mock<IAzureService>();
    // ... other mocks

    tool = new ExampleTool(/* injected mocks */);
}
```

### Service Method Testing
```csharp
[Test]
public async Task DemonstrateAzureService_ReturnsSuccessResponse()
{
    // Act
    var result = await tool.DemonstrateAzureService("test-tenant", false);

    // Assert
    Assert.That(result.ResponseError, Is.Null);
    Assert.That(result.ServiceName, Is.EqualTo("Azure Authentication"));
    // ... more assertions
}
```

### Error Scenario Testing
```csharp
[Test]
public async Task DemonstrateErrorHandling_ForceFailure_ReturnsErrorResponse()
{
    // Act
    var result = await tool.DemonstrateErrorHandling("argument", true);

    // Assert
    Assert.That(result.ResponseError, Is.Not.Null);
    Assert.That(result.ResponseError, Contains.Substring("ArgumentException"));
}
```

## Usage as Development Reference

When creating new tools, developers can reference the ExampleTool for:

1. **Project Structure**: Proper namespace, file organization, and class hierarchy
2. **Service Integration**: How to inject and use framework services
3. **CLI Design**: Command structure, options, and argument handling
4. **MCP Integration**: Exposing methods to LLM agents with proper attributes
5. **Error Handling**: Comprehensive exception handling and logging
6. **Response Design**: Creating structured response models with proper formatting
7. **Testing Approach**: Unit test patterns and mock service usage
8. **Documentation Standards**: How to document tool functionality and usage

The ExampleTool serves as a living template that demonstrates current best practices and can be updated as the framework evolves.
