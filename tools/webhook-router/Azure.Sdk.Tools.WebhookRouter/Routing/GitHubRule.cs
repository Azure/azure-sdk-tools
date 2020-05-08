using System;
using System.Collections.Generic;
using System.Text;

namespace Azure.Sdk.Tools.WebhookRouter.Routing
{
    public class GitHubRule : Rule
    {
        public GitHubRule(Guid route, string eventHubsNamespace, string eventHubName, string webhookSecret) : base(route, eventHubsNamespace, eventHubName)
        {
            this.WebhookSecret = webhookSecret;
        }

        public string WebhookSecret { get; private set; }
    }
}
