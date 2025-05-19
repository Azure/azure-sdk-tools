// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Contract;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Tools.HelloWorldTool
{
    #if DEBUG
    [McpServerToolType, Description("Echoes the message back to the client.")]
    public class HelloWorldTool(ILogger<HelloWorldTool> logger, IOutputService output) : MCPTool
    {
        private Argument<string> _inputArg = new Argument<string>(
            name: "input",
            description: "The text to echo back"
        )
        {
            Arity = ArgumentArity.ExactlyOne
        };

        public override Command GetCommand()
        {
            Command command = new("hello-world");
            command.AddArgument(_inputArg);
            command.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
            return command;
        }

        public override async Task HandleCommand(InvocationContext ctx, CancellationToken ct)
        {
            string input = ctx.ParseResult.GetValueForArgument(_inputArg);
            var result = EchoSuccess(input);
            ctx.ExitCode = ExitCode;
            output.Output(result);
            await Task.CompletedTask;
        }

        [McpServerTool(Name = "hello-world-success"), Description("Echoes the message back to the client")]
        public DefaultCommandResponse EchoSuccess(string message)
        {
            try
            {
                logger.LogInformation("Echoing message: {message}", message);

                return new()
                {
                    Message = $"RESPONDING TO '{message}' with SUCCESS: {ExitCode}",
                    Duration = 1
                };
            }
            catch(Exception ex)
            {
                logger.LogError("Exception during echo: {ex}", ex);
                SetFailure();
                return new()
                {
                    Message = $"Failed to echo message: {ex.Message}",
                    Duration = 1
                };
            }
        }
    }
    #endif
}
