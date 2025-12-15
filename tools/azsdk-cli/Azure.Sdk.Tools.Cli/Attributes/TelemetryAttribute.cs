namespace Azure.Sdk.Tools.Cli.Attributes
{   
    [AttributeUsage(AttributeTargets.Property)]
    public class TelemetryAttribute(bool enabled = true) : Attribute
    {
        public bool Enabled { get; } = enabled;
    }
}
