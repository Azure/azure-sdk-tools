using System;
using System.Collections.Generic;
using System.Text;

namespace Azure.Sdk.Tools.WebhookRouter.Routing
{
    public class AzureDevOpsRule : Rule
    {
        public AzureDevOpsRule(Guid route, string eventHubsNamespace, string eventHubName) : base(route, eventHubsNamespace, eventHubName)
        {
        }
    }
}
