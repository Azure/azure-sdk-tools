using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.DataContracts;

namespace APIViewWeb.Filters
{
    public class TelemetryIpAddressFilter : ITelemetryProcessor
    {
        private readonly ITelemetryProcessor _next;


        public TelemetryIpAddressFilter(ITelemetryProcessor next)
        {
            _next = next;
        }

        public void Process(ITelemetry item)
        {
            if (item.Context?.Location?.Ip != null)
            {
                item.Context.Location.Ip = null;
            }
            _next.Process(item);
        }
    }
}



