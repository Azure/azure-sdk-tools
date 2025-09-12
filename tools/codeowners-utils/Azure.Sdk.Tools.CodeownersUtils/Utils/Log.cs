using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Azure.Sdk.Tools.CodeownersUtils.Utils
{
    /// <summary>
    /// Lightweight logging helper for the library.
    /// By default this uses a NullLogger so consumers don't have to configure logging.
    /// Consumers (apps/tests) can call Log.Configure(...) with an ILogger to
    /// enable logging to their preferred sink.
    /// </summary>
    public static class Log
    {
        private static ILogger logger = NullLogger.Instance;

        public static ILogger Logger => logger;

        public static void Configure(ILogger logger)
        {
            Log.logger = logger ?? NullLogger.Instance;
        }

        public static void Configure(ILoggerFactory loggerFactory)
        {
            Log.logger = loggerFactory?.CreateLogger(typeof(Log).Assembly.GetName().Name)?? NullLogger.Instance;
        }
    }
}
