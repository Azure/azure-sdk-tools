// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Mock.Handlers;

namespace Azure.Sdk.Tools.Mock.Handlers.Example;

/// <summary>
/// Example custom handler for the azsdk_hello_world tool.
/// Demonstrates argument-based switching to return different mock responses
/// depending on the input, making the mock server more flexible for testing.
/// </summary>
public class HelloWorldHandler : IMockToolHandler
{
    public string ToolName => "azsdk_hello_world";

    public CommandResponse Handle(Dictionary<string, object?>? arguments)
    {
        var message = arguments?.GetValueOrDefault("message")?.ToString() ?? "world";

        return message.ToLowerInvariant() switch
        {
            "error" => CreateErrorResponse(),
            "slow" => CreateSlowResponse(),
            _ => MockToolFactory.GetDefaultResponse()
        };
    }

    private static DefaultCommandResponse CreateErrorResponse() => new()
    {
        Message = "Simulated error for testing",
        ResponseError = "MOCK_ERROR"
    };

    private static DefaultCommandResponse CreateSlowResponse() => new()
    {
        Message = "Simulated slow response",
        Duration = 30000
    };
}
