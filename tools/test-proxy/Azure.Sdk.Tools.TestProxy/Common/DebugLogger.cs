using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System.Text;
using System.IO;
using System;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Extensions;

namespace Azure.Sdk.Tools.TestProxy.Common
{
    /// <summary>
    /// Outside of DI, there is no way to use the ASP.NET logging instances elsewhere in your classes libraries.
    /// Given that RecordingHandler is a singleton, and therefore not injectable, we have to use a static
    /// set methodology to get access to the same logging stack as the controllers. This static class allows class libraries 
    /// that are not part of the ASP.NET server stack directly to still log useful information.
    /// 
    ///  and ConfigureLogger(loggerFactory) should definitely be called in Startup.cs Configure() function.
    /// 
    /// Yes, we could instantiate a new RecordingHandler for each recording as it comes in, but that is 
    ///  A) Not the most fun to debug issues with
    ///  B) Just slower than re-using the same instance
    ///  C) There is no real reason (other than to get access to DI-ed parameters) to switch the existing method.
    /// </summary>
    public static class DebugLogger
    {
        private static ILogger logger = null;

        public static void ConfigureLogger(ILoggerFactory factory)
        {
            if (logger == null)
            {
                logger = factory.CreateLogger<HttpRequestInteractions>();
            }

        }

        /// <summary>
        /// Used to retrieve the final log level setting. This is a "runtime" setting that is checking the result AFTER
        /// accounting for launchSettings, appSettings, and environment variable settings.
        /// </summary>
        /// <param name="level">A log level. If that level is enabled, returns true. False otherwise.</param>
        /// <returns></returns>
        public static bool CheckLogLevel(LogLevel level)
        {
            var result = logger?.IsEnabled(LogLevel.Debug);

            if (result.HasValue)
            {
                return result.Value;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Simple access to the logging api. Accepts a simple message (preformatted) and logs to debug logger.
        /// </summary>
        /// <param name="details">The content which should be logged.</param>
        public static void LogDebug(string details)
        {
            logger.LogDebug(details);
        }

        /// <summary>
        /// Helper function used to evaluate an incoming httprequest and non-destructively log some information about it using a provided logger instance. When not
        /// actually logging anything, this function is entirely passthrough.
        /// </summary>
        /// <param name="loggerInstance">Usually will be the DI-ed individual ILogger instance from a controller. However any valid ILogger instance is fine here.</param>
        /// <param name="req">The http request which needs to be detailed.</param>
        /// <returns></returns>
        public static async Task LogRequestDetailsAsync(ILogger loggerInstance, HttpRequest req)
        {
            if(CheckLogLevel(LogLevel.Debug))
            {
                loggerInstance.LogDebug(await _generateLogLine(req));
            }
        }

        /// <summary>
        /// Helper function used to evaluate an incoming httprequest and non-destructively log some information about it using the non-DI logger instance. When not
        /// actually logging anything, this function is entirely passthrough.
        /// </summary>
        /// <param name="req">The http request which needs to be detailed.</param>
        /// <returns></returns>
        public static async Task LogRequestDetailsAsync(HttpRequest req)
        {
            if (CheckLogLevel(LogLevel.Debug))
            {
                logger.LogDebug(await _generateLogLine(req));
            }
        }

        /// <summary>
        /// Generate a line of data from an http request. This is non-destructive, which means it does not mess 
        /// with the request Body stream at all.
        /// </summary>
        /// <param name="req"></param>
        /// <returns></returns>
        private static async Task<string> _generateLogLine(HttpRequest req)
        {
            StringBuilder sb = new StringBuilder();
            string headers = string.Empty;

            using (MemoryStream ms = new MemoryStream())
            {
                await JsonSerializer.SerializeAsync(ms, req.Headers);
                headers = Encoding.UTF8.GetString(ms.ToArray());
            }

            sb.AppendLine("URI: [ " + req.GetDisplayUrl() + "]");
            sb.AppendLine("Headers: [" + headers + "]");

            return sb.ToString();
        }
    }
}
