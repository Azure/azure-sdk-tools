using System.CommandLine;
using System.CommandLine.Invocation;
using Azure.Sdk.Tools.SecretRotation.Configuration;
using Azure.Sdk.Tools.SecretRotation.Core;

namespace Azure.Sdk.Tools.SecretManagement.Cli.Commands;

public class RotateCommand : RotationCommandBase
{
    private readonly Option<bool> expiringOption = new(new[] { "--expiring", "-e" }, "Only rotate expiring secrets");
    private readonly Option<bool> whatIfOption = new(new[] { "--dry-run", "-d" }, "Preview the changes that will be made without submitting them.");

    public RotateCommand() : base("rotate", "Rotate one, expiring or all secrets")
    {
        AddOption(this.expiringOption);
        AddOption(this.whatIfOption);
    }

    protected override async Task HandleCommandAsync(ILogger logger, RotationConfiguration rotationConfiguration,
        InvocationContext invocationContext)
    {
        bool onlyRotateExpiring = invocationContext.ParseResult.GetValueForOption(this.expiringOption);
        bool whatIf = invocationContext.ParseResult.GetValueForOption(this.whatIfOption);

        var timeProvider = new TimeProvider();

        IEnumerable<RotationPlan> plans = rotationConfiguration.GetAllRotationPlans(logger, timeProvider);

        foreach (RotationPlan plan in plans)
        {
            await plan.ExecuteAsync(onlyRotateExpiring, whatIf);
        }
    }
}
