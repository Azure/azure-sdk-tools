// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;
using APIViewWeb.Managers;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace APIViewWeb.HostedServices
{
    public class PullRequestBackgroundHostedService : IHostedService, IDisposable
    {
        private bool _isDisabled = false;
        private IPullRequestManager _pullRequestManager;
        private readonly TelemetryClient _telemetryClient;

        public PullRequestBackgroundHostedService(IPullRequestManager pullRequestManager,
            IConfiguration configuration, TelemetryClient telemetryClient)
        {
            _pullRequestManager = pullRequestManager;
            _telemetryClient = telemetryClient;

            // We can disable background task using app settings if required
            var taskDisabled = configuration["PullRequestCleanupTaskDisabled"];
            if (!String.IsNullOrEmpty(taskDisabled) && taskDisabled == "true")
            {
                _isDisabled = true;
            }

        }

        public Task StartAsync(CancellationToken stoppingToken)
        {
            if (!_isDisabled)
            {
                var _executingTask = ExecuteAsync(stoppingToken);
                if (_executingTask.IsCompleted)
                {
                    return _executingTask;
                }
            }
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken stoppingToken)
        {
            return Task.CompletedTask;
        }

        private async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            do
            {
                try
                {
                    await _pullRequestManager.CleanupPullRequestData();
                }
                catch (Exception ex)
                {
                    _telemetryClient.TrackException(ex);
                }
                finally
                {
                    //6 hours delay before running background task again
                    await Task.Delay(6 * 60 * 60000, stoppingToken);
                }
            }
            while (!stoppingToken.IsCancellationRequested);
        }

        public void Dispose() { }
    }
}
