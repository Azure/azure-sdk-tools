using System;

namespace Azure.Tools.GeneratorAgent.Exceptions
{
    /// <summary>
    /// General exception thrown when any process execution fails.
    /// This exception type is suitable for AI analysis and automated fixing.
    /// </summary>
    internal class GeneralProcessExecutionException : ProcessExecutionException
    {
        public GeneralProcessExecutionException(
            string message,
            string command,
            string output,
            string error,
            int exitCode,
            Exception? inner = null)
            : base(message, command, output, error, exitCode, inner) { }
    }
}
