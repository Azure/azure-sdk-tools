// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Mock.Handlers.Example;

/// <summary>
/// Mock handler for azsdk_hello_world_fail. Always returns an error response so callers
/// can exercise failure-path handling deterministically.
/// </summary>
public class HelloWorldFailHandler : IMockToolHandler
{
    public string ToolName => "azsdk_hello_world_fail";

    public CommandResponse Handle(Dictionary<string, object?>? arguments) => new DefaultCommandResponse
    {
        Message = "Simulated failure from azsdk_hello_world_fail",
        ResponseError = "MOCK_HELLO_WORLD_FAIL"
    };
}

/// <summary>
/// Shared mock for all azsdk_example_* tools. Each tool maps to a fixed service name and operation
/// label so callers can tell which path was exercised without changing the response shape.
/// </summary>
internal static class ExampleResponses
{
    public static CommandResponse Build(string serviceName, string operation, string result) =>
        new ExampleServiceResponse
        {
            ServiceName = serviceName,
            Operation = operation,
            Result = result,
            Details = new Dictionary<string, string>
            {
                ["mock"] = "true",
                ["correlationId"] = "00000000-0000-0000-0000-000000000001"
            }
        };
}

public class ExampleAzureServiceHandler : IMockToolHandler
{
    public string ToolName => "azsdk_example_azure_service";
    public CommandResponse Handle(Dictionary<string, object?>? arguments) =>
        ExampleResponses.Build("AzureStorage", "ListBlobs", "OK");
}

public class ExampleDevOpsServiceHandler : IMockToolHandler
{
    public string ToolName => "azsdk_example_devops_service";
    public CommandResponse Handle(Dictionary<string, object?>? arguments) =>
        ExampleResponses.Build("AzureDevOps", "GetBuild", "OK");
}

public class ExampleGitHubServiceHandler : IMockToolHandler
{
    public string ToolName => "azsdk_example_github_service";
    public CommandResponse Handle(Dictionary<string, object?>? arguments) =>
        ExampleResponses.Build("GitHub", "GetIssue", "OK");
}

public class ExampleAiServiceHandler : IMockToolHandler
{
    public string ToolName => "azsdk_example_ai_service";
    public CommandResponse Handle(Dictionary<string, object?>? arguments) =>
        ExampleResponses.Build("AzureOpenAI", "Completion", "OK");
}

public class ExampleErrorHandlingHandler : IMockToolHandler
{
    public string ToolName => "azsdk_example_error_handling";
    public CommandResponse Handle(Dictionary<string, object?>? arguments)
    {
        var response = ExampleResponses.Build("ErrorHandling", "Simulated", "Failed");
        response.ResponseError = "SIMULATED_ERROR";
        return response;
    }
}

public class ExampleProcessExecutionHandler : IMockToolHandler
{
    public string ToolName => "azsdk_example_process_execution";
    public CommandResponse Handle(Dictionary<string, object?>? arguments) =>
        ExampleResponses.Build("Process", "sleep 1", "exit 0");
}

public class ExamplePowershellExecutionHandler : IMockToolHandler
{
    public string ToolName => "azsdk_example_powershell_execution";
    public CommandResponse Handle(Dictionary<string, object?>? arguments) =>
        ExampleResponses.Build("PowerShell", "Get-Date", "exit 0");
}

public class ExampleAgentFibonacciHandler : IMockToolHandler
{
    public string ToolName => "azsdk_example_agent_fibonacci";
    public CommandResponse Handle(Dictionary<string, object?>? arguments)
    {
        var n = arguments?.GetValueOrDefault("n")?.ToString() ?? "10";
        // 10th Fibonacci number; intentionally fixed so the mock is deterministic regardless of input.
        return ExampleResponses.Build("AgentFibonacci", $"fib({n})", "55");
    }
}
