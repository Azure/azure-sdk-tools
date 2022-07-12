namespace Azure.Sdk.Tools.PipelineWitness.ApplicationInsights
{
    using System.Reflection;
    using Microsoft.ApplicationInsights.Channel;
    using Microsoft.ApplicationInsights.Extensibility;

    public class ApplicationVersionTelemetryInitializer : ITelemetryInitializer
    {
        private static readonly string Version = GetVersion();

        public void Initialize(ITelemetry telemetry)
        {
            if (!string.IsNullOrEmpty(Version))
            {
                var component = telemetry.Context?.Component;

                if (component != null)
                {
                    component.Version = Version;
                }
            }
        }
        
        private static string GetVersion()
        {
            var assembly = typeof(ApplicationVersionTelemetryInitializer).Assembly;
            
            var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? assembly.GetName().Version?.ToString();

            return version;
        }
    }
}
