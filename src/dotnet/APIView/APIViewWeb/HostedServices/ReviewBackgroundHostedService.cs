// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;
using APIViewWeb.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace APIViewWeb.HostedServices
{
    public class ReviewBackgroundHostedService : IHostedService, IDisposable
    {
        private bool _isDisabled = false;
        private ReviewManager _reviewManager;
        private int _autoArchiveInactiveGracePeriodMonths; // This is inactive duration in months

        public ReviewBackgroundHostedService(ReviewManager reviewManager, IConfiguration configuration)
        {
            _reviewManager = reviewManager;
            // We can disable background task using app settings if required
            var taskDisabled = configuration["BackgroundTaskDisabled"];
            if (!String.IsNullOrEmpty(taskDisabled) && taskDisabled == "true")
            {
                _isDisabled = true;
            }

            var gracePeriod = configuration["ArchiveReviewGracePeriodInMonths"];
            if (String.IsNullOrEmpty(gracePeriod) || !int.TryParse(gracePeriod, out _autoArchiveInactiveGracePeriodMonths))
            {
                _autoArchiveInactiveGracePeriodMonths = 4;
            }
        }

        public Task StartAsync(CancellationToken stoppingToken)
        {
            if (!_isDisabled)
            {
                _reviewManager.UpdateReviewBackground();
                return ArchiveInactiveReviews(stoppingToken, _autoArchiveInactiveGracePeriodMonths);
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken stoppingToken)
        {
            return Task.CompletedTask;
        }

        public void Dispose(){}

        private async Task ArchiveInactiveReviews(CancellationToken stoppingToken, int archiveAfter)
        {
            do
            {
                await _reviewManager.AutoArchiveReviews(archiveAfter);
                // Wait 6 hours before running archive task again
                await Task.Delay(6 * 60 * 60000, stoppingToken);
            }
            while (!stoppingToken.IsCancellationRequested);
        }
    }
}
