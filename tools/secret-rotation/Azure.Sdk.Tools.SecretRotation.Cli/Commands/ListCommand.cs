using System.CommandLine.Invocation;
using System.Text.Json;
using Azure.Sdk.Tools.SecretRotation.Configuration;
using Azure.Sdk.Tools.SecretRotation.Core;

namespace Azure.Sdk.Tools.SecretRotation.Cli.Commands;

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
            Console.WriteLine($"name: {plan.Name} - tags: {string.Join(", ", plan.Tags)}");
        }

        return Task.CompletedTask;
    }
}
