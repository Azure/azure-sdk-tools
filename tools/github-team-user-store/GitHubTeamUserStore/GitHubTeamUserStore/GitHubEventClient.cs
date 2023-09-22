using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Octokit;
using GitHubTeamUserStore.Constants;

namespace GitHubTeamUserStore
{
    public class GitHubEventClient
    {
        private const int MaxPageSize = 100;
        // This needs to be done set because both GetAllMembers and GetAllChildTeams API calls auto-paginate
        // but default to a page size of 30. Default to 100/page to reduce the number of API calls
        private static ApiOptions _apiOptions = new ApiOptions() { PageSize = MaxPageSize };
        public GitHubClient _gitHubClient = null;
        public int CoreRateLimit { get; set; } = 0;
        private int _numRetries = 5;
        private int _delayTimeInMs = 1000;

        public GitHubEventClient(string productHeaderName)
        {
            _gitHubClient = CreateClientWithGitHubEnvToken(productHeaderName);
        }

        /// <summary>
        /// This method creates a GitHubClient using the GITHUB_TOKEN from the environment for authentication
        /// </summary>
        /// <param name="productHeaderName">This is used to generate the User Agent string sent with each request. The name used should represent the product, the GitHub Organization, or the GitHub username that's using Octokit.net (in that order of preference).</param>
        /// <exception cref="ArgumentException">If the product header name is null or empty</exception>
        /// <exception cref="ApplicationException">If there is no GITHUB_TOKEN in the environment</exception>
        /// <returns>Authenticated GitHubClient</returns>
        public virtual GitHubClient CreateClientWithGitHubEnvToken(string productHeaderName)
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
            var gitHubClient = new GitHubClient(new ProductHeaderValue(productHeaderName))
            {
                Credentials = new Credentials(githubToken)
            };
            return gitHubClient;
        }

        /// <summary>
        /// Using the authenticated GitHubClient, call the RateLimit API to get the rate limits.
        /// </summary>
        /// <returns>Octokit.MiscellaneousRateLimit which contains the rate limit information.</returns>
        public async Task<MiscellaneousRateLimit> GetRateLimits()
        {
            return await _gitHubClient.RateLimit.GetRateLimits();
        }

        /// <summary>
        /// Write the current rate limit and remaining number of transactions.
        /// </summary>
        /// <param name="prependMessage">Optional message to prepend to the rate limit message.</param>
        public async Task WriteRateLimits(string prependMessage = null)
        {
            var miscRateLimit = await GetRateLimits();
            CoreRateLimit = miscRateLimit.Resources.Core.Limit;
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

        // Given a teamId, get the Team from github. Chances are, this is only going to be used to get
        // the first team
        public async Task<Team> GetTeamById(int teamId)
        {
            return await _gitHubClient.Organization.Team.Get(teamId);
        }

        /// <summary>
        /// Given an Octokit.Team, call to get the team members. Note: GitHub's GetTeamMembers API gets all of the Users
        /// for the team which includes all the members of child teams.
        /// </summary>
        /// <param name="team">Octokit.Team, the team whose members to retrieve.</param>
        /// <returns>IReadOnlyList of Octokit.Users</returns>
        /// <exception cref="ApplicationException">Thrown if GetAllMembers fails after all retries have been exhausted.</exception>
        public async Task<IReadOnlyList<User>> GetTeamMembers(Team team)
        {
            // For the cases where exceptions/retries fail and an empty ReadOnlyList needs to be returned
            List<User> emptyUserList = new List<User>();

            int tryNumber = 0;
            while (tryNumber < _numRetries)
            {
                tryNumber++;
                try
                {
                    return await _gitHubClient.Organization.Team.GetAllMembers(team.Id, _apiOptions);
                }
                // This is what gets thrown if we try and get a userList for certain special teams on GitHub. 
                // None of these teams are used directly in anything and neither team is a child team of 
                // azure-sdk-write. If a ForbiddenException is encountered, then report it and return an
                // empty list.
                catch (Octokit.ForbiddenException forbiddenEx)
                {
                    Console.WriteLine($"{team.Name} cannot be retrieved using a GitHub PAT.");
                    Console.WriteLine(forbiddenEx.Message);
                    return emptyUserList.AsReadOnly();
                }
                // The only time we should get down here is if there's an exception caused by a network hiccup.
                // Sleep for a second and try again.
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    Console.WriteLine($"delaying {_delayTimeInMs} and retrying");
                    await Task.Delay(_delayTimeInMs);
                }
            }
            throw new ApplicationException($"Unable to get members for team {team.Name}. See above exception(s)");
        }

        /// <summary>
        /// Given an Octokit.Team, call to get all child teams.
        /// </summary>
        /// <param name="team">Octokit.Team, the team whose child teams to retrieve.</param>
        /// <returns>IReadOnlyList of Octkit.Team</returns>
        /// <exception cref="ApplicationException">Thrown if GetAllChildTeams fails after all retries have been exhausted</exception>
        public async Task<IReadOnlyList<Team>> GetAllChildTeams(Team team)
        {
            int tryNumber = 0;
            while (tryNumber < _numRetries)
            {
                tryNumber++;
                try
                {
                    return await _gitHubClient.Organization.Team.GetAllChildTeams(team.Id, _apiOptions);
                }
                // The only time we should get down here is if there's an exception caused by a network hiccup.
                // Sleep for a second and try again.
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    Console.WriteLine($"delaying {_delayTimeInMs} and retrying");
                    await Task.Delay(_delayTimeInMs);
                }
            }
            throw new ApplicationException($"Unable to get members for team {team.Name}. See above exception(s)");
        }

        /// <summary>
        /// Upload the data to blob storage. Uses the BlobUriBuilder to get the blob information to created the
        /// Blob clients and upload the data.
        /// </summary>
        /// <param name="rawJson">The json string, representing the information that will be uploaded to blob storage.</param>
        /// <param name="blobUriBuilder">BlobUriBuilder which contains the blob storage information.</param>
        /// <returns></returns>
        /// <exception cref="ApplicationException">If there is no AZURE_SDK_TEAM_USER_STORE_SAS in the environment</exception>
        public async Task UploadDataToBlobStorage(string rawJson, BlobUriBuilder blobUriBuilder)
        {
            BlobServiceClient blobServiceClient = new BlobServiceClient(blobUriBuilder.ToUri());
            BlobContainerClient blobContainerClient = blobServiceClient.GetBlobContainerClient(blobUriBuilder.BlobContainerName);
            BlobClient blobClient = blobContainerClient.GetBlobClient(blobUriBuilder.BlobName);
            await blobClient.UploadAsync(BinaryData.FromString(rawJson), overwrite: true);
        }

        /// <summary>
        /// Fetch the blob data from storage.
        /// </summary>
        /// <param name="blobUriBuilder">BlobUriBuilder which contains the blob storage information.</param>
        /// <returns>The raw json string blob.</returns>
        /// <exception cref="ApplicationException">Thrown if the HttpResponseMessage does not contain a success status code.</exception>
        public async Task<string> GetBlobDataFromStorage(BlobUriBuilder blobUriBuilder)
        {
            HttpClient client = new HttpClient();
            string blobUri = $"https://{blobUriBuilder.Host}/{blobUriBuilder.BlobContainerName}/{blobUriBuilder.BlobName}";
            HttpResponseMessage response = await client.GetAsync(blobUri);
            if (response.IsSuccessStatusCode)
            {
                string rawJson = await response.Content.ReadAsStringAsync();
                return rawJson;
            }
            throw new ApplicationException($"Unable to retrieve data from blob storage. Uri: {blobUri}, Status code: {response.StatusCode}, Reason {response.ReasonPhrase}");
        }

        /// <summary>
        /// Check to see if the user is a member of the given Org
        /// </summary>
        /// <param name="orgName">Organization name. Chances are this will only ever be "Azure"</param>
        /// <param name="user">The User.Login for the event object from the action payload</param>
        /// <returns>bool, true if the user is a member of the org, false otherwise</returns>
        public async Task<bool> IsUserPublicMemberOfOrg(string orgName, string user)
        {
            // Chances are the orgname is only going to be "Azure"
            return await _gitHubClient.Organization.Member.CheckMemberPublic(orgName, user);
        }

        public async Task<HashSet<string>> GetRepositoryLabels(string repository)
        {
            try
            {
                var labels = await _gitHubClient.Issue.Labels.GetAllForRepository(ProductAndTeamConstants.Azure, repository, _apiOptions);
                Console.WriteLine($"number of labels in {repository}={labels.Count}");
                // The label list is a IReadOnlyList<Octokit.Label> which is more than what's needed for verification.
                // Convert the label list into a HashSet<string> using just the label's name. The reason for this is
                // lookup time, O(n) for the HashSet vs O(n^2) for the list.
                HashSet<string> labelNameHash = new HashSet<string>(labels.Select(x => x.Name).ToList());
                return labelNameHash;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw;
            }
        }
    }
}
