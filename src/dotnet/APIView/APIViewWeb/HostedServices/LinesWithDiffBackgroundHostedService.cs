using System.Threading.Tasks;
using System.Threading;
using System;
using Microsoft.Extensions.Hosting;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Managers;
using Microsoft.Extensions.Configuration;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights;
using MongoDB.Driver.Linq;
using System.Linq;
using APIViewWeb.LeanModels;

namespace APIViewWeb.HostedServices
{
    public class LinesWithDiffBackgroundHostedService : BackgroundService
    {
        private readonly bool _isDisabled;
        private readonly IReviewManager _reviewManager;
        private readonly IAPIRevisionsManager _apiRevisionManager;

        static TelemetryClient _telemetryClient = new(TelemetryConfiguration.CreateDefault());

        public LinesWithDiffBackgroundHostedService(IReviewManager reviewManager, IAPIRevisionsManager apiRevisionManager, IConfiguration configuration)
        {
            _reviewManager = reviewManager;
            _apiRevisionManager = apiRevisionManager;

            if (bool.TryParse(configuration["LinesWithDiffBackgroundTaskDisabled"], out bool taskDisabled))
            {
                _isDisabled = taskDisabled;
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_isDisabled)
            {
                try
                {
                    var reviews = await _reviewManager.GetReviewsAsync(language: "Swagger");
                    foreach (var review in reviews)
                    {
                        var apiRevisions = await _apiRevisionManager.GetAPIRevisionsAsync(reviewId: review.Id);
             
                        foreach (var apiRevision in apiRevisions)
                        {
                            await _apiRevisionManager.GetLineNumbersOfHeadingsOfSectionsWithDiff(reviewId: review.Id, apiRevision: apiRevision);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _telemetryClient.TrackException(ex);
                }
            }
        }
    }
}
