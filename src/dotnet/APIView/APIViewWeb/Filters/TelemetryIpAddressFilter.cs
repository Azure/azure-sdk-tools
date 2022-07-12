using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.DataContracts;

namespace APIViewWeb.Filters
{
    public class TelemetryIpAddressFilter : ITelemetryProcessor
    {
        private ITelemetryProcessor Next { get; set; }

        public TelemetryIpAddressFilter(ITelemetryProcessor next)
        {
            this.Next = next;
        }

        public void Process(ITelemetry item)
        {
            if(item.Context?.Location?.Ip != null)
            {
                item.Context.Location.Ip = null;
            }

            this.Next.Process(item);
        }
    }
}



