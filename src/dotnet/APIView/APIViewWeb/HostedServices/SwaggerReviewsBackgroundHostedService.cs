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
    public class SwaggerReviewsBackgroundHostedService : BackgroundService
    {
        private readonly bool _isDisabled;
        private readonly IReviewManager _reviewManager;

        static TelemetryClient _telemetryClient = new (TelemetryConfiguration.CreateDefault());


        public SwaggerReviewsBackgroundHostedService(IReviewManager reviewManager, IConfiguration configuration)
        {
            _reviewManager = reviewManager;
            if (bool.TryParse(configuration["SwaggerMetaDataBackgroundTaskDisabled"], out bool taskDisabled))
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
                    await _reviewManager.UpdateSwaggerReviewsMetaData();
                }
                catch (Exception ex)
                {
                    _telemetryClient.TrackException(ex);
                }
                
            }
        }
    }
}
