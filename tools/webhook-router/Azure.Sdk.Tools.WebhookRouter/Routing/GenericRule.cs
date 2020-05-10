using System;
using System.Collections.Generic;
using System.Text;

namespace Azure.Sdk.Tools.WebhookRouter.Routing
{
    public class GenericRule : Rule
    {
        public GenericRule(Guid route, string eventHubsNamespace, string eventHubName) : base(route, eventHubsNamespace, eventHubName)
        {
        }
    }
}
