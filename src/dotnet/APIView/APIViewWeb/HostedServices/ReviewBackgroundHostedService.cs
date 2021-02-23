using System;
using System.Threading;
using System.Threading.Tasks;
using APIViewWeb.Respositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace APIViewWeb.HostedServices
{
    public class ReviewBackgroundHostedService : IHostedService, IDisposable
    {
        private int _executionInterval = 24;
        private bool _isDisabled = false;
        private Timer _timer;
        private ReviewManager _reviewManager;

        public ReviewBackgroundHostedService(ReviewManager reviewManager, IConfiguration configuration)
        {
            _reviewManager = reviewManager;
            var interval = configuration["BackgroundTaskRunInterval"];
            int.TryParse(interval, out _executionInterval);
            if(_executionInterval <= 0)
            {
                // Set default interval as 24 hours
                _executionInterval = 24;
            }
            // We can disable background task using app settings if required
            var taskDisabled = configuration["BackgroundTaskDisabled"];
            if (!String.IsNullOrEmpty(taskDisabled) && taskDisabled == "true")
            {
                _isDisabled = true;
            }
        }

        public Task StartAsync(CancellationToken stoppingToken)
        {
            _timer = new Timer(RunJob, null, TimeSpan.Zero, TimeSpan.FromHours(_executionInterval));
            return Task.CompletedTask;
        }

        private void RunJob(object state)
        {
            if(!_isDisabled)
            {
                _reviewManager.UpdateReviewBackground();
            }
        }

        public Task StopAsync(CancellationToken stoppingToken)
        {
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
