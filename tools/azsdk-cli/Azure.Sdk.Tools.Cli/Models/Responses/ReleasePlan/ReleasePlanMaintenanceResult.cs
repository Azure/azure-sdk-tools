using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlanList;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReleasePlanMaintenanceOperation
{
    Preview,
    Cleanup,
    Reminder,
    Confirmation
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReleasePlanMaintenanceOutcome
{
    Eligible,
    Abandoned,
    Delivered,
    Skipped,
    Failed
}

public class ReleasePlanMaintenanceResult
{
    [JsonPropertyName("release_plan_id")]
    public int ReleasePlanId { get; init; }

    [JsonPropertyName("work_item_id")]
    public int WorkItemId { get; init; }

    [JsonPropertyName("release_plan_link")]
    public string ReleasePlanLink { get; init; } = string.Empty;

    [JsonPropertyName("operation")]
    public ReleasePlanMaintenanceOperation Operation { get; init; }

    [JsonPropertyName("outcome")]
    public ReleasePlanMaintenanceOutcome Outcome { get; init; }

    [JsonPropertyName("reason")]
    public string Reason { get; init; } = string.Empty;
}