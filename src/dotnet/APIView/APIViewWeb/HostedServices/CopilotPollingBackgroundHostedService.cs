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
                if (_pollingJobQueueManager.TryDequeue(out AIReviewJobInfoModel jobInfo))
                {
                    var task = Task.Run(async () =>
                    {
                        await _jobProcessor.ProcessJobAsync(jobInfo, stoppingToken);
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
