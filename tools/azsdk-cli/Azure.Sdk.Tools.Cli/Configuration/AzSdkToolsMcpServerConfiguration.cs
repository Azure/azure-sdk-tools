namespace Azure.Sdk.Tools.Cli.Configuration
{
    public class AzSdkToolsMcpServerConfiguration
    {
        public const string DefaultName = Constants.TOOLS_ACTIVITY_SOURCE;

        public string Name { get; set; } = DefaultName;

        public string Version { get; set; } = "1.0.0-dev";

        public bool IsTelemetryEnabled { get; set; } = true;
    }
}
