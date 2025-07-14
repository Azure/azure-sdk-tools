// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using APIViewWeb.DTOs;
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

namespace APIViewWeb.HostedServices;

public class CopilotPollingBackgroundHostedService : BackgroundService
{
    private readonly IPollingJobQueueManager _pollingJobQueueManager;
    private readonly string _copilotEndpoint;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAPIRevisionsManager _apiRevisionsManager;
    private readonly ICosmosCommentsRepository _commentsRepository;
    private readonly IHubContext<SignalRHub> _signalRHubContext;
    private readonly ILogger<CopilotPollingBackgroundHostedService> _logger;
    private readonly IConfiguration _configuration;

    public CopilotPollingBackgroundHostedService(
        IPollingJobQueueManager pollingJobQueueManager,
        IHttpClientFactory httpClientFactory,
        IAPIRevisionsManager apiRevisionsManager,
        ICosmosCommentsRepository commentsRepository,
        IHubContext<SignalRHub> signalRHub,
        IConfiguration configuration,
        ILogger<CopilotPollingBackgroundHostedService> logger)
    {
        _pollingJobQueueManager = pollingJobQueueManager;
        _copilotEndpoint = configuration["CopilotServiceEndpoint"];
        _httpClientFactory = httpClientFactory;
        _apiRevisionsManager = apiRevisionsManager;
        _commentsRepository = commentsRepository;
        _signalRHubContext = signalRHub;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        List<Task> runningTasks = new();

        while (!stoppingToken.IsCancellationRequested)
        {
            if (_pollingJobQueueManager.TryDequeue(out AIReviewJobInfoModel jobInfo))
            {
                Task task = Task.Run(async () =>
                {
                    try
                    {
                        stoppingToken.ThrowIfCancellationRequested();
                        HttpClient client = _httpClientFactory.CreateClient();
                        string pollUrl = $"{_copilotEndpoint}/api-review/{jobInfo.JobId}";
                        Poller poller = new();
                        AIReviewJobPolledResponseModel result = await poller.PollAsync(
                            async () =>
                            {
                                stoppingToken.ThrowIfCancellationRequested();
                                HttpResponseMessage response = await client.GetAsync(pollUrl, stoppingToken);
                                response.EnsureSuccessStatusCode();
                                string pollResponseString = await response.Content.ReadAsStringAsync(stoppingToken);
                                AIReviewJobPolledResponseModel pollResponse =
                                    JsonSerializer.Deserialize<AIReviewJobPolledResponseModel>(pollResponseString);
                                return pollResponse;
                            },
                            response => response.Status != "InProgress",
                            120);
                        if (result.Status == "Error")
                        {
                            throw new Exception(result.Details);
                        }

                        // Write back result as comments to APIView
                        foreach (AIReviewComment comment in result.Comments)
                        {
                            (string lineText, string lineId) codeLine = jobInfo.CodeLines[comment.LineNo - 1];
                            CommentItemModel commentModel = new()
                            {
                                CreatedOn = DateTime.UtcNow,
                                ReviewId = jobInfo.APIRevision.ReviewId,
                                APIRevisionId = jobInfo.APIRevision.Id,
                                ElementId = codeLine.lineId,
                                ResolutionLocked = false,
                                CreatedBy = ApiViewConstants.AzureSdkBotName
                            };

                            StringBuilder commentText = new();
                            commentText.AppendLine(comment.Comment);
                            commentText.AppendLine();
                            commentText.AppendLine();
                            if (!string.IsNullOrEmpty(comment.Suggestion))
                            {
                                commentText.AppendLine($"Suggestion : `{comment.Suggestion}`");
                                commentText.AppendLine();
                                commentText.AppendLine();
                            }

                            if (comment.RuleIds.Count > 0)
                            {
                                commentText.AppendLine("**Guidelines**");
                                foreach (string ruleId in comment.RuleIds)
                                {
                                    commentText.AppendLine();
                                    commentText.AppendLine($"https://azure.github.io/azure-sdk/{ruleId}");
                                }
                            }

                            commentModel.CommentText = commentText.ToString();

                            await _commentsRepository.UpsertCommentAsync(commentModel);
                            jobInfo.APIRevision.HasAutoGeneratedComments = true;
                        }

                        jobInfo.APIRevision.CopilotReviewInProgress = false;
                        await _apiRevisionsManager.UpdateAPIRevisionAsync(jobInfo.APIRevision);

                        SiteNotificationDto siteNotification = NotificationHelpers.GetSiteNotificationForReview(
                            new SiteNotificationDto
                            {
                                ReviewId = jobInfo.APIRevision.ReviewId,
                                RevisionId = jobInfo.APIRevision.Id,
                                Status = result.Status,
                                Type = SiteNotificationType.CopilotReviewCompleted
                            },
                            jobInfo.JobId, 
                            _configuration["APIVIew-SPA-Host-Url"],
                            result.Comments.Count,
                            result.Status == SiteNotificationStatus.Error ? result.Details : "");

                        await _signalRHubContext.Clients.All.SendAsync("ReceiveNotification", siteNotification);
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
