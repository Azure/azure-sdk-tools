using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.PipelineWitness.Tests
{
    public class TestLogger : ILogger
    {
        internal List<object> Logs { get; } = new List<object>();

        public IDisposable BeginScope<TState>(TState state)
        {
            throw new NotImplementedException();
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
            Func<TState, Exception, string> formatter)
        {
            Logs.Add(state);
        }
    }

    public class TestLoggingFactory : ILoggerFactory
    {
        private readonly TestLogger logger;

        public TestLoggingFactory(TestLogger logger)
        {
            this.logger = logger;
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public void AddProvider(ILoggerProvider provider)
        {
            throw new NotImplementedException();
        }

        public ILogger CreateLogger(string categoryName)
        {
            return this.logger;
        }
    }
}
