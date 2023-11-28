using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using System.Text;
using System;
using System.Collections.Generic;
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
    /// To utilize this, ensure that ConfigureLogger(loggerFactory) is called in Startup.cs Configure() function.
    /// 
    /// Yes, we could instantiate a new RecordingHandler for each recording as it comes in, but that is 
    ///  A) Not the most fun to debug issues with
    ///  B) Just slower than re-using the same instance
    ///  C) There is no real reason (other than to get access to DI-ed parameters) to switch the existing method.
    ///  
    /// In the case of running CLI commands, the DebugLogger will not be initialized as Startup's Configure has not
    /// and will not be executed. In this case, the non-async functions, LogInformation and LogDebug, will use
    /// Console.WriteLine and Debug.Writeline if the logger is null. The async functions are only called when running
    /// in a server.
    /// </summary>
    public static class DebugLogger
    {
        // internal for testing
        internal static ILogger Logger { get; set; }

        public static void ConfigureLogger(ILoggerFactory factory)
        {
            if (Logger == null && factory != null)
            {
                Logger = factory.CreateLogger("Azure.Sdk.Tools.TestProxy");
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
            var result = Logger?.IsEnabled(LogLevel.Debug);

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
        /// Simple access to the logging api. Accepts a simple message (preformatted) and logs to information logger.
        /// </summary>
        /// <param name="details">The content which should be logged.</param>
        public static void LogInformation(string details)
        {
            if (null != Logger)
            {
                Logger.LogInformation(details);
            }
            else
            {
                System.Console.WriteLine(details);
            }
        }

        public static void LogError(string details)
        {
            if (null != Logger)
            {
                Logger.LogError(details);
            }
            else
            {
                System.Console.WriteLine(details);
            }
        }

        public static void LogError(int statusCode, Exception e)
        {
            var details = statusCode.ToString() + Environment.NewLine + e.Message + Environment.NewLine + e.StackTrace;
            if (null != Logger)
            {
                Logger.LogError(details);
            }
            else
            {
                System.Console.WriteLine(details);
            }
        }

        public static void LogTrace(string details)
        {
            if (null != Logger)
            {
                Logger.LogTrace(details);
            }
            else
            {
                System.Console.WriteLine(details);
            }
        }

        /// <summary>
        /// Simple access to the logging api. Accepts a simple message (preformatted) and logs to debug logger.
        /// </summary>
        /// <param name="details">The content which should be logged.</param>
        public static void LogDebug(string details)
        {
            if (Logger != null)
            {
                Logger.LogDebug(details);
            }
            else
            {
                // Honor the following environment variable settings:
                //
                // LOGGING__LOGLEVEL
                // LOGGING__LOGLEVEL__DEFAULT
                // LOGGING__LOGLEVEL__MICROSOFT
                //
                // We do this because when invoking against CLI commands, we don't have access to the same logging provider
                // that is provided by the ASP.NET hostbuilder. Given that, we want to get as close as possible.
                Enum.TryParse(typeof(LogLevel),
                    Environment.GetEnvironmentVariable("LOGGING__LOGLEVEL") ?? string.Empty,
                    ignoreCase: true, out var loglevel);

                Enum.TryParse(typeof(LogLevel),
                    Environment.GetEnvironmentVariable("LOGGING__LOGLEVEL__DEFAULT") ?? string.Empty,
                    ignoreCase: true, out var loglevel_default);

                Enum.TryParse(typeof(LogLevel),
                    Environment.GetEnvironmentVariable("LOGGING__LOGLEVEL__MICROSOFT") ?? string.Empty,
                    ignoreCase: true, out var loglevel_microsoft);

                if ((loglevel != null && (int)loglevel <= 1)
                    || (loglevel_default != null && (int)loglevel_default <= 1)
                    || (loglevel_microsoft != null && (int)loglevel_microsoft <= 1))
                {
                    System.Console.WriteLine(details);
                }
            }
        }

        /// <summary>
        /// Helper function used to evaluate an incoming httprequest and non-destructively log some information about it using a provided logger instance. When not
        /// actually logging anything, this function is entirely passthrough.
        /// </summary>
        /// <param name="loggerInstance">Usually will be the DI-ed individual ILogger instance from a controller. However any valid ILogger instance is fine here.</param>
        /// <param name="req">The http request which needs to be detailed.</param>
        /// <returns></returns>
        public static void LogAdminRequestDetails(ILogger loggerInstance, HttpRequest req)
        {
            if(CheckLogLevel(LogLevel.Debug))
            {
                var headers = Encoding.UTF8.GetString(JsonSerializer.SerializeToUtf8Bytes(req.Headers));
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("URI: [ " + req.GetDisplayUrl() + "]");
                sb.AppendLine("Headers: [" + headers + "]");
                loggerInstance.LogDebug(sb.ToString());
            }
        }

        /// <summary>
        /// Helper function used to evaluate an incoming httprequest and non-destructively log some information about it using the non-DI logger instance. When not
        /// actually logging anything, this function is entirely passthrough.
        /// </summary>
        /// <param name="req">The http request which needs to be detailed.</param>
        /// <param name="sanitizers">The set of sanitizers to apply before logging.</param>
        /// <returns>The log line.</returns>
        public static void LogRequestDetails(HttpRequest req, IEnumerable<RecordedTestSanitizer> sanitizers)
        {
            if (CheckLogLevel(LogLevel.Debug))
            {
                Logger.LogDebug(_generateLogLine(req, sanitizers));
            }
        }

        /// <summary>
        /// Generate a line of data from an http request. This is non-destructive, which means it does not mess 
        /// with the request Body stream at all.
        /// </summary>
        /// <param name="req">The request</param>
        /// <param name="sanitizers">The set of sanitizers to apply before logging.</param>
        /// <returns>The log line.</returns>
        private static string _generateLogLine(HttpRequest req, IEnumerable<RecordedTestSanitizer> sanitizers)
        {
            RecordEntry entry = RecordingHandler.CreateNoBodyRecordEntry(req);

            if (sanitizers != null)
            {
                foreach (var sanitizer in sanitizers)
                {
                    sanitizer.Sanitize(entry);
                }
            }

            var headers = Encoding.UTF8.GetString(JsonSerializer.SerializeToUtf8Bytes(entry.Request.Headers));

            StringBuilder sb = new StringBuilder();

            sb.AppendLine("URI: [ " + entry.RequestUri + "]");
            sb.AppendLine("Headers: [" + headers + "]");

            return sb.ToString();
        }
    }
}
