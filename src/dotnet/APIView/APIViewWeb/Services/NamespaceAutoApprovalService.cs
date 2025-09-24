using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using APIViewWeb.Managers;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace APIViewWeb.Services
{
    // TODO: 3 days auto-approval feature is temporarily disabled - entire class commented out
    /*
    /// <summary>
    /// Background service that automatically approves namespace reviews after 3 business days with no open comments
    /// </summary>
    public class NamespaceAutoApprovalService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<NamespaceAutoApprovalService> _logger;

        public NamespaceAutoApprovalService(IServiceProvider serviceProvider, ILogger<NamespaceAutoApprovalService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {


            // TODO: 3 days auto-approval feature is temporarily disabled
            // Uncomment the code below to re-enable this feature
            
            
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var reviewManager = scope.ServiceProvider.GetRequiredService<IReviewManager>();
                    
                    _logger.LogDebug("Processing pending namespace auto-approvals");
                    await reviewManager.ProcessPendingNamespaceAutoApprovals();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in namespace auto-approval process");
                }
                
                // Run every 6 hours
                await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
            }
            
            
            _logger.LogInformation("Auto-approval feature is disabled. Service will not process any auto-approvals.");
            
            // Keep the service alive but do nothing
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }
    }
    */
}
