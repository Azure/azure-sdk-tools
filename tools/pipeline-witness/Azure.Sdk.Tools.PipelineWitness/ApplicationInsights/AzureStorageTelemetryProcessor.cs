using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace Azure.Sdk.Tools.PipelineWitness.ApplicationInsights
{
    public class AzureStorageTelemetryProcessor : ITelemetryProcessor
    {
        private readonly ITelemetryProcessor next;

        // next will point to the next TelemetryProcessor in the chain.
        public AzureStorageTelemetryProcessor(ITelemetryProcessor next)
        {
            this.next = next;
        }

        public void Process(ITelemetry telemetry)
        {
            if (telemetry is DependencyTelemetry { Type: "Azure blob" or "Microsoft.Storage" } storageTelemetry)
            {
                // dont process dependency telemetry from Azure Storage
                return;
            }

            this.next.Process(telemetry);
        }
    }
}
