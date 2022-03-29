// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using ApiView;
using APIView.DIff;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
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
        static readonly string REVIEW_DIFF_URL = "https://{hostName}/Assemblies/Review/{ReviewId}?diffOnly=True&diffRevisionId={NewRevision}";
        static readonly string REVIEW_URL = "https://{hostName}/Assemblies/Review/{ReviewId}";
        static readonly string ISSUE_COMMENT_PACKAGE_IDENTIFIER = "**API change check for `<PKG-NAME>`**";
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
        private HashSet<string> _allowedListBotAccounts;

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
            var ghToken = _configuration["github-access-token"];
            if (ghToken != null)
            {
                _githubClient.Credentials = new Credentials(ghToken);

            }

            var pullRequestReviewCloseAfter = _configuration["pull-request-review-close-after-days"] ?? "30";
            _pullRequestCleanupDays = int.Parse(pullRequestReviewCloseAfter);
            _allowedListBotAccounts = new HashSet<string>();
            var botAllowedList = _configuration["allowedList-bot-github-accounts"];
            if (!string.IsNullOrEmpty(botAllowedList))
            {
                _allowedListBotAccounts.UnionWith(botAllowedList.Split(","));
            }
        }


        // API change detection for PR will pull artifact from devops artifact
        public async Task<string> DetectApiChanges(string buildId, 
            string artifactName, 
            string originalFileName, 
            string commitSha, 
            string repoName, 
            string packageName, 
            int prNumber,
            string hostName,
            string codeFileName = null,
            string baselineCodeFileName = null,
            bool commentOnPR = true)
        {
            var requestTelemetry = new RequestTelemetry { Name = "Detecting API changes for PR: " + prNumber };
            var operation = _telemetryClient.StartOperation(requestTelemetry);
            try
            {
                originalFileName = originalFileName ?? codeFileName;
                string[] repoInfo = repoName.Split("/");
                var pullRequestModel = await GetPullRequestModel(prNumber, repoName, packageName, originalFileName);
                if (pullRequestModel.Commits.Any(c=> c== commitSha))
                {
                    // PR commit is already processed. No need to reprocess it again.
                    return "";
                }

                pullRequestModel.Commits.Add(commitSha);
                //Check if PR owner is part of Azure//Microsoft org in GitHub
                await AssertPullRequestCreatorPermission(pullRequestModel);
                
                using var memoryStream = new MemoryStream();
                using var baselineStream = new MemoryStream();
                var codeFile = await _reviewManager.GetCodeFile(repoName, buildId, artifactName, packageName, originalFileName, codeFileName, memoryStream, baselineCodeFileName: baselineCodeFileName, baselineStream: baselineStream);
                CodeFile baseLineCodeFile = null;
                if (baselineStream.Length>0)
                {
                    baselineStream.Position = 0;
                    baseLineCodeFile = await CodeFile.DeserializeAsync(baselineStream);
                }

                if (codeFile != null)
                {
                    var comment = await GetApiDiffFromAutomaticReview(codeFile, prNumber, originalFileName, memoryStream, pullRequestModel, hostName, baseLineCodeFile, baselineStream, baselineCodeFileName);
                    // If caller script handles writing comment on PR then we don't want to create duplicate comment by APIView.
                    if (comment != "")
                    {
                        if (commentOnPR)
                        {
                            var existingComment = await GetExistingCommentForPackage(codeFile.PackageName, repoInfo[0], repoInfo[1], prNumber);
                            if (existingComment != null)
                            {
                                await _githubClient.Issue.Comment.Update(repoInfo[0], repoInfo[1], existingComment.Id, comment);
                            }
                            else
                            {
                                await _githubClient.Issue.Comment.Create(repoInfo[0], repoInfo[1], prNumber, comment);
                            }
                        }

                        if (!string.IsNullOrEmpty(pullRequestModel.ReviewId))
                        {
                            return REVIEW_URL.Replace("{hostName}", hostName).Replace("{ReviewId}", pullRequestModel.ReviewId);
                        }
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
            return "";
        }

        private async Task<PullRequestModel> GetPullRequestModel(int prNumber, string repoName, string packageName, string originalFile)
        {
            var pullRequestModel = await _pullRequestsRepository.GetPullRequestAsync(prNumber, repoName, packageName);
            if (pullRequestModel == null)
            {
                string[] repoInfo = repoName.Split("/");
                var issue = await _githubClient.Issue.Get(repoInfo[0], repoInfo[1], prNumber);
                pullRequestModel = new PullRequestModel()
                {
                    RepoName = repoName,
                    PullRequestNumber = prNumber,
                    FilePath = originalFile,
                    Author = issue.User.Login,
                    PackageName = packageName
                };
            }
            return pullRequestModel;
        }

        private bool ShouldShowDiffAsComment(string language)
        {
            switch(language)
            {
                case "Swagger":
                    return false;
                default:
                    return true;
            }
        }

        private async Task<IssueComment> GetExistingCommentForPackage(string packageName, string repoOwner, string repoName, int pr)
        {
            var comments = await _githubClient.Issue.Comment.GetAllForIssue(repoOwner, repoName, pr);
            if (comments != null)
            {
                // Check for comment created for current package.
                // GitHub issue comment unfortunately doesn't have any key to verify. So we need to check actual body to find the comment.
                var commentBody = ISSUE_COMMENT_PACKAGE_IDENTIFIER.Replace("<PKG-NAME>", packageName);
                return comments.Where(c => c.Body.Contains(commentBody)).LastOrDefault();
            }
            return null;
        }

        private async Task<bool> IsReviewSame(ReviewModel review, RenderedCodeFile renderedCodeFile)
        {
            foreach (var revision in review.Revisions.Reverse())
            {
                if (await _reviewManager.IsReviewSame(revision, renderedCodeFile))
                {
                    return true;
                }
            }
            return false;
        }

        private async Task<ReviewRevisionModel> CreateBaselineRevision(
            CodeFile baselineCodeFile,
            MemoryStream baseLineStream,
            PullRequestModel prModel,
            string fileName)
        {
            var newRevision = new ReviewRevisionModel()
            {
                Author = prModel.Author,
                Label = $"Baseline for PR {prModel.PullRequestNumber}"
            };
            var reviewCodeFileModel = await _reviewManager.CreateReviewCodeFileModel(newRevision.RevisionId, baseLineStream, baselineCodeFile);
            reviewCodeFileModel.FileName = fileName;
            newRevision.Files.Add(reviewCodeFileModel);
            return newRevision;
        }

        private ReviewModel CreateNewReview(PullRequestModel prModel)
        {
            return new ReviewModel()
            {
                Author = prModel.Author,
                CreationDate = DateTime.Now,
                Name = prModel.PackageName,
                IsClosed = false,
                FilterType = ReviewType.PullRequest,
                ReviewId = IdHelper.GenerateId()
            };
        }

        public async Task<string> GetApiDiffFromAutomaticReview(CodeFile codeFile,
            int prNumber,
            string originalFileName,
            MemoryStream memoryStream,
            PullRequestModel pullRequestModel,
            string hostName,
            CodeFile baselineCodeFile,
            MemoryStream baseLineStream,
            string baselineFileName)
        {
            var newRevision = new ReviewRevisionModel()
            {
                Author = pullRequestModel.Author,
                Label = $"Created for PR {prNumber}"
            };
            var stringBuilder = new StringBuilder();
            stringBuilder.Append(ISSUE_COMMENT_PACKAGE_IDENTIFIER.Replace("<PKG-NAME>", codeFile.PackageName));
            stringBuilder.Append(Environment.NewLine).Append(Environment.NewLine);

            // Get automatically generated master review for package or previously cloned review for this pull request
            var review = await GetBaseLineReview(codeFile.Language, codeFile.PackageName, pullRequestModel);
            if (review == null)
            {
                // If base line is not available (possible if package is new or request coming from SDK automation)
                review = CreateNewReview(pullRequestModel);
                // If request passes code file for baseline 
                if (baselineCodeFile != null)
                {
                    var baseline = await CreateBaselineRevision(baselineCodeFile, baseLineStream, pullRequestModel, baselineFileName);
                    review.Revisions.Add(baseline);
                }
                var reviewUrl = REVIEW_URL.Replace("{hostName}", hostName).Replace("{ReviewId}", review.ReviewId);
                stringBuilder.Append($"API review is created for `{codeFile.PackageName}`. You can review APIs [here]({reviewUrl}).").Append(Environment.NewLine);
            }
            else
            {
                // Check if API surface level matches with any revisions
                var renderedCodeFile = new RenderedCodeFile(codeFile);
                if (await IsReviewSame(review, renderedCodeFile))
                {
                    //Do not update the comment if review was already created and it matches with current revision.
                    if (pullRequestModel.ReviewId != null)
                        return "";

                    //Baseline review was not created earlier or this is the first commit of PR
                    stringBuilder.Append($"API changes are not detected in this pull request for `{codeFile.PackageName}`");
                    return stringBuilder.ToString();
                }

                if (pullRequestModel.ReviewId != null)
                {
                    // If baseline review was already created and if APIs in current commit doesn't match any of the revisions in generated review then create new baseline using main branch and compare again.
                    // If APIs are still different, find the diff against latest baseline.
                    review = await GetBaseLineReview(codeFile.Language, codeFile.PackageName, pullRequestModel, true);
                    review.ReviewId = pullRequestModel.ReviewId;
                    if (await IsReviewSame(review, renderedCodeFile))
                    {
                        // We will run into this if some one makes unintended API changes in a PR and then reverts it back.
                        // We must clear previous comment and update it to show no changes found.
                        stringBuilder.Append($"API changes are not detected in this pull request for `{codeFile.PackageName}`");
                        return stringBuilder.ToString();
                    }
                }

                var diffUrl = REVIEW_DIFF_URL.Replace("{hostName}", hostName).Replace("{ReviewId}", review.ReviewId).Replace("{NewRevision}", review.Revisions.Last().RevisionId);
                stringBuilder.Append($"API changes have been detected in `{codeFile.PackageName}`. You can review API changes [here]({diffUrl})").Append(Environment.NewLine);
                // If review doesn't match with any revisions then generate formatted diff against last revision of automatic review
                if (ShouldShowDiffAsComment(codeFile.Language))
                {
                    await GetFormattedDiff(renderedCodeFile, review.Revisions.Last(), stringBuilder);
                }                
            }

            var reviewCodeFileModel = await _reviewManager.CreateReviewCodeFileModel(newRevision.RevisionId, memoryStream, codeFile);
            reviewCodeFileModel.FileName = originalFileName;
            newRevision.Files.Add(reviewCodeFileModel);
            review.Revisions.Add(newRevision);
            pullRequestModel.ReviewId = review.ReviewId;
            review.FilterType = ReviewType.PullRequest;
            await _reviewsRepository.UpsertReviewAsync(review);
            await _pullRequestsRepository.UpsertPullRequestAsync(pullRequestModel);

            return stringBuilder.ToString();
        }

        private async Task GetFormattedDiff(RenderedCodeFile renderedCodeFile, ReviewRevisionModel lastRevision, StringBuilder stringBuilder)
        {
            RenderedCodeFile autoReview = await _codeFileRepository.GetCodeFileAsync(lastRevision, false);
            var autoReviewTextFile = autoReview.RenderText(showDocumentation: false, skipDiff: true);
            var prCodeTextFile = renderedCodeFile.RenderText(showDocumentation: false, skipDiff: true);
            var diffLines = InlineDiff.Compute(autoReviewTextFile, prCodeTextFile, autoReviewTextFile, prCodeTextFile);
            if (diffLines == null || diffLines.Length == 0 || diffLines.Count(l=>l.Kind != DiffLineKind.Unchanged) > 10)
            {
                return;
            }

            stringBuilder.Append(Environment.NewLine).Append("**API changes**").Append(Environment.NewLine);
            stringBuilder.Append("```diff").Append(Environment.NewLine);
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

        private async Task<ReviewModel> GetBaseLineReview(string Language, string packageName, PullRequestModel pullRequestModel, bool forceBaseline = false)
        {
            // Get  previously cloned review for this pull request or automatically generated master review for package
            ReviewModel review = null;
            // Force baseline is passed when we need to refresh revision 0 with API revision from main branch(Automatic review revision)
            // If API review is not created for PR then also fetch review from main branch.
            if (forceBaseline || pullRequestModel.ReviewId == null)
            {
                var autoReview = await _reviewsRepository.GetMasterReviewForPackageAsync(Language, packageName);
                if (autoReview != null)
                {
                    review = CloneReview(autoReview);
                    review.Author = pullRequestModel.Author;
                }
            }

            // If either automatic baseline is not available or if review is already created for PR then return this review to create new revision.
            if (review == null && pullRequestModel.ReviewId != null)
            {
                review = await _reviewsRepository.GetReviewAsync(pullRequestModel.ReviewId);
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
            // White list bot accounts to create API reviews from PR automatically
            if (!_allowedListBotAccounts.Contains(prModel.Author))
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
}
