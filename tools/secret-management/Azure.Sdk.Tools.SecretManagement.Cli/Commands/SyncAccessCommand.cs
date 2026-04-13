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

    private readonly Option<bool> noDeleteOption = new("--no-delete")
    {
        Description = "Skip deleting federated identity credentials that are not in the config.",
    };

    private readonly Option<bool> dryRunOption = new("--dry-run")
    {
        Description = "Only perform read operations; log what would be changed without making modifications.",
    };

    public SyncAccessCommand() : base("sync-access", "RBAC and Federated Identity manager for managed identities")
    {
        AddOption(this.fileOption);
        AddOption(this.noDeleteOption);
        AddOption(this.dryRunOption);
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
        var noDelete = invocationContext.ParseResult.GetValueForOption(this.noDeleteOption);
        var dryRun = invocationContext.ParseResult.GetValueForOption(this.dryRunOption);

        var options = new ReconcileOptions
        {
            NoDelete = noDelete,
            DryRun = dryRun,
        };

        await AccessManager.Run(logger, fileOptions?.ToList() ?? new List<string>(), options);
    }
}