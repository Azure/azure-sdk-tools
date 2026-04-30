using GitHubTeamUserStore.Constants;
using Octokit;

namespace GitHubTeamUserStore
{
    public class GitHubEventClient
    {
        private const int MaxPageSize = 100;
        // The repository label API auto-paginates, but defaults to 30 items per page. Use 100 to reduce calls.
        private static readonly ApiOptions _apiOptions = new ApiOptions() { PageSize = MaxPageSize };
        private readonly GitHubClient _gitHubClient;

        public GitHubEventClient(string productHeaderName)
        {
            if (string.IsNullOrEmpty(productHeaderName))
            {
                throw new ArgumentException("productHeaderName cannot be null or empty");
            }

            var githubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
            if (string.IsNullOrEmpty(githubToken))
            {
                throw new ApplicationException("GITHUB_TOKEN cannot be null or empty");
            }

            _gitHubClient = new GitHubClient(new Octokit.ProductHeaderValue(productHeaderName))
            {
                Credentials = new Credentials(githubToken)
            };
        }

        /// <summary>
        /// Write the current rate limit and remaining number of transactions.
        /// </summary>
        /// <param name="prependMessage">Optional message to prepend to the rate limit message.</param>
        public async Task WriteRateLimits(string prependMessage = null)
        {
            var miscRateLimit = await _gitHubClient.RateLimit.GetRateLimits();
            // Get the Minutes till reset.
            TimeSpan span = miscRateLimit.Resources.Core.Reset.UtcDateTime.Subtract(DateTime.UtcNow);
            // In the message, cast TotalMinutes to an int to get a whole number of minutes.
            string rateLimitMessage = $"Limit={miscRateLimit.Resources.Core.Limit}, Remaining={miscRateLimit.Resources.Core.Remaining}, Limit Reset in {(int)span.TotalMinutes} minutes.";
            if (prependMessage != null)
            {
                rateLimitMessage = $"{prependMessage} {rateLimitMessage}";
            }
            Console.WriteLine(rateLimitMessage);
        }

        public async Task<HashSet<string>> GetRepositoryLabels(string repository)
        {
            var labels = await _gitHubClient.Issue.Labels.GetAllForRepository(ProductAndTeamConstants.Azure, repository, _apiOptions);
            Console.WriteLine($"number of labels in {repository}={labels.Count}");
            // The label list is a IReadOnlyList<Octokit.Label> which is more than what's needed for verification.
            // Convert the label list into a HashSet<string> using just the label's name. The reason for this is
            // lookup time, O(n) for the HashSet vs O(n^2) for the list.
            HashSet<string> labelNameHash = new HashSet<string>(labels.Select(x => x.Name).ToList());
            return labelNameHash;
        }
    }
}
