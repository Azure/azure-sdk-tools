// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using APIViewWeb.Managers;
using APIViewWeb.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace APIViewWeb.HostedServices
{
    public class CopilotPollingBackgroundHostedService : BackgroundService
    {
        private readonly IPollingJobQueueManager _pollingJobQueueManager;
        private readonly ICopilotJobProcessor _jobProcessor;
        private readonly ILogger<CopilotPollingBackgroundHostedService> _logger;

        public CopilotPollingBackgroundHostedService(
            IPollingJobQueueManager pollingJobQueueManager, 
            ICopilotJobProcessor jobProcessor,
            ILogger<CopilotPollingBackgroundHostedService> logger)
        {
            _pollingJobQueueManager = pollingJobQueueManager;
            _jobProcessor = jobProcessor;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var runningTasks = new List<Task>();

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (_pollingJobQueueManager.TryDequeue(out AIReviewJobInfoModel jobInfo))
                    {
                        var task = Task.Run(async () =>
                        {
                            try
                            {
                                await _jobProcessor.ProcessJobAsync(jobInfo, stoppingToken);
                            }
                            catch (Exception ex) when (!(ex is OperationCanceledException))
                            {
                                _logger.LogError(ex, "Error processing Copilot job {JobId} for revision {RevisionId}", jobInfo?.JobId, jobInfo?.APIRevision?.Id);
                            }
                        }, stoppingToken);
                        
                        runningTasks.Add(task);
                    }
                    
                    runningTasks.RemoveAll(t => t.IsCompleted);
                    await Task.Delay(1000, stoppingToken);
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    _logger.LogError(ex, "Error in CopilotPollingBackgroundHostedService main loop");
                    // Brief delay to avoid tight loop on persistent errors
                    try
                    {
                        await Task.Delay(5000, stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
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
