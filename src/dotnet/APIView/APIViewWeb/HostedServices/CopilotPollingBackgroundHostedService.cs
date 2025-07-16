// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
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
using APIViewWeb.Managers;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace APIViewWeb.HostedServices
{
    public class CopilotPollingBackgroundHostedService : BackgroundService
    {
        private readonly IPollingJobQueueManager _pollingJobQueueManager;
        private readonly string _copilotEndpoint;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IAPIRevisionsManager _apiRevisionsManager;
        private readonly ICosmosCommentsRepository _commentsRepository;
        private readonly IHubContext<SignalRHub> _signalRHubContext;
        private readonly ILogger<CopilotPollingBackgroundHostedService> _logger;

        private const string SummarySource = "summary";

        public CopilotPollingBackgroundHostedService(
            IPollingJobQueueManager pollingJobQueueManager, IConfiguration configuration, IHttpClientFactory httpClientFactory,
            IAPIRevisionsManager apiRevisionsManager, ICosmosCommentsRepository commentsRepository, IHubContext<SignalRHub> signalRHub,
            ILogger<CopilotPollingBackgroundHostedService> logger)
        {
            _pollingJobQueueManager = pollingJobQueueManager;
            _copilotEndpoint = configuration["CopilotServiceEndpoint"];
            _httpClientFactory = httpClientFactory;
            _apiRevisionsManager = apiRevisionsManager;
            _commentsRepository = commentsRepository;
            _signalRHubContext = signalRHub;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var runningTasks = new List<Task>();

            while (!stoppingToken.IsCancellationRequested)
            {
                if (_pollingJobQueueManager.TryDequeue(out AIReviewJobInfoModel jobInfo))
                {
                    var task = Task.Run(async () =>
                    {
                        try
                        {
                            stoppingToken.ThrowIfCancellationRequested();
                            var client = _httpClientFactory.CreateClient();
                            var pollUrl = $"{_copilotEndpoint}/api-review/{jobInfo.JobId}";
                            var poller = new Poller();
                            var result = await poller.PollAsync(
                                operation: async () =>
                                {
                                    stoppingToken.ThrowIfCancellationRequested();
                                    var response = await client.GetAsync(pollUrl, stoppingToken);
                                    response.EnsureSuccessStatusCode();
                                    var pollResponseString = await response.Content.ReadAsStringAsync(stoppingToken);
                                    var pollResponse = JsonSerializer.Deserialize<AIReviewJobPolledResponseModel>(pollResponseString);
                                    return pollResponse;
                                },
                                isComplete: response => (response.Status != "InProgress"),
                                initialInterval: 120, // Two minutes
                                maxInterval: 120
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
                            }, stoppingToken);
                        }
                        catch (Exception e)
                        {
                            jobInfo.APIRevision.CopilotReviewInProgress = false;
                            await _apiRevisionsManager.UpdateAPIRevisionAsync(jobInfo.APIRevision);
                            _logger.LogError(e, "Error while polling Copilot job {JobId}", jobInfo.JobId);
                            throw;
                        }

                    }, stoppingToken);
                    runningTasks.Add(task);
                }
                runningTasks.RemoveAll(t => t.IsCompleted);
                await Task.Delay(1000, stoppingToken);
            }

            try
            {
                await Task.WhenAll(runningTasks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "One or more CopilotPollingBackgroundHostedService background jobs failed.");
            }
        }
    }
}
