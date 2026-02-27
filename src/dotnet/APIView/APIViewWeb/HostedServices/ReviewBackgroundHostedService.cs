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
        private static readonly TimeSpan BackgroundTaskInterval = TimeSpan.FromHours(6);

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
            if (_isDisabled)
            {
                _telemetryClient.TrackTrace("ReviewBackgroundHostedService is disabled via configuration. Exiting.");
                return;
            }

            _telemetryClient.TrackTrace($"ReviewBackgroundHostedService starting. ArchiveGracePeriod={_autoArchiveInactiveGracePeriodMonths} months, PurgeGracePeriod={_autoPurgeGracePeriodMonths} months");

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
                _telemetryClient.TrackTrace($"ReviewBackgroundHostedService encountered a fatal error: {ex.Message}");
                _telemetryClient.TrackException(ex);
            }
        }

        private async Task ArchiveInactiveAPIReviews(CancellationToken stoppingToken, int archiveAfter)
        {
            do
            {
                _telemetryClient.TrackTrace($"AutoArchive cycle starting. ArchiveAfter={archiveAfter} months");
                try
                {
                    await _apiRevisionManager.AutoArchiveAPIRevisions(archiveAfter);
                    _telemetryClient.TrackTrace("AutoArchive cycle completed successfully.");
                }
                catch(Exception ex)
                {
                    _telemetryClient.TrackTrace($"AutoArchive cycle failed: {ex.Message}");
                    _telemetryClient.TrackException(ex);
                }
                finally
                {
                    // Wait before running archive task again
                    await Task.Delay(BackgroundTaskInterval, stoppingToken);
                }                
            }
            while (!stoppingToken.IsCancellationRequested);
        }

        private async Task PurgeDeletedAPIReviews(CancellationToken stoppingToken, int purgeAfter)
        {
            do
            {
                _telemetryClient.TrackTrace($"AutoPurge cycle starting. PurgeAfter={purgeAfter} months");
                try
                {
                    await _apiRevisionManager.AutoPurgeAPIRevisions(purgeAfter);
                    _telemetryClient.TrackTrace("AutoPurge cycle completed successfully.");
                }
                catch(Exception ex)
                {
                    _telemetryClient.TrackTrace($"AutoPurge cycle failed: {ex.Message}");
                    _telemetryClient.TrackException(ex);
                }
                finally
                {
                    // Wait before running purge task again
                    await Task.Delay(BackgroundTaskInterval, stoppingToken);
                }                
            }
            while (!stoppingToken.IsCancellationRequested);
        }
    }
}
