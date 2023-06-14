using System.CommandLine;
using System.CommandLine.Invocation;
using Azure.Sdk.Tools.SecretRotation.Configuration;
using Azure.Sdk.Tools.SecretRotation.Core;

namespace Azure.Sdk.Tools.SecretManagement.Cli.Commands;

public class RotateCommand : RotationCommandBase
{
    private readonly Option<bool> forceOption = new(new[] { "--force", "-f" }, "Force rotation of secrets outside of their expiration window.");
    private readonly Option<bool> whatIfOption = new(new[] { "--dry-run", "-d" }, "Preview the changes that will be made without submitting them.");

    public RotateCommand() : base("rotate", "Rotate one, expiring or all secrets")
    {
        AddOption(this.forceOption);
        AddOption(this.whatIfOption);
    }

    protected override async Task HandleCommandAsync(ILogger logger, RotationConfiguration rotationConfiguration,
        InvocationContext invocationContext)
    {
        bool force = invocationContext.ParseResult.GetValueForOption(this.forceOption);
        bool whatIf = invocationContext.ParseResult.GetValueForOption(this.whatIfOption);

        var timeProvider = new TimeProvider();

        IEnumerable<RotationPlan> plans = rotationConfiguration.GetAllRotationPlans(logger, timeProvider);

        foreach (RotationPlan plan in plans)
        {
            await plan.ExecuteAsync(onlyRotateExpiring: !force, whatIf);
        }
    }
}
