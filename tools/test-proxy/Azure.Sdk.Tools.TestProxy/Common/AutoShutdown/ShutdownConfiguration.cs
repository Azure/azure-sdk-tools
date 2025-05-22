namespace Azure.Sdk.Tools.TestProxy.Common.AutoShutdown
{
    public class ShutdownConfiguration
    {
        public bool EnableAutoShutdown { get; set; } = false;
        public int TimeoutInSeconds { get; set; } = 300;
    }
}
