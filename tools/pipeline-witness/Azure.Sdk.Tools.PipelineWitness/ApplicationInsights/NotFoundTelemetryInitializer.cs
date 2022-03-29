using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.VisualStudio.Services.ActivityStatistic;

namespace Azure.Sdk.Tools.PipelineWitness.ApplicationInsights
{
    public class NotFoundTelemetryInitializer : ITelemetryInitializer
    {
        public void Initialize(ITelemetry telemetry)
        {
            if (!(telemetry is DependencyTelemetry dependencyTelemetry))
            {
                return;
            }

            if (dependencyTelemetry.Success == false
                && dependencyTelemetry.Type == "Azure blob"
                && (dependencyTelemetry.ResultCode == "404" || dependencyTelemetry.ResultCode == "409"))
            {
                // Set implicit 404 and 409 failures from azure storage to success
                dependencyTelemetry.Success = true;
            }
        }
    }
}
