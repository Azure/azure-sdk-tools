using System.Threading.Tasks;
using System.Threading;
using System;
using Microsoft.Extensions.Hosting;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Managers;
using Microsoft.Extensions.Configuration;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using System.Collections.Generic;
using System.Linq;
using APIViewWeb.Helpers;

namespace APIViewWeb.HostedServices
{
    public class LinesWithDiffBackgroundHostedService : BackgroundService
    {
        private readonly bool _isDisabled;
        private readonly IReviewManager _reviewManager;
        private readonly IAPIRevisionsManager _apiRevisionManager;
        private readonly TelemetryClient _telemetryClient;

        public LinesWithDiffBackgroundHostedService(IReviewManager reviewManager, 
            IAPIRevisionsManager apiRevisionManager, 
            IConfiguration configuration,
            TelemetryClient telemetryClient)
        {
            _reviewManager = reviewManager;
            _apiRevisionManager = apiRevisionManager;
            _telemetryClient = telemetryClient;

            if (bool.TryParse(configuration["LinesWithDiffBackgroundTaskDisabled"], out bool taskDisabled))
            {
                _isDisabled = taskDisabled;
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_isDisabled)
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    var requestTelemetry = new RequestTelemetry { Name = "Computing Line Number of Sections with Diff" };
                    var operation = _telemetryClient.StartOperation(requestTelemetry);
                    try
                    {
                        var reviews = await _reviewManager.GetReviewsAsync(language: ApiViewConstants.SwaggerLanguage);
                        reviews = reviews.OrderBy(r => r.CreatedOn).Reverse();
                        int index = 1;
                        int total = reviews.Count();
                        foreach (var review in reviews)
                        {
                            _telemetryClient.TrackTrace($"Computing Line Number of Sections with Diff for Review {review.Id}, processing {index}/{total}.");
                            var apiRevisions = await _apiRevisionManager.GetAPIRevisionsAsync(reviewId: review.Id);
                            var processedRevisions = new HashSet<string>();
                            foreach (var apiRevision in apiRevisions)
                            {
                                processedRevisions.Add(apiRevision.Id);
                                await _apiRevisionManager.GetLineNumbersOfHeadingsOfSectionsWithDiff(reviewId: review.Id, apiRevision: apiRevision, apiRevisions: apiRevisions.Where(r => !processedRevisions.Contains(r.Id)));                            
                            }
                            index++;
                        }
                        break; // Exit the loop after successful completion
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException))
                    {
                        _telemetryClient.TrackException(ex);
                        // Wait before retrying to avoid tight loop on persistent errors
                        try
                        {
                            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                    }
                    finally
                    {
                        _telemetryClient.StopOperation(operation);
                    }
                }
            }
        }
    }
}
