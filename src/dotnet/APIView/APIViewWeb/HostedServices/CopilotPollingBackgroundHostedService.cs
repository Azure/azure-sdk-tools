// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;
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

        public CopilotPollingBackgroundHostedService(
            IPollingJobQueueManager pollingJobQueueManager, IConfiguration configuration, IHttpClientFactory httpClientFactory,
            IAPIRevisionsManager apiRevisionsManager, ICosmosCommentsRepository commentsRepository, IHubContext<SignalRHub> signalRHub)
        {
            _pollingJobQueueManager = pollingJobQueueManager;
            _copilotEndpoint = configuration["CopilotServiceEndpoint"];
            _httpClientFactory = httpClientFactory;
            _apiRevisionsManager = apiRevisionsManager;
            _commentsRepository = commentsRepository;
            _signalRHubContext = signalRHub;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (_pollingJobQueueManager.TryDequeue(out AIReviewJobInfoModel jobInfo))
                {
                    var task = Task.Run(async () =>
                    {
                        try
                        {
                            var client = _httpClientFactory.CreateClient();
                            var poolUrl = $"{_copilotEndpoint}/api-review/{jobInfo.JobId}";
                            var poller = new Poller();
                            var result = await poller.PollAsync(
                                operation: async () =>
                                {
                                    var response = await client.GetAsync(poolUrl);
                                    response.EnsureSuccessStatusCode();
                                    var pollResponseString = await response.Content.ReadAsStringAsync();
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

                            // Write back result as comments to APIView
                            foreach (var comment in result.Comments)
                            {
                                var codeLine = jobInfo.CodeLines[comment.LineNo - 1];
                                var commentModel = new CommentItemModel();
                                commentModel.CreatedOn = DateTime.UtcNow;
                                commentModel.ReviewId = jobInfo.APIRevision.ReviewId;
                                commentModel.APIRevisionId = jobInfo.APIRevision.Id;
                                commentModel.ElementId = codeLine.lineId;

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
                                foreach (var id in comment.RuleIds)
                                {
                                    commentText.AppendLine($"See: https://azure.github.io/azure-sdk/{id}");
                                }
                                commentModel.ResolutionLocked = false;
                                commentModel.CreatedBy = "azure-sdk";
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
                                NoOfGeneratedComment = result.Comments.Count
                            });
                        }
                        catch (Exception e)
                        {
                            jobInfo.APIRevision.CopilotReviewInProgress = false;
                            await _apiRevisionsManager.UpdateAPIRevisionAsync(jobInfo.APIRevision);
                            throw new Exception($"Copilot Failed: {e.Message}");
                        }

                    }, stoppingToken);
                }
                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}
