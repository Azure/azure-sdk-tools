using System;

namespace Azure.Tools.GeneratorAgent.Exceptions
{
    /// <summary>
    /// Exception thrown when a process execution times out.
    /// Contains additional context about the timeout duration.
    /// </summary>
    internal class ProcessTimeoutException : ProcessExecutionException
    {
        /// <summary>
        /// The timeout duration that was exceeded
        /// </summary>
        public TimeSpan Timeout { get; }

        public ProcessTimeoutException(
            string command,
            string output,
            string error,
            TimeSpan timeout)
            : base($"Process timed out after {timeout.TotalMilliseconds}ms",
                  command,
                  output,
                  error)
        {
            Timeout = timeout;
        }
    }
}
