using System;

namespace Azure.Tools.GeneratorAgent.Exceptions
{
    /// <summary>
    /// Base exception class for process execution related errors.
    /// Contains common properties for all process execution failures.
    /// </summary>
    internal abstract class ProcessExecutionException : Exception
    {
        /// <summary>
        /// The command that was being executed
        /// </summary>
        public string Command { get; }

        /// <summary>
        /// The standard output from the process (if any)
        /// </summary>
        public string Output { get; }

        /// <summary>
        /// The standard error from the process (if any)
        /// </summary>
        public string Error { get; }

        /// <summary>
        /// The process exit code (if available)
        /// </summary>
        public int? ExitCode { get; }

        protected ProcessExecutionException(
            string message,
            string command,
            string output,
            string error,
            int? exitCode = null,
            Exception? inner = null) : base(message, inner)
        {
            Command = command;
            Output = output;
            Error = error;
            ExitCode = exitCode;
        }
    }
}
