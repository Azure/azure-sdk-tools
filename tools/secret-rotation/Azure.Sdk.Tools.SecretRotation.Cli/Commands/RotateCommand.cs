using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Azure.Sdk.Tools.SecretRotation.Configuration;
using Azure.Sdk.Tools.SecretRotation.Core;

namespace Azure.Sdk.Tools.SecretRotation.Cli.Commands;

public class RotateCommand : RotationCommandBase
{
    private readonly Option<bool> allOption = new(new[] { "--all", "-a" }, "Rotate all secrets");
    private readonly Option<string[]> secretsOption = new(new[] { "--secrets", "-s" }, "Rotate only the specified secrets");
    private readonly Option<bool> expiringOption = new(new[] { "--expiring", "-e" }, "Only rotate expiring secrets");
    private readonly Option<bool> whatIfOption = new(new[] { "--dry-run", "-d" }, "Preview the changes that will be made without submitting them.");

    public RotateCommand() : base("rotate", "Rotate one, expiring or all secrets")
    {
        AddOption(this.expiringOption);
        AddOption(this.whatIfOption);
        AddOption(this.allOption);
        AddOption(this.secretsOption);
        AddValidator(ValidateOptions);
    }

    protected override async Task HandleCommandAsync(ILogger logger, RotationConfiguration rotationConfiguration,
        InvocationContext invocationContext)
    {
        bool onlyRotateExpiring = invocationContext.ParseResult.GetValueForOption(this.expiringOption);
        bool all = invocationContext.ParseResult.GetValueForOption(this.allOption);
        bool whatIf = invocationContext.ParseResult.GetValueForOption(this.whatIfOption);

        var timeProvider = new TimeProvider();

        IEnumerable<RotationPlan> plans;

        if (all)
        {
            plans = rotationConfiguration.GetAllRotationPlans(logger, timeProvider);
        }
        else
        {
            string[] secretNames = invocationContext.ParseResult.GetValueForOption(this.secretsOption)!;

            plans = rotationConfiguration.GetRotationPlans(logger, secretNames, timeProvider);
        }

        foreach (RotationPlan plan in plans)
        {
            await plan.ExecuteAsync(onlyRotateExpiring, whatIf);
        }
    }

    private void ValidateOptions(CommandResult commandResult)
    {
        bool secretsUsed = commandResult.FindResultFor(this.secretsOption) is not null;
        bool allUsed = commandResult.FindResultFor(this.allOption) is not null;

        if (!(secretsUsed || allUsed))
        {
            commandResult.ErrorMessage = "Either the --secrets or the --all option must be provided.";
        }

        if (secretsUsed && allUsed)
        {
            commandResult.ErrorMessage = "The --secrets and --all options cannot both be provided.";
        }
    }
}
