namespace Azure.Sdk.Tools.Cli.Models.Responses
{
    public record SDKBreakingChangeDetectionResponse
    {
        public required bool HasBreakingChanges { get; init; } = false;
        public required string[] BreakingChanges { get; init; } = Array.Empty<string>();
    }
}
