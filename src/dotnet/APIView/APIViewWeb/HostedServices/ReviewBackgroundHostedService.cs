﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;
using APIViewWeb.Managers;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace APIViewWeb.HostedServices
{
    public class ReviewBackgroundHostedService : BackgroundService
    {
        private readonly bool _isDisabled;
        private readonly ReviewManager _reviewManager;
        private readonly int _autoArchiveInactiveGracePeriodMonths; // This is inactive duration in months

        static TelemetryClient _telemetryClient = new(TelemetryConfiguration.CreateDefault());

        public ReviewBackgroundHostedService(ReviewManager reviewManager, IConfiguration configuration)
        {
            _reviewManager = reviewManager;
            // We can disable background task using app settings if required
            if (bool.TryParse(configuration["BackgroundTaskDisabled"], out bool taskDisabled))
            {
                _isDisabled = taskDisabled;
            }

            var gracePeriod = configuration["ArchiveReviewGracePeriodInMonths"];
            if (String.IsNullOrEmpty(gracePeriod) || !int.TryParse(gracePeriod, out _autoArchiveInactiveGracePeriodMonths))
            {
                _autoArchiveInactiveGracePeriodMonths = 4;
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_isDisabled)
            {
                try
                {
                    await _reviewManager.UpdateReviewBackground();
                    await ArchiveInactiveReviews(stoppingToken, _autoArchiveInactiveGracePeriodMonths);
                }
                catch (Exception ex)
                {
                    _telemetryClient.TrackException(ex);
                }
            }
        }

        private async Task ArchiveInactiveReviews(CancellationToken stoppingToken, int archiveAfter)
        {
            do
            {
                try
                {
                    await _reviewManager.AutoArchiveReviews(archiveAfter);
                }
                catch(Exception ex)
                {
                    _telemetryClient.TrackException(ex);
                }
                finally
                {
                    // Wait 6 hours before running archive task again
                    await Task.Delay(6 * 60 * 60000, stoppingToken);
                }                
            }
            while (!stoppingToken.IsCancellationRequested);
        }
    }
}
