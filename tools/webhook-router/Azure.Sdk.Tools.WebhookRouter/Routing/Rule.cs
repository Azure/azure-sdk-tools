using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.WebhookRouter.Routing
{
    public abstract class Rule
    {
        public Rule(Guid route, string eventHubsNamespace, string eventHubName)
        {
            this.Route = route;
            this.EventHubsNamespace = eventHubsNamespace;
            this.EventHubName = eventHubName;
        }

        public Guid Route { get; private set; }
        public string EventHubsNamespace { get; private set; }
        public string EventHubName { get; private set; }
    }
}
