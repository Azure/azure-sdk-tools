using System.Collections.Generic;
using System.Threading.Tasks;
using APIViewWeb.Helpers;
using APIViewWeb.Models;

namespace APIViewWeb.Managers
{
    public interface IPullRequestManager
    {
        public Task UpsertPullRequestAsync(PullRequestModel pullRequestModel);
        public Task<IEnumerable<PullRequestModel>> GetPullRequestsModelAsync(string reviewId, string apiRevisionId = null);
        public Task<IEnumerable<PullRequestModel>> GetPullRequestsModelAsync(int pullRequestNumber, string repoName);
        public Task<PullRequestModel> GetPullRequestModelAsync(int prNumber, string repoName, string packageName, string originalFile, string language);
        public Task CleanupPullRequestData();
        public Task<string> CreateAPIRevisionIfAPIHasChanges(
            string buildId, string artifactName, string originalFileName, string commitSha, string repoName,
            string packageName, int prNumber, string hostName, CreateAPIRevisionAPIResponse responseContent,
            string codeFileName = null, string baselineCodeFileName = null, string language = null, string project = "internal", string packageType = null);
    }
}
