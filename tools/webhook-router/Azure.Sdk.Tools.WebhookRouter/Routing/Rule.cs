using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.WebhookRouter.Routing
{
    public class Rule
    {
        public Rule(Guid route, Dictionary<string, string> settings)
        {
            this.route = route;
            this.settings = settings;
        }

        private Guid route;
        private Dictionary<string, string> settings;

        public Guid Route => route;
        public string EventHubsNamespace => settings["eventhubs-namespace"];
        public string EventHubName => settings["eventhub-name"];
        public PayloadType PayloadType => (PayloadType)Enum.Parse(typeof(PayloadType), settings["payload-type"]);

        public async Task<Payload> ParseRequestAsync(HttpRequest request)
        {
            //Using a dummy payload for local testing.
            //var json = await JsonDocument.ParseAsync(request.Body);
            var json = JsonDocument.Parse("{}");
            var payload = new Payload(json);
            return payload;
        }
    }
}
