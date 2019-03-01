using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace PixelServer.Controllers
{
    [Route("api/impressions")]
    [ApiController]
    public class TrackingController : ControllerBase
    {
        // used for manual logs to application insights
        private static TelemetryClient telemetry;

        // populated and returned as 1 pixel image
        private static byte[] img;
        private static string imgPath = AppDomain.CurrentDomain.BaseDirectory + "/Etc/pixel.png";

        private IMemoryCache _cache;

        public TrackingController(IMemoryCache memoryCache)
        {
            _cache = memoryCache;
        }

        /// <summary>
        /// Currently the only entrypoint, services a 1 pixel image while recording that there was an impression for the specific path.
        /// 
        /// Uses in-memory cache of afore-seen IPs to recognize a "duplicate" impression. The addresses themselves are not recorded.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<ActionResult> Get(string path)
        {
            await trackEventAsync(path);

            return File(getCachedImage(), "image/png");
        }

        private async Task trackEventAsync(string path)
        {
            if (telemetry == null)
            {
                telemetry = new TelemetryClient();
            }

            await Task.Run(() => {
                telemetry.TrackEvent("PixelImpression", new Dictionary<string, string>()
                {
                    { "visitor_duplicate", getCachedVisitorStatus(getRequestAddress()) },
                    { "visitor_path", path }
                });
            });
        }

        private string getCachedVisitorStatus(string address)
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

        private byte[] getCachedImage()
        {
            if (img == null)
            {
                img = System.IO.File.ReadAllBytes(imgPath);
            }

            return img;
        }

        private string getRequestAddress()
        {
            return Request.HttpContext.Connection.RemoteIpAddress.ToString();
        }
    }
}
