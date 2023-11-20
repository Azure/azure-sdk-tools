using System.Collections.Generic;
using System.Threading.Tasks;
using APIViewWeb.Models;

namespace APIViewWeb.Managers
{
    public interface IPullRequestManager
    {
        public Task<IEnumerable<PullRequestModel>> GetPullRequestsModelAsync(string reviewId);
        public Task<IEnumerable<PullRequestModel>> GetPullRequestsModelAsync(int pullRequestNumber, string repoName);
        public Task<PullRequestModel> GetPullRequestModelAsync(int prNumber, string repoName, string packageName, string originalFile, string language);
    }
}
