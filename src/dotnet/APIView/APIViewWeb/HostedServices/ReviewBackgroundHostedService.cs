// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using APIViewWeb.Managers;
using APIViewWeb.Managers.Interfaces;
using Microsoft.ApplicationInsights;
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
        private readonly int _autoPurgeGracePeriodMonths; // This is the period after soft-delete before hard-delete
        private readonly HashSet<string> _upgradeDisabledLangs = new HashSet<string>();
        private readonly int _backgroundBatchProcessCount;
        private readonly TelemetryClient _telemetryClient;
        private readonly bool _isUpgradeTestEnabled;
        private readonly string _packageNameFilterForUpgrade;

        public ReviewBackgroundHostedService(
            IReviewManager reviewManager, IAPIRevisionsManager apiRevisionManager,
            IConfiguration configuration, TelemetryClient telemetryClient)
        {
            _reviewManager = reviewManager;
            _apiRevisionManager = apiRevisionManager;
            _telemetryClient = telemetryClient;

            // We can disable background task using app settings if required
            if (bool.TryParse(configuration["BackgroundTaskDisabled"], out bool taskDisabled))
            {
                _isDisabled = taskDisabled;
            }

            if (bool.TryParse(configuration["ReviewUpgradabilityTestEnabled"], out bool upgradeTestEnabled))
            {
                _isUpgradeTestEnabled = upgradeTestEnabled;
            }

            var packageNameFilterForUpgrade = configuration["PackageNameFilterForReviewUpgrade"];
            if (!string.IsNullOrEmpty(packageNameFilterForUpgrade))
            {
                _packageNameFilterForUpgrade = packageNameFilterForUpgrade;
            }

            var gracePeriod = configuration["ArchiveReviewGracePeriodInMonths"];
            if (String.IsNullOrEmpty(gracePeriod) || !int.TryParse(gracePeriod, out _autoArchiveInactiveGracePeriodMonths))
            {
                _autoArchiveInactiveGracePeriodMonths = 4;
            }

            var purgeGracePeriod = configuration["PurgeReviewGracePeriodInMonths"];
            if (String.IsNullOrEmpty(purgeGracePeriod) || !int.TryParse(purgeGracePeriod, out _autoPurgeGracePeriodMonths))
            {
                _autoPurgeGracePeriodMonths = 6; // Default to 6 months after soft-delete
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
                    await _reviewManager.UpdateReviewsInBackground(_upgradeDisabledLangs, _backgroundBatchProcessCount, _isUpgradeTestEnabled, _packageNameFilterForUpgrade);
                    
                    // Start both archive and purge tasks concurrently
                    var archiveTask = ArchiveInactiveAPIReviews(stoppingToken, _autoArchiveInactiveGracePeriodMonths);
                    var purgeTask = PurgeDeletedAPIReviews(stoppingToken, _autoPurgeGracePeriodMonths);
                    
                    await Task.WhenAll(archiveTask, purgeTask);
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
                    await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
                }                
            }
            while (!stoppingToken.IsCancellationRequested);
        }

        private async Task PurgeDeletedAPIReviews(CancellationToken stoppingToken, int purgeAfter)
        {
            do
            {
                try
                {
                    await _apiRevisionManager.AutoPurgeAPIRevisions(purgeAfter);
                }
                catch(Exception ex)
                {
                    _telemetryClient.TrackException(ex);
                }
                finally
                {
                    // Wait 6 hours before running purge task again
                    await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
                }                
            }
            while (!stoppingToken.IsCancellationRequested);
        }
    }
}
