using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GitHubTeamUserStore.Constants
{
    internal class ProductAndTeamConstants
    {
        // The ProductHeaderName is used to register the GitHubClient for this application
        public const string ProductHeaderName = "azure-sdk-github-team-user-store";

        public const string Azure = "Azure";
        // Need to do this since Octokit doesn't expose the API to get team by name.
        // The team Id won't change even if the team name gets modified.
        public const int AzureSdkWriteTeamId = 3057675;
        public const string AzureSdkWriteTeamName = "azure-sdk-write";
    }
}
