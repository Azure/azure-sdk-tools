using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using APIViewWeb.Managers;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace APIViewWeb.Services
{
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
            _logger.LogInformation("Namespace Auto-Approval Service started");

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
            
            _logger.LogInformation("Namespace Auto-Approval Service stopped");
        }
    }
}
