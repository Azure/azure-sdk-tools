using System.CommandLine.Invocation;
using System.Text.Json;
using Azure.Sdk.Tools.SecretRotation.Configuration;
using Azure.Sdk.Tools.SecretRotation.Core;

namespace Azure.Sdk.Tools.SecretRotation.Cli.Commands;

public class StatusCommand : RotationCommandBase
{
    public StatusCommand() : base("status", "Show secret rotation status")
    {
    }

    protected override async Task HandleCommandAsync(ILogger logger, RotationConfiguration rotationConfiguration,
        InvocationContext invocationContext)
    {
        var timeProvider = new TimeProvider();
        IEnumerable<RotationPlan> plans = rotationConfiguration.GetAllRotationPlans(logger, timeProvider);

        foreach (RotationPlan plan in plans)
        {
            Console.WriteLine();
            Console.WriteLine($"{plan.Name}:");

            RotationPlanStatus status = await plan.GetStatusAsync();
            Console.WriteLine(JsonSerializer.Serialize(status, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
