using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.Cli.Tests.TestHelpers {
    public class TestLogger<T> : ILogger<T>
    {
        public readonly List<object> Logs = new List<object>();

        public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception exception,
            Func<TState, Exception, string> formatter)
        {
            // store the raw state (typically a FormattedLogValues)
            Logs.Add(state);
        }

        // Null disposable for BeginScope
        private class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new NullScope();
            public void Dispose() { }
        }
    }
}
