using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace Azure.Sdk.Tools.PipelineWitness.ApplicationInsights
{
    public class BlobNotFoundTelemetryProcessor : ITelemetryProcessor
    {
        private readonly ITelemetryProcessor next;

        // next will point to the next TelemetryProcessor in the chain.
        public BlobNotFoundTelemetryProcessor(ITelemetryProcessor next)
        {
            this.next = next;
        }
  
        public void Process(ITelemetry telemetry)
        {
            if (telemetry is DependencyTelemetry { Success: false, Type: "Azure blob" or "Microsoft.Storage" } blobRequestTelemetry)
            {
                blobRequestTelemetry.Properties.TryGetValue("Error", out var errorProperty);
                
                var isNotFound = blobRequestTelemetry.ResultCode is "404" or "409"
                    || (blobRequestTelemetry.ResultCode == "" && errorProperty?.Contains("Status: 404") == true);

                if (isNotFound)
                {
                    // Set implicit 404 and 409 failures from azure storage to success
                    blobRequestTelemetry.Success = true;
                    blobRequestTelemetry.Properties.Remove("Error");
                }
            }

            this.next.Process(telemetry);
        }
    }
}
