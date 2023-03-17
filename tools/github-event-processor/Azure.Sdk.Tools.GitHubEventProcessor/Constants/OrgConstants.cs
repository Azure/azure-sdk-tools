using System;
using System.Collections.Generic;
using System.Text;

namespace Azure.Sdk.Tools.GitHubEventProcessor.Constants
{
    /// <summary>
    /// Organization and product constants.
    /// </summary>
    public class OrgConstants
    {
        // The Azure is used to check whether or not the use is a member of Azure org.
        public const string Azure = "Azure";
        // The ProductHeaderName is used to register the GitHubClient, specificially, for this
        // application
        public const string ProductHeaderName = "azure-sdk-github-event-processor";
    }
}
