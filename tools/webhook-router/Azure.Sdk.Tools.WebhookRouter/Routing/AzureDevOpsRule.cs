using System;
using System.Collections.Generic;
using System.Text;

namespace Azure.Sdk.Tools.WebhookRouter.Routing
{
    public class AzureDevOpsRule : Rule
    {
        public AzureDevOpsRule(Guid route, string eventHubsNamespace, string eventHubName, string credentialHash, string credentialSalt) : base(route, eventHubsNamespace, eventHubName)
        {
            CredentialHash = credentialHash;
            CrdentialSalt = credentialSalt;
        }

        public string CredentialHash { get; private set; }
        public string CrdentialSalt { get; private set; }
    }
}
