// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Commands;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Tools.Core;

namespace Azure.Sdk.Tools.Cli.Tools.Example
{
#if DEBUG
    [McpServerToolType, Description("Simple echo tool for testing and demonstration purposes")]
    public class HelloWorldTool(ILogger<HelloWorldTool> logger) : MCPTool()
    {
        // MCP Tool Names
        private const string HelloWorldFailToolName = "azsdk_hello_world_fail";
        private const string HelloWorldToolName = "azsdk_hello_world";

        public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.Example];

        private Argument<string> _inputArg = new Argument<string>("input")
        {
            Description = "The text to echo back",
            Arity = ArgumentArity.ExactlyOne
        };

        private readonly Option<bool> failOpt = new("--fail")
        {
            Description = "Force failure",
            Required = false,
            DefaultValueFactory = _ => false,
        };

        protected override Command GetCommand() => new("hello-world", "Simple echo tool for testing framework features") { _inputArg, failOpt };

        public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
        {
            string input = parseResult.GetValue(_inputArg) ?? "";
            var fail = parseResult.GetValue(failOpt);
            var result = fail ? EchoFail(input) : EchoSuccess(input);
            return await Task.FromResult<CommandResponse>(result);
        }

        [McpServerTool(Name = HelloWorldFailToolName), Description("Returns an error response for testing failure scenarios")]
        public DefaultCommandResponse EchoFail(string message)
        {
            try
            {
                logger.LogError("Echoing message: {message}", message);

                return new()
                {
                    ExitCode = 1,
                    ResponseError = $"RESPONDING TO '{message}' with FAIL: 1",
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while echoing message: {message}", message);
                return new()
                {
                    ResponseError = $"Error occurred while processing '{message}': {ex.Message}"
                };
            }
        }

        [McpServerTool(Name = HelloWorldToolName), Description("Returns your message with success status")]
        public DefaultCommandResponse EchoSuccess(string message)
        {
            try
            {
                logger.LogInformation("Echoing message: {message}", message);

                return new()
                {
                    Message = $"RESPONDING TO '{message}' with SUCCESS: 0",
                    Duration = 1
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while echoing message: {message}", message);
                return new()
                {
                    ResponseError = $"Error occurred while processing '{message}': {ex.Message}"
                };
            }
        }
    }
#endif
}
