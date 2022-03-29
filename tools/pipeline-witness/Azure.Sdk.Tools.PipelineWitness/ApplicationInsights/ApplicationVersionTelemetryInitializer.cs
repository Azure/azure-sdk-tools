using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace Azure.Sdk.Tools.PipelineWitness.ApplicationInsights
{
    public class ApplicationVersionTelemetryInitializer : ITelemetryInitializer
    {
        private static string _version = typeof(ApplicationVersionTelemetryInitializer).Assembly.GetName().Version.ToString();

        public void Initialize(ITelemetry telemetry)
        {
            if (telemetry is ISupportProperties propertyTelemetry)
            {
                propertyTelemetry.Properties["Application version"] = _version;
            }
        }
    }
}
