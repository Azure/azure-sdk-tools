// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
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
using Microsoft.Extensions.Configuration;
using Octokit;

namespace APIViewWeb.Repositories
{
    public class PullRequestManager
    {        
        static readonly GitHubClient _githubClient = new GitHubClient(new Octokit.ProductHeaderValue("apiview"));
        readonly TelemetryClient _telemetryClient = new TelemetryClient(TelemetryConfiguration.CreateDefault());

        private readonly ReviewManager _reviewManager;
        private readonly CosmosPullRequestsRepository _pullRequestsRepository;
        private readonly IConfiguration _configuration;
        private readonly CosmosReviewRepository _reviewsRepository;
        private readonly BlobCodeFileRepository _codeFileRepository;
        private readonly DevopsArtifactRepository _devopsArtifactRepository;

        public PullRequestManager(
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
            _githubClient.Credentials = new Credentials(_configuration["github-access-token"]);
        }

        // API change detection for PR will pull artifact from devops artifact
        public async Task DetectApiChanges(string buildId, string artifactName, string filePath, int prNumber, string commitSha, string repoName)
        {
            var requestTelemetry = new RequestTelemetry { Name = "Detecting API changes for PR: " + prNumber };
            var operation = _telemetryClient.StartOperation(requestTelemetry);
            try
            {
                var pullRequestModel = await _pullRequestsRepository.GetPullRequestAsync(prNumber, repoName, filePath);
                if (pullRequestModel == null)
                {
                    pullRequestModel = new PullRequestModel()
                    {
                        RepoName = repoName,
                        PullRequestNumber = prNumber,
                        FilePath = filePath
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

                using var stream = await _devopsArtifactRepository.DownloadPackageArtifact(repoName, buildId, artifactName, filePath);
                if (stream != null)
                {
                    using var memoryStream = new MemoryStream();
                    var codeFile = await _reviewManager.CreateCodeFile(Path.GetFileName(filePath), stream, false, memoryStream);
                    var apiDiff = await GetApiDiffFromAutomaticReview(codeFile);
                    if (apiDiff != "")
                    {
                        var repoInfo = repoName.Split("/");
                        await _githubClient.Issue.Comment.Create(repoInfo[0], repoInfo[1], prNumber, apiDiff);
                    }
                    await _pullRequestsRepository.UpsertPullRequestAsync(pullRequestModel);
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

        public async Task<string> GetApiDiffFromAutomaticReview(CodeFile codeFile)
        {
            // Get automatically generated master review for package
            var review = await _reviewsRepository.GetMasterReviewForPackageAsync(codeFile.Language, codeFile.PackageName);
            if (review == null)
            {
                return "";
            }

            // Check if API surface level matches with any revisions
            var renderedCodeFile = new RenderedCodeFile(codeFile);
            foreach (var approvedRevision in review.Revisions.Reverse())
            {
                if (await _reviewManager.IsReviewSame(approvedRevision, renderedCodeFile))
                {
                    return "";
                }
            }
            // If review doesn't match with any revisions then generate formatted diff against last revision of automatic review
            return await GetFormattedDiff(renderedCodeFile, review.Revisions.Last());
        }

        private async Task<string> GetFormattedDiff(RenderedCodeFile renderedCodeFile, ReviewRevisionModel lastRevision)
        {
            RenderedCodeFile autoReview = await _codeFileRepository.GetCodeFileAsync(lastRevision);
            var autoReviewTextFile = autoReview.RenderText(showDocumentation: false, skipDiff: true);
            var prCodeTextFile = renderedCodeFile.RenderText(showDocumentation: false, skipDiff: true);
            var diffLines = InlineDiff.Compute(autoReviewTextFile, prCodeTextFile, autoReviewTextFile, prCodeTextFile);
            if (diffLines == null || diffLines.Length == 0)
            {
                return "";
            }

            var stringBuilder = new StringBuilder();
            stringBuilder.Append("Following API change(s) have been detected in this PR by APIView system.").Append(Environment.NewLine);
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
                //No need to include unchanged lines for now. We will enhance this in next revision to include context.
            }
            stringBuilder.Append("```");
            return stringBuilder.ToString();
        }
    }
}
