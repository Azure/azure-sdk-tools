using System.Collections.Generic;
using System.Threading.Tasks;
using APIViewWeb.Models;

namespace APIViewWeb.Managers
{
    public interface IPullRequestManager
    {
        public Task UpsertPullRequestAsync(PullRequestModel pullRequestModel);
        public Task<IEnumerable<PullRequestModel>> GetPullRequestsModelAsync(string reviewId, string apiRevisionId = null);
        public Task<IEnumerable<PullRequestModel>> GetPullRequestsModelAsync(int pullRequestNumber, string repoName);
        public Task<PullRequestModel> GetPullRequestModelAsync(int prNumber, string repoName, string packageName, string originalFile, string language);
        public Task CreateOrUpdateCommentsOnPR(List<PullRequestModel> pullRequests, string repoOwner, string repoName, int prNumber, string hostName);
        public Task CleanupPullRequestData();
    }
}
