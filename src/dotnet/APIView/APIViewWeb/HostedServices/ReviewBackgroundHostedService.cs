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

        public ReviewBackgroundHostedService(ReviewManager reviewManager, IConfiguration configuration)
        {
            _reviewManager = reviewManager;
            // We can disable background task using app settings if required
            var taskDisabled = configuration["BackgroundTaskDisabled"];
            if (!String.IsNullOrEmpty(taskDisabled) && taskDisabled == "true")
            {
                _isDisabled = true;
            }
        }

        public Task StartAsync(CancellationToken stoppingToken)
        {
            if (!_isDisabled)
            {
                _reviewManager.UpdateReviewBackground();
            }
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken stoppingToken)
        {
            return Task.CompletedTask;
        }

        public void Dispose(){}
    }
}
