using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.PipelineWitness.Tests
{
    public class TestLogger : ILogger
    {
        internal List<object> Logs { get; } = [];

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
}
