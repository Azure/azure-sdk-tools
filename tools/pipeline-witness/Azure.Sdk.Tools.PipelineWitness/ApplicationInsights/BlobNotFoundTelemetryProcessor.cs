using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace Azure.Sdk.Tools.PipelineWitness.ApplicationInsights
{
    public class BlobNotFoundTelemetryProcessor : ITelemetryProcessor
    {
        private readonly ITelemetryProcessor _next;

        // next will point to the next TelemetryProcessor in the chain.
        public BlobNotFoundTelemetryProcessor(ITelemetryProcessor next)
        {
            _next = next;
        }

        public void Process(ITelemetry item)
        {
            if (item is DependencyTelemetry { Success: false, Type: "Azure blob" or "Microsoft.Storage" } blobRequestTelemetry)
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

            _next.Process(item);
        }
    }
}
