using System.Reflection;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace Azure.Sdk.Tools.PipelineWitness.ApplicationInsights
{
    public class ApplicationVersionTelemetryInitializer<T> : ITelemetryInitializer
    {
        private static string _version = GetVersion();

        public void Initialize(ITelemetry telemetry)
        {
            if (telemetry is ISupportProperties propertyTelemetry)
            {
                propertyTelemetry.Properties["Application version"] = _version;
            }
        }
        
        private static string GetVersion()
        {
            var assembly = typeof(T).Assembly;
            
            var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? assembly.GetName().Version.ToString();

            return version;
        }
    }
}
