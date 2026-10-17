using APIViewWeb.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace APIViewWeb.Repositories
{
    public interface ICosmosPullRequestsRepository
    {
        public Task<PullRequestModel> GetPullRequestAsync(int pullRequestNumber, string repoName, string packageName, string language = null);
        public Task<IEnumerable<PullRequestModel>> GetPullRequestsAsync(string reviewId, string apiRevisionId = null);
        public Task UpsertPullRequestAsync(PullRequestModel pullRequestModel);
        public Task<IEnumerable<PullRequestModel>> GetPullRequestsAsync(bool isOpen);
        public Task<List<PullRequestModel>> GetPullRequestsAsync(int pullRequestNumber, string repoName);
    }
}
