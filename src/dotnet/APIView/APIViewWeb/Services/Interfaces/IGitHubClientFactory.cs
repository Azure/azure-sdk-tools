using System.Threading.Tasks;
using Octokit;

namespace APIViewWeb.Services;

public interface IGitHubClientFactory
{
    public Task<GitHubClient> CreateGitHubClientAsync(string owner, string repository);
}
