using System.Reflection;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;

namespace Azure.Sdk.Tools.PipelineWitness.ApplicationInsights
{
    public class ApplicationVersionTelemetryInitializer : ITelemetryInitializer
    {
        private static readonly string version = GetVersion();

        public void Initialize(ITelemetry telemetry)
        {
            if (!string.IsNullOrEmpty(version))
            {
                var component = telemetry.Context?.Component;

                if (component != null)
                {
                    component.Version = version;
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
