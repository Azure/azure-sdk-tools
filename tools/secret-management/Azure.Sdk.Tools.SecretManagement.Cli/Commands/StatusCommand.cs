using System.CommandLine.Invocation;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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
        var timeProvider = new TimeProvider();
        IEnumerable<RotationPlan> plans = rotationConfiguration.GetAllRotationPlans(logger, timeProvider);

        List<(RotationPlan Plan, RotationPlanStatus Status)> statuses = new();

        foreach (RotationPlan plan in plans)
        {
            logger.LogInformation($"Getting status for plan '{plan.Name}'");
            RotationPlanStatus status = await plan.GetStatusAsync();

            if (logger.IsEnabled(LogLevel.Debug))
            {
                var builder = new StringBuilder();

                builder.AppendLine($"  Plan:");
                builder.AppendLine($"    RotationPeriod: {plan.RotationPeriod}");
                builder.AppendLine($"    RotationThreshold: {plan.RotationThreshold}");
                builder.AppendLine($"    RevokeAfterPeriod: {plan.RevokeAfterPeriod}");

                builder.AppendLine($"  Status:");
                builder.AppendLine($"    ExpirationDate: {status.ExpirationDate}");
                builder.AppendLine($"    Expired: {status.Expired}");
                builder.AppendLine($"    ThresholdExpired: {status.ThresholdExpired}");
                builder.AppendLine($"    RequiresRevocation: {status.RequiresRevocation}");
                builder.AppendLine($"    Exception: {status.Exception?.Message}");

                logger.LogDebug(builder.ToString());
            }


            statuses.Add((plan, status));
        }

        var errored = statuses.Where(x => x.Status.Exception is not null).ToArray();
        var expired = statuses.Except(errored).Where(x => x.Status is { Expired: true }).ToArray();
        var expiring = statuses.Except(errored).Where(x => x.Status is { Expired: false, ThresholdExpired: true }).ToArray();
        var upToDate = statuses.Except(errored).Where(x => x.Status is { Expired: false, ThresholdExpired: false }).ToArray();

        var statusBuilder = new StringBuilder();

        void AppendStatusSection(IList<(RotationPlan Plan, RotationPlanStatus Status)> sectionStatuses, string header)
        {
            if (!sectionStatuses.Any())
            {
                return;
            }

            statusBuilder.AppendLine();
            statusBuilder.AppendLine(header);
            foreach ((RotationPlan plan, RotationPlanStatus status) in sectionStatuses)
            {
                foreach (string line in GetPlanStatusLine(plan, status).Split("\n"))
                {
                    statusBuilder.Append("  ");
                    statusBuilder.AppendLine(line);
                }
            }
        }

        AppendStatusSection(expired, "Expired:");
        AppendStatusSection(expiring, "Expiring:");
        AppendStatusSection(upToDate, "Up-to-date:");
        AppendStatusSection(errored, "Error reading plan status:");

        logger.LogInformation(statusBuilder.ToString());

        if (expired.Any())
        {
            invocationContext.ExitCode = 1;
        }
    }

    private static string GetPlanStatusLine(RotationPlan plan, RotationPlanStatus status)
    {
        if (status.Exception != null)
        {
            return $"{plan.Name}:\n  {status.Exception.Message}";
        }

        DateTimeOffset? expirationDate = status.ExpirationDate;

        DateTimeOffset now = DateTimeOffset.UtcNow;

        string expiration = expirationDate.HasValue
            ? $"{FormatTimeSpan(expirationDate.Value.Subtract(now))}"
            : "No expiration date";

        return $"{plan.Name} - {expiration} / ({FormatTimeSpan(plan.RotationPeriod)} @ {FormatTimeSpan(plan.RotationThreshold)})";
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
