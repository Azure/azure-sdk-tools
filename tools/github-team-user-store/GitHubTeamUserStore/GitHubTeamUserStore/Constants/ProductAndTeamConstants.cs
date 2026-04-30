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
        public const string OpenSourceApiBaseUrl = "https://repos.opensource.microsoft.com/api";
        public const string OpenSourceApiScope = "api://2efaf292-00a0-426c-ba7d-f5d2b214b8fc/.default";
        public const string OpenSourceApiVersion = "2019-10-01";
        // Need to do this since Octokit doesn't expose the API to get team by name.
        // The team Id won't change even if the team name gets modified.
        public const int AzureSdkWriteTeamId = 3057675;
        public const string AzureSdkWriteTeamName = "azure-sdk-write";
        public const string TeamUserCacheFileName = "azure-sdk-write-teams-blob";
        public const string UserOrgVisibilityCacheFileName = "user-org-visibility-blob";
        public const string RepositoryLabelCacheFileName = "repository-labels-blob";
    }
}
