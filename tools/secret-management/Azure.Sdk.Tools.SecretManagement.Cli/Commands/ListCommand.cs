using System.CommandLine.Invocation;
using System.Text;
using Azure.Sdk.Tools.SecretRotation.Configuration;

namespace Azure.Sdk.Tools.SecretManagement.Cli.Commands;

public class ListCommand : RotationCommandBase
{
    public ListCommand() : base("list", "List secret rotation plans")
    {
    }

    protected override Task HandleCommandAsync(ILogger logger, RotationConfiguration rotationConfiguration,
        InvocationContext invocationContext)
    {
        foreach (PlanConfiguration plan in rotationConfiguration.PlanConfigurations)
        {
            logger.LogInformation(plan.Name);

            if (logger.IsEnabled(LogLevel.Debug))
            {
                var builder = new StringBuilder();

                builder.AppendLine($"  Tags: {string.Join(", ", plan.Tags)}");
                builder.AppendLine($"  Rotation Period: {plan.RotationPeriod}");
                builder.AppendLine($"  Rotation Threshold: {plan.RotationThreshold}");
                builder.AppendLine($"  Warning Threshold: {plan.WarningThreshold}");
                builder.AppendLine($"  Revoke After Period: {plan.RevokeAfterPeriod}");
                builder.AppendLine($"  Store Types: {string.Join(", ", plan.StoreConfigurations.Select(s => s.Type).Distinct() )}");

                logger.LogDebug(builder.ToString());
            }
        }

        return Task.CompletedTask;
    }
}
