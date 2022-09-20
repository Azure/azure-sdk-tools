using System.Reflection;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace Azure.Sdk.Tools.PipelineWitness.ApplicationInsights
{
    public class ApplicationVersionTelemetryInitializer : ITelemetryInitializer
    {
        private static string _version = GetVersion();

        public void Initialize(ITelemetry telemetry)
        {
            if (!string.IsNullOrEmpty(_version))
            {
                var component = telemetry.Context?.Component;

                if (component != null)
                {
                    component.Version = _version;
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
