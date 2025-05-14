// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Models;
using ModelContextProtocol.Server;
using System.Text.Json;

namespace Azure.Sdk.Tools.Cli.Tools.HelloWorldTool
{

    [McpServerToolType, Description("Echoes the message back to the client.")]
    public class HelloWorldTool(ILogger<HelloWorldTool> logger, IOutputService output) : MCPTool
    {
        private readonly ILogger<HelloWorldTool> logger = logger;
        private readonly IOutputService output = output;
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

            command.SetHandler(async ctx =>
            {
                ctx.ExitCode = await HandleCommand(ctx, ctx.GetCancellationToken());
            });

            return command;
        }

        public override async Task<int> HandleCommand(InvocationContext ctx, CancellationToken ct)
        {
            string input = ctx.ParseResult.GetValueForArgument(_inputArg);
            var result = Echo(input);

            output.Output(new DefaultCommandResponse
            {
                ExitCode = 0,
                Message = result
            });

            return await Task.FromResult(0);
        }

        [McpServerTool(Name = "hello-world"), Description("Echoes the message back to the client.")]
        public static string Echo(string message) => $"RESPONDING TO {message}";
    }
}
