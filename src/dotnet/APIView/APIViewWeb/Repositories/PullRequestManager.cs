// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using ApiView;
using APIView.DIff;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using APIViewWeb.Respositories;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Octokit;

namespace APIViewWeb.Repositories
{
    public class PullRequestManager
    {
        static readonly string REVIEW_DIFF_URL = "https://apiview.dev/Assemblies/Review/{ReviewId}?diffOnly=True&diffRevisionId={NewRevision}";
        static readonly GitHubClient _githubClient = new GitHubClient(new Octokit.ProductHeaderValue("apiview"));
        readonly TelemetryClient _telemetryClient = new TelemetryClient(TelemetryConfiguration.CreateDefault());

        private readonly ReviewManager _reviewManager;
        private readonly CosmosPullRequestsRepository _pullRequestsRepository;
        private readonly IConfiguration _configuration;
        private readonly CosmosReviewRepository _reviewsRepository;
        private readonly BlobCodeFileRepository _codeFileRepository;
        private readonly DevopsArtifactRepository _devopsArtifactRepository;
        private readonly IAuthorizationService _authorizationService;
        private readonly int _pullRequestCleanupDays;

        public PullRequestManager(
            IAuthorizationService authorizationService,
            ReviewManager reviewManager,
            CosmosReviewRepository reviewsRepository,
            CosmosPullRequestsRepository pullRequestsRepository,
            BlobCodeFileRepository codeFileRepository,
            DevopsArtifactRepository devopsArtifactRepository,
            IConfiguration configuration
            )
        {
            _reviewManager = reviewManager;
            _pullRequestsRepository = pullRequestsRepository;
            _configuration = configuration;
            _reviewsRepository = reviewsRepository;
            _codeFileRepository = codeFileRepository;
            _devopsArtifactRepository = devopsArtifactRepository;
            _authorizationService = authorizationService;
            _githubClient.Credentials = new Credentials(_configuration["github-access-token"]);

            var pullRequestReviewCloseAfter = _configuration["pull-request-review-close-after-days"] ?? "30";
            _pullRequestCleanupDays = int.Parse(pullRequestReviewCloseAfter);
        }

        // API change detection for PR will pull artifact from devops artifact
        public async Task DetectApiChanges(string buildId, string artifactName, string filePath, int prNumber, string commitSha, string repoName, string packageName)
        {
            var requestTelemetry = new RequestTelemetry { Name = "Detecting API changes for PR: " + prNumber };
            var operation = _telemetryClient.StartOperation(requestTelemetry);
            try
            {
                string[] repoInfo = repoName.Split("/");
                var pullRequestModel = await _pullRequestsRepository.GetPullRequestAsync(prNumber, repoName, packageName);
                if (pullRequestModel == null)
                {
                    var issue = await _githubClient.Issue.Get(repoInfo[0], repoInfo[1], prNumber);
                    pullRequestModel = new PullRequestModel()
                    {
                        RepoName = repoName,
                        PullRequestNumber = prNumber,
                        FilePath = filePath,
                        Author = issue.User.Login,
                        PackageName = packageName
                    };
                }
                else
                {
                    if (pullRequestModel.Commits.Any(c=> c== commitSha))
                    {
                        // PR commit is already processed. No need to reprocess it again.
                        return;
                    }
                }
                pullRequestModel.Commits.Add(commitSha);
                await AssertPullRequestCreatorPermission(pullRequestModel);

                using var stream = await _devopsArtifactRepository.DownloadPackageArtifact(repoName, buildId, artifactName, filePath);
                if (stream != null)
                {
                    using var memoryStream = new MemoryStream();
                    var fileName = Path.GetFileName(filePath);
                    var codeFile = await _reviewManager.CreateCodeFile(fileName, stream, false, memoryStream);
                    var apiDiff = await GetApiDiffFromAutomaticReview(codeFile, prNumber, fileName, memoryStream, pullRequestModel);
                    if (apiDiff != "")
                    {
                        await _githubClient.Issue.Comment.Create(repoInfo[0], repoInfo[1], prNumber, apiDiff);
                    }                    
                }
                else
                {
                    _telemetryClient.TrackTrace("Failed to download artifact. Please recheck build id and artifact path values in API change detection request.");
                }
            }
            catch (Exception ex)
            {
                _telemetryClient.TrackException(ex);
            }
            finally
            {
                _telemetryClient.StopOperation(operation);
            }
        }

        public async Task<string> GetApiDiffFromAutomaticReview(CodeFile codeFile, int prNumber, string originalFileName, MemoryStream memoryStream, PullRequestModel pullRequestModel)
        {
            // Get automatically generated master review for package or previously cloned review for this pull request
            var review = await GetBaseLineReview(codeFile.Language, codeFile.PackageName, pullRequestModel);
            if (review == null)
            {
                return "";
            }

            // Check if API surface level matches with any revisions
            var renderedCodeFile = new RenderedCodeFile(codeFile);
            foreach (var revision in review.Revisions.Reverse())
            {
                if (await _reviewManager.IsReviewSame(revision, renderedCodeFile))
                {
                    return "";
                }
            }

            var newRevision = new ReviewRevisionModel()
            {
                Author = review.Author,
                Label = "Created for PR " + prNumber
            };

            var stringBuilder = new StringBuilder();
            var diffUrl = REVIEW_DIFF_URL.Replace("{ReviewId}",review.ReviewId).Replace("{NewRevision}", review.Revisions.Last().RevisionId);
            stringBuilder.Append($"API changes have been detected in this PR. You can review API changes [here]({diffUrl})").Append(Environment.NewLine);
            // If review doesn't match with any revisions then generate formatted diff against last revision of automatic review
            await GetFormattedDiff(renderedCodeFile, review.Revisions.Last(), stringBuilder);

            var reviewCodeFileModel = await _reviewManager.CreateReviewCodeFileModel(newRevision.RevisionId, memoryStream, codeFile);
            reviewCodeFileModel.FileName = originalFileName;
            newRevision.Files.Add(reviewCodeFileModel);
            review.Revisions.Add(newRevision);
            await _reviewsRepository.UpsertReviewAsync(review);
            await _pullRequestsRepository.UpsertPullRequestAsync(pullRequestModel);

            return stringBuilder.ToString();
        }

        private async Task GetFormattedDiff(RenderedCodeFile renderedCodeFile, ReviewRevisionModel lastRevision, StringBuilder stringBuilder)
        {
            RenderedCodeFile autoReview = await _codeFileRepository.GetCodeFileAsync(lastRevision);
            var autoReviewTextFile = autoReview.RenderText(showDocumentation: false, skipDiff: true);
            var prCodeTextFile = renderedCodeFile.RenderText(showDocumentation: false, skipDiff: true);
            var diffLines = InlineDiff.Compute(autoReviewTextFile, prCodeTextFile, autoReviewTextFile, prCodeTextFile);
            if (diffLines == null || diffLines.Length == 0 || diffLines.Count(l=>l.Kind != DiffLineKind.Unchanged) > 10)
            {
                return;
            }

            stringBuilder.Append(Environment.NewLine).Append("**API changes**").Append(Environment.NewLine);
            stringBuilder.Append("```").Append(Environment.NewLine);
            foreach (var line in diffLines)
            {
                if (line.Kind == DiffLineKind.Added)
                {
                    stringBuilder.Append("+ ").Append(line.Line.DisplayString).Append(Environment.NewLine);
                }
                else if (line.Kind == DiffLineKind.Removed)
                {
                    stringBuilder.Append("- ").Append(line.Line.DisplayString).Append(Environment.NewLine);
                }
            }
            stringBuilder.Append("```");
        }

        private async Task<ReviewModel> GetBaseLineReview(string Language, string packageName, PullRequestModel pullRequestModel)
        {
            // Get  previously cloned review for this pull request or automatically generated master review for package
            ReviewModel review;
            if (pullRequestModel.ReviewId != null)
            {
                review = await _reviewsRepository.GetReviewAsync(pullRequestModel.ReviewId);
            }
            else
            {
                var autoReview = await _reviewsRepository.GetMasterReviewForPackageAsync(Language, packageName);
                review = CloneReview(autoReview);
                pullRequestModel.ReviewId = review.ReviewId;
            }
            return review;
        }

        private ReviewModel CloneReview(ReviewModel review)
        {
            var baseRevision = review.Revisions.Last();
            var reviewCopy = new ReviewModel()
            {
                Author = review.Author,
                CreationDate = baseRevision.CreationDate,
                Name = review.Name,
                IsAutomatic = false,
                IsClosed = false,
                RunAnalysis = review.RunAnalysis,
                ReviewId = IdHelper.GenerateId()
            };
            reviewCopy.Revisions.Add(baseRevision);
            return reviewCopy;
        }

        public async Task CleanupPullRequestData()
        {
            var telemetry = new RequestTelemetry { Name = "Cleaning up Reviews created for pull requests" };
            var operation = _telemetryClient.StartOperation(telemetry);
            try
            {               
                var pullRequests = await _pullRequestsRepository.GetPullRequestsAsync(true);
                foreach(var prModel in pullRequests)
                {
                    try
                    {
                        if (await IsPullRequestEligibleForCleanup(prModel))
                        {
                            _telemetryClient.TrackEvent("Closing review created for pull request " + prModel.PullRequestNumber);
                            await ClosePullRequestReview(prModel);
                        }
                    }
                    catch (Exception ex)
                    {
                        _telemetryClient.TrackEvent("Failed to close review " + prModel.ReviewId);
                        _telemetryClient.TrackException(ex);
                    }
                }                
            }
            catch(Exception ex)
            {
                _telemetryClient.TrackException(ex);
            }
            finally
            {
                _telemetryClient.StopOperation(operation);
            }
        }

        private async Task<bool> IsPullRequestEligibleForCleanup(PullRequestModel prModel)
        {
            var repoInfo = prModel.RepoName.Split("/");
            var issue = await _githubClient.Issue.Get(repoInfo[0], repoInfo[1], prModel.PullRequestNumber);
            // Close review created for pull request if pull request was closed more than _pullRequestCleanupDays days ago
            if (issue.ClosedAt != null)
            {
                return issue.ClosedAt?.AddDays(_pullRequestCleanupDays) < DateTimeOffset.Now;
            }
            return false;
        }

        private async Task ClosePullRequestReview(PullRequestModel pullRequestModel)
        {
            if (pullRequestModel.ReviewId != null)
            {
                var review = await _reviewsRepository.GetReviewAsync(pullRequestModel.ReviewId);
                if (review != null)
                {
                    review.IsClosed = true;
                    await _reviewsRepository.UpsertReviewAsync(review);
                }
            }

            pullRequestModel.IsOpen = false;
            await _pullRequestsRepository.UpsertPullRequestAsync(pullRequestModel);
        }

        private async Task AssertPullRequestCreatorPermission(PullRequestModel prModel)
        {
            var orgs = await _githubClient.Organization.GetAllForUser(prModel.Author);
            var orgNames = orgs.Select(o => o.Login);
            var result = await _authorizationService.AuthorizeAsync(
                null,
                orgNames,
                new[] { PullRequestPermissionRequirement.Instance });
            if (!result.Succeeded)
            {
                _telemetryClient.TrackTrace($"API change detection permission failed for user {prModel.Author}.");
                throw new AuthorizationFailedException();
            }
        }
    }
}
