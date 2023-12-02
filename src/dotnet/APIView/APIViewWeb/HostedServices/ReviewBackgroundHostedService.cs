// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using APIViewWeb.Managers;
using APIViewWeb.Managers.Interfaces;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace APIViewWeb.HostedServices
{
    public class ReviewBackgroundHostedService : BackgroundService
    {
        private readonly bool _isDisabled;
        private readonly IReviewManager _reviewManager;
        private readonly IAPIRevisionsManager _apiRevisionManager;
        private readonly int _autoArchiveInactiveGracePeriodMonths; // This is inactive duration in months
        private readonly HashSet<string> _upgradeDisabledLangs = new HashSet<string>();
        private readonly int _backgroundBatchProcessCount;

        static TelemetryClient _telemetryClient = new(TelemetryConfiguration.CreateDefault());

        public ReviewBackgroundHostedService(IReviewManager reviewManager, IAPIRevisionsManager apiRevisionManager, IConfiguration configuration)
        {
            _reviewManager = reviewManager;
            _apiRevisionManager = apiRevisionManager;

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
            var backgroundTaskDisabledLangs = configuration["ReviewUpdateDisabledLanguages"];
            if(!string.IsNullOrEmpty(backgroundTaskDisabledLangs))
            {
                _upgradeDisabledLangs.UnionWith(backgroundTaskDisabledLangs.Split(','));
            }

            // Number of review revisions to be passed to pipeline when updating review with a new parser version
            var batchCount = configuration["ReviewUpdateBatchCount"];
            if (String.IsNullOrEmpty(batchCount) || !int.TryParse(batchCount, out _backgroundBatchProcessCount))
            {
                _backgroundBatchProcessCount = 20;
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_isDisabled)
            {
                try
                {
                    await _reviewManager.UpdateReviewsInBackground(_upgradeDisabledLangs, _backgroundBatchProcessCount);
                    await ArchiveInactiveAPIReviews(stoppingToken, _autoArchiveInactiveGracePeriodMonths);
                }
                catch (Exception ex)
                {
                    _telemetryClient.TrackException(ex);
                }
            }
        }

        private async Task ArchiveInactiveAPIReviews(CancellationToken stoppingToken, int archiveAfter)
        {
            do
            {
                try
                {
                    await _apiRevisionManager.AutoArchiveAPIRevisions(archiveAfter);
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
