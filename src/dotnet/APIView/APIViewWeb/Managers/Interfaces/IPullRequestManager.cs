using System.Collections.Generic;
using System.Threading.Tasks;
using APIViewWeb.Models;

namespace APIViewWeb.Managers
{
    public interface IPullRequestManager
    {
        public Task<IEnumerable<PullRequestModel>> GetPullRequestsModel(string reviewId);
        public Task<IEnumerable<PullRequestModel>> GetPullRequestsModel(int pullRequestNumber, string repoName);
    }
}
