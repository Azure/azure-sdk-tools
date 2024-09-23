using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace PixelServer.Controllers
{
    [ApiController]
    public class TrackingController : ControllerBase
    {
        private readonly IMemoryCache _cache;
        private readonly TelemetryClient _telemetry;

        public TrackingController(IMemoryCache memoryCache, TelemetryClient telemetry)
        {
            _cache = memoryCache;
            _telemetry = telemetry;
        }

        /// <summary>
        /// Currently the only entrypoint, services a 1 pixel image while recording that there was an impression for the specific path.
        /// 
        /// Uses in-memory cache of afore-seen IPs to recognize a "duplicate" impression. The addresses themselves are not recorded.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        [Route("api/impressions/{*path}")]
        [HttpGet]
        public IActionResult Get(string path)
        {
            _telemetry.TrackEvent("PixelImpression", new Dictionary<string, string>()
            {
                { "visitor_duplicate", getCachedVisitorStatus(getRequestAddress()) },
                { "visitor_path", path }
            });

            return new ImageActionResult();
        }
        
        private string getCachedVisitorStatus(System.Net.IPAddress address)
        {
            if (!_cache.TryGetValue(address, out _))
            {
                // Set cache options.
                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    // Keep in cache for this time, reset time if accessed.
                    .SetSlidingExpiration(TimeSpan.FromHours(1));

                // Save data in cache.
                _cache.Set(address, DateTime.UtcNow, cacheEntryOptions);

                return "false";
            }

            return "true";
        }

        private System.Net.IPAddress getRequestAddress()
        {
            return Request.HttpContext.Connection.RemoteIpAddress;
        }

        private class ImageActionResult : IActionResult
        {
            private static readonly byte[] _imgPayload = System.IO.File.ReadAllBytes(AppDomain.CurrentDomain.BaseDirectory + "/Etc/pixel.png");

            public Task ExecuteResultAsync(ActionContext context)
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status200OK;
                context.HttpContext.Response.ContentType = "image/png";
                context.HttpContext.Response.ContentLength = _imgPayload.Length;
                return context.HttpContext.Response.Body.WriteAsync(_imgPayload, 0, _imgPayload.Length);
            }
        }

    }
}
