using System.CommandLine.Invocation;
using System.Text;
using Azure.Sdk.Tools.SecretRotation.Configuration;
using Azure.Sdk.Tools.SecretRotation.Core;

namespace Azure.Sdk.Tools.SecretManagement.Cli.Commands;

public class StatusCommand : RotationCommandBase
{
    public StatusCommand() : base("status", "Show secret rotation status")
    {
    }

    protected override async Task HandleCommandAsync(ILogger logger, RotationConfiguration rotationConfiguration,
        InvocationContext invocationContext)
    {
        var timeProvider = TimeProvider.System;
        RotationPlan[] plans = rotationConfiguration.GetAllRotationPlans(logger, timeProvider).ToArray();

        logger.LogInformation($"Getting status for {plans.Length} plans");

        (RotationPlan Plan, RotationPlanStatus Status)[] statuses = await plans
            .Select(async plan => {
                logger.LogDebug($"Getting status for plan '{plan.Name}'.");
                return (plan, await plan.GetStatusAsync());
            })
            .LimitConcurrencyAsync(10);


        var plansBuyState = statuses.GroupBy(x => x.Status.State)
            .ToDictionary(x => x.Key, x => x.ToArray());


        void LogStatusSection(RotationState state, string header)
        {
            if (!plansBuyState.TryGetValue(state, out var matchingPlans))
            {
                return;
            }

            logger.LogInformation($"\n{header}");

            foreach ((RotationPlan plan, RotationPlanStatus status) in matchingPlans)
            {
                var builder = new StringBuilder();
                var debugBuilder = new StringBuilder();

                builder.Append($"  {plan.Name} - ");
                DateTimeOffset? expirationDate = status.ExpirationDate;
                if (expirationDate.HasValue)
                {
                    builder.AppendLine($"{expirationDate} ({FormatTimeSpan(expirationDate.Value.Subtract(DateTimeOffset.UtcNow))})");
                }
                else
                {
                    builder.AppendLine("no expiration date");
                }

                debugBuilder.AppendLine($"    Plan:");
                debugBuilder.AppendLine($"      Rotation Period: {plan.RotationPeriod}");
                debugBuilder.AppendLine($"      Rotation Threshold: {plan.RotationThreshold}");
                debugBuilder.AppendLine($"      Warning Threshold: {plan.WarningThreshold}");
                debugBuilder.AppendLine($"      Revoke After Period: {plan.RevokeAfterPeriod}");
                debugBuilder.AppendLine($"    Status:");
                debugBuilder.AppendLine($"      Expiration Date: {status.ExpirationDate}");
                debugBuilder.AppendLine($"      State: {status.State}");
                debugBuilder.AppendLine($"      Requires Revocation: {status.RequiresRevocation}");

                if (status.Exception != null)
                {
                    builder.AppendLine($"    Exception:");
                    builder.AppendLine($"      {status.Exception.Message}");
                }

                logger.LogInformation(builder.ToString());
                logger.LogDebug(debugBuilder.ToString());
            }
        }

        LogStatusSection(RotationState.Expired, "Expired:");
        LogStatusSection(RotationState.Warning, "Expiring:");
        LogStatusSection(RotationState.Rotate, "Should Rotate:");
        LogStatusSection(RotationState.UpToDate, "Up-to-date:");
        LogStatusSection(RotationState.Error, "Error reading plan status:");

        if (statuses.Any(x => x.Status.State is RotationState.Expired or RotationState.Warning))
        {
            invocationContext.ExitCode = 1;
        }
    }

    private static string FormatTimeSpan(TimeSpan timeSpan)
    {
        if (timeSpan == TimeSpan.Zero)
        {
            return "0d";
        }

        StringBuilder builder = new StringBuilder();

        if (timeSpan.Days > 0)
        {
            builder.Append(timeSpan.Days);
            builder.Append('d');
        }

        if (timeSpan.Days < 2 && timeSpan.TotalDays - timeSpan.Days > 0)
        {
            if (timeSpan.Hours > 0)
            {
                if (builder.Length > 0)
                {
                    builder.Append(' ');
                }
                builder.Append(timeSpan.Hours);
                builder.Append('h');
            }
            if (timeSpan.Minutes > 0)
            {
                if (builder.Length > 0)
                {
                    builder.Append(' ');
                }
                builder.Append(timeSpan.Minutes);
                builder.Append('m');
            }
        }

        return builder.ToString();
    }
}
