// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using CreateMikLabelModel.Models;
using Octokit;
using Polly;

namespace CreateMikLabelModel
{
    internal class TrainingDataClient
    {
        private static int s_randomSeed = Environment.TickCount;
        private static readonly ThreadLocal<Random> RandomNumberGenerator = new ThreadLocal<Random>(() => new Random(Interlocked.Increment(ref s_randomSeed)), false);

        private GitHubClient _client;
        private ILogger _logger;

        public TrainingDataClient(string githubAccessToken, ILogger logger)
        {
            _client = new GitHubClient(new ProductHeaderValue("Microsoft-ML-IssueBot", "1.0.0.0"))
            {
                 Credentials = new Credentials(githubAccessToken)
            };

            _logger = logger;
        }

        public async IAsyncEnumerable<Issue> GetIssuesAsync(
            IEnumerable<string> repositories,
            TrainingDataFilters filters,
            DateTimeOffset? startingDate = null)
        {
            var retryPolicy = CreateRetryPolicy<IReadOnlyList<Issue>>();

            var request = new RepositoryIssueRequest
            {
                Since = startingDate,
                Filter = IssueFilter.All,
                State = ItemStateFilter.All
            };

            if (filters.RequiredIssueLabelNames != null)
            {
                foreach (var requiredLabel in filters.RequiredIssueLabelNames)
                {
                    request.Labels.Add(requiredLabel);
                }
            }

            var options = new ApiOptions
            {
                PageSize = 100
            };

            foreach (var repository in repositories)
            {
                _logger.LogInformation($"Querying issues for '{ repository }'.");

                var repositoryInfo = RepositoryInformation.Parse(repository);
                var issues = await retryPolicy.ExecuteAsync(() => _client.Issue.GetAllForRepository(repositoryInfo.Owner, repositoryInfo.Name, request, options)).ConfigureAwait(false);

                _logger.LogInformation($"{ issues.Count } filtered issues were found for '{ repository }' before filtering was applied.");

                foreach (var issue in issues)
                {
                    if (filters.IssueFilter(issue))
                    {
                        yield return issue;
                    }
                }
            }
        }

        public async IAsyncEnumerable<PullRequestWithFiles> GetPullRequestsAsync(
            IEnumerable<string> repositories,
            TrainingDataFilters filters,
            DateTimeOffset? startingDate = null)
        {
            var pullRequestRetryPolicy = CreateRetryPolicy<IReadOnlyList<PullRequest>>();
            var fileRetryPolicy = CreateRetryPolicy<IReadOnlyList<PullRequestFile>>();

            var request = new PullRequestRequest
            {
                State = ItemStateFilter.All,
                SortProperty = PullRequestSort.Created,
                SortDirection = SortDirection.Descending,
            };

            var options = new ApiOptions
            {
                PageSize = 100
            };

            foreach (var repository in repositories)
            {
                _logger.LogInformation($"Querying pull requests for '{ repository }'.");

                var repositoryInfo = RepositoryInformation.Parse(repository);
                var pullRequests = await pullRequestRetryPolicy.ExecuteAsync(() => _client.PullRequest.GetAllForRepository(repositoryInfo.Owner, repositoryInfo.Name, request, options)).ConfigureAwait(false);

                _logger.LogInformation($"{ pullRequests.Count } pull requests were found for '{ repository }' before filtering was applied.");

                foreach (var pullRequest in pullRequests)
                {
                    // Pull requests can't be filtered by date, so manually scrub any earlier than
                    // the requested starting date.

                    if ((startingDate.HasValue) && (pullRequest.CreatedAt < startingDate.Value))
                    {
                        continue;
                    }

                    // Pull requests can't be filtered by labels, so manually scrub any that do not
                    // have the required labels associated.

                    if ((filters.RequiredPullRequestLabelNames is { Length: > 0 })
                        && (!filters.RequiredPullRequestLabelNames.All(requiredLabel => pullRequest.Labels.Any(label => label.Name == requiredLabel))))
                    {
                        continue;
                    }

                    var files = await fileRetryPolicy.ExecuteAsync(() => _client.PullRequest.Files(repositoryInfo.Owner, repositoryInfo.Name, pullRequest.Number)).ConfigureAwait(false);
                    var pullRequestWithFiles = new PullRequestWithFiles(pullRequest, files.Select(file => file.FileName).ToArray());

                    if (filters.PullRequestFilter(pullRequestWithFiles))
                    {
                        yield return pullRequestWithFiles;
                    }
                }
            }
        }

        private static IAsyncPolicy<T> CreateRetryPolicy<T>(int maxRetryAttempts = 10, int defaultAbuseBackoffSeconds = 30, double exponentialBackoffSeconds = 0.8, double baseJitterSeconds = 2) =>
            Policy<T>
                .Handle<Exception>(ex => ShouldRetry(ex))
                .WaitAndRetryAsync(
                    maxRetryAttempts,
                    attempt => CalculateRetryDelay(attempt, exponentialBackoffSeconds, baseJitterSeconds),
                    async (exception, attempt) =>
                    {
                        var delay = exception switch
                        {
                            RateLimitExceededException rateEx => ((rateEx.Reset - DateTimeOffset.Now).Add(TimeSpan.FromSeconds(5))),
                            AbuseException abuseEx => TimeSpan.FromSeconds(abuseEx.RetryAfterSeconds.GetValueOrDefault(defaultAbuseBackoffSeconds)),
                            _ => default(TimeSpan?)
                        };

                        if (delay.HasValue)
                        {
                            await Task.Delay(delay.Value).ConfigureAwait(false);
                        }
                    });

        private static TimeSpan CalculateRetryDelay(int attempt, double exponentialBackoffSeconds, double baseJitterSeconds) =>
            TimeSpan.FromSeconds((Math.Pow(2, attempt) * exponentialBackoffSeconds) + (RandomNumberGenerator.Value.NextDouble() * baseJitterSeconds));

        private static bool ShouldRetry(Exception ex) => ((IsRetriableException(ex)) || (IsRetriableException(ex?.InnerException)));

        private static bool IsRetriableException(Exception ex)
        {
            if (ex == null)
            {
                return false;
            }

            switch (ex)
            {
                case AbuseException _:
                case RateLimitExceededException _:
                case TimeoutException _:
                case TaskCanceledException _:
                case OperationCanceledException _:
                case WebException _:
                case SocketException _:
                case IOException _:
                    return true;

                case HttpRequestException requestEx:
                    return IsRetriableStatus(requestEx.StatusCode);

                case ApiException apiEx:
                    return IsRetriableStatus(apiEx.StatusCode);

                default:
                    return false;
            };
        }

        private static bool IsRetriableStatus(HttpStatusCode? statusCode) =>
            ((statusCode == null)
                || (statusCode == HttpStatusCode.Unauthorized)
                || (statusCode == ((HttpStatusCode)408))
                || (statusCode == HttpStatusCode.Conflict)
                || (statusCode == ((HttpStatusCode)429))
                || (statusCode == HttpStatusCode.InternalServerError)
                || (statusCode == HttpStatusCode.ServiceUnavailable)
                || (statusCode == HttpStatusCode.GatewayTimeout));
    }
}
