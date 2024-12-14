using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.TestProxy.Common.AutoShutdown
{
    public class ShutdownTimerMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ShutdownTimer _shutdownTimer;
        private readonly ShutdownConfiguration _shutdownConfig;

        public ShutdownTimerMiddleware(RequestDelegate next, ShutdownTimer shutdownTimer, ShutdownConfiguration shutdownConfig)
        {
            _next = next;
            _shutdownTimer = shutdownTimer;
            _shutdownConfig = shutdownConfig;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (_shutdownConfig.EnableAutoShutdown)
            {
                _shutdownTimer.ResetTimer();
            }
            await _next(context);
        }
    }
}
