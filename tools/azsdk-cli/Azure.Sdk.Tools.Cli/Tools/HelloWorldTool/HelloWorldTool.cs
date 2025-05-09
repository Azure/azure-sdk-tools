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
    public class HelloWorldTool : MCPTool
    {
        private readonly ILogger<HelloWorldTool> logger;
        private readonly IResponseService responseService;

        public HelloWorldTool(ILogger<HelloWorldTool> logger, IResponseService responseService)
        {
            this.logger = logger;
            this.responseService = responseService;
        }

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

#pragma warning disable CS1998
        public override async Task<int> HandleCommand(InvocationContext ctx, CancellationToken ct)
#pragma warning restore CS1998
        {
            string input = ctx.ParseResult.GetValueForArgument(_inputArg);
            var result = Echo(input);
            var response = responseService.Respond(new DefaultCommandResponse
            {
                ExitCode = 0,
                Message = result
            });
            logger.LogInformation("{response}", response);

            return 0;
        }

        [McpServerTool(Name = "hello-world"), Description("Echoes the message back to the client.")]
        public static string Echo(string message) => $"RESPONDING TO {message}";
    }
}
