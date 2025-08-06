using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using APIViewWeb.Helpers;
using APIViewWeb.Hubs;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace APIViewWeb.HostedServices
{
    public class CopilotJobProcessor : ICopilotJobProcessor
    {
        private readonly string _copilotEndpoint;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IAPIRevisionsManager _apiRevisionsManager;
        private readonly ICosmosCommentsRepository _commentsRepository;
        private readonly IHubContext<SignalRHub> _signalRHubContext;
        private readonly ILogger<CopilotJobProcessor> _logger;

        private const string SummarySource = "summary";

        public CopilotJobProcessor(
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            IAPIRevisionsManager apiRevisionsManager,
            ICosmosCommentsRepository commentsRepository,
            IHubContext<SignalRHub> signalRHubContext,
            ILogger<CopilotJobProcessor> logger)
        {
            _copilotEndpoint = configuration["CopilotServiceEndpoint"];
            _httpClientFactory = httpClientFactory;
            _apiRevisionsManager = apiRevisionsManager;
            _commentsRepository = commentsRepository;
            _signalRHubContext = signalRHubContext;
            _logger = logger;
        }

        public async Task ProcessJobAsync(AIReviewJobInfoModel jobInfo, CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var client = _httpClientFactory.CreateClient();
                var pollUrl = $"{_copilotEndpoint}/api-review/{jobInfo.JobId}";
                var poller = new Poller();
                
                var result = await poller.PollAsync(
                    operation: async () =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var response = await client.GetAsync(pollUrl, cancellationToken);
                        response.EnsureSuccessStatusCode();
                        var pollResponseString = await response.Content.ReadAsStringAsync(cancellationToken);
                        var pollResponse = JsonSerializer.Deserialize<AIReviewJobPolledResponseModel>(pollResponseString);
                        return pollResponse;
                    },
                    isComplete: response => (response.Status != "InProgress"),
                    initialInterval: 120, // Two minutes
                    maxInterval: 120,
                    cancellationToken: cancellationToken
                );
                
                if (result.Status == "Error")
                {
                    throw new Exception(result.Details);
                }

                List<AIReviewComment> validComments = result.Comments?
                    .Where(comment =>
                        jobInfo.CodeLines[comment.LineNo - 1].lineId != null || comment.Source == SummarySource)
                    .ToList() ?? new List<AIReviewComment>();

                // Write back result as comments to APIView
                foreach (var comment in validComments)
                {
                    var codeLine = jobInfo.CodeLines[comment.LineNo - 1];
                    var commentModel = new CommentItemModel
                    {
                        CreatedOn = DateTime.UtcNow,
                        ReviewId = jobInfo.APIRevision.ReviewId,
                        APIRevisionId = jobInfo.APIRevision.Id,
                        ElementId = codeLine.lineId ?? (comment.Source == SummarySource ? CodeFileHelpers.FirstRowElementId : null),
                    };

                    var commentText = new StringBuilder();
                    commentText.AppendLine(comment.Comment);
                    commentText.AppendLine();
                    commentText.AppendLine();
                    if (!String.IsNullOrEmpty(comment.Suggestion))
                    {
                        commentText.AppendLine($"Suggestion : `{comment.Suggestion}`");
                        commentText.AppendLine();
                        commentText.AppendLine();
                    }

                    if (comment.RuleIds.Count > 0)
                    {
                        commentText.AppendLine("**Guidelines**");
                        foreach (var ruleId in comment.RuleIds)
                        {
                            commentText.AppendLine();
                            commentText.AppendLine($"https://azure.github.io/azure-sdk/{ruleId}");
                        }
                    }

                    commentModel.ResolutionLocked = false;
                    commentModel.CreatedBy = ApiViewConstants.AzureSdkBotName;
                    commentModel.CommentText = commentText.ToString();

                    await _commentsRepository.UpsertCommentAsync(commentModel);
                    jobInfo.APIRevision.HasAutoGeneratedComments = true;
                }
                
                jobInfo.APIRevision.CopilotReviewInProgress = false;
                await _apiRevisionsManager.UpdateAPIRevisionAsync(jobInfo.APIRevision);
                
                await _signalRHubContext.Clients.All.SendAsync("ReceiveAIReviewUpdates", new AIReviewJobCompletedModel()
                {
                    ReviewId = jobInfo.APIRevision.ReviewId,
                    APIRevisionId = jobInfo.APIRevision.Id,
                    Status = result.Status,
                    Details = result.Details,
                    CreatedBy = jobInfo.CreatedBy,
                    NoOfGeneratedComment = validComments.Count,
                    JobId = jobInfo.JobId
                }, cancellationToken);
            }
            catch (Exception e)
            {
                jobInfo.APIRevision.CopilotReviewInProgress = false;
                await _apiRevisionsManager.UpdateAPIRevisionAsync(jobInfo.APIRevision);
                _logger.LogError(e, "Error while processing Copilot job {JobId}", jobInfo.JobId);
                throw;
            }
        }
    }
}
