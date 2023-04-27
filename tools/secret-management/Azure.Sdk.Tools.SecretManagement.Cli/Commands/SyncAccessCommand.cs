using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.Extensions.Logging.Console;
using Azure.Sdk.Tools.AccessManagement;

namespace Azure.Sdk.Tools.SecretManagement.Cli.Commands;

public class SyncAccessCommand : Command
{
    private readonly Option<string[]> fileOption = new(new[] { "--file", "-f" })
    {
        Arity = ArgumentArity.OneOrMore,
        Description = "Name of the plan to sync.",
        IsRequired = true,
    };

    public SyncAccessCommand() : base("sync-access", "RBAC and Federated Identity manager for AAD applications")
    {
        AddOption(this.fileOption);
        this.SetHandler(Run);
    }

    public async Task Run(InvocationContext invocationContext)
    {
        var loggerFactory = LoggerFactory.Create(builder => builder
            .AddConsoleFormatter<SimplerConsoleFormatter, ConsoleFormatterOptions>()
            .AddConsole(options => options.FormatterName = SimplerConsoleFormatter.FormatterName)
            .SetMinimumLevel(LogLevel.Information));

        var logger = loggerFactory.CreateLogger(string.Empty);

        var fileOptions = invocationContext.ParseResult.GetValueForOption(this.fileOption);
        await AccessManager.Run(logger, fileOptions?.ToList() ?? new List<string>());
    }
}