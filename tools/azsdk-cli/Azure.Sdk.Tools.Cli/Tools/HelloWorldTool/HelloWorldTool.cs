using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Models;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.HelloWorldTool
{

    [McpServerToolType, Description("Echoes the message back to the client.")]
    public class HelloWorldTool : MCPTool
    {
        private readonly ILogger<HelloWorldTool> _logger;


        public HelloWorldTool(ILogger<HelloWorldTool> logger)
        {
            _logger = logger;
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
            Command command = new Command("hello-world");
            command.AddArgument(_inputArg);

            command.SetHandler(async ctx =>
            {
                ctx.ExitCode = await HandleCommand(ctx, ctx.GetCancellationToken());
            });

            return command;
        }

        public override async Task<int>HandleCommand(InvocationContext ctx, CancellationToken ct)
        {
            string input = ctx.ParseResult.GetValueForArgument(_inputArg);

            var result = new CommandResponse()
            {
                Status = 0,
                Result = Echo(input)
            };

            _logger.LogInformation("Result {result}", result);

            return 0;
        }

        [McpServerTool(Name="hello-world"), Description("Echoes the message back to the client.")]
        public static string Echo(string message) => $"RESPONDING TO {message}";
    }
}
