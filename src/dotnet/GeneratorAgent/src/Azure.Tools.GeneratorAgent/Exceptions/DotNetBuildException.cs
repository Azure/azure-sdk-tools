using System;

namespace Azure.Tools.GeneratorAgent.Exceptions
{
    /// <summary>
    /// Exception thrown when .NET build process fails.
    /// This exception type is suitable for AI analysis and automated fixing.
    /// </summary>
    internal class DotNetBuildException : ProcessExecutionException
    {
        public DotNetBuildException(
            string command,
            string output,
            string error,
            int exitCode,
            Exception? inner = null)
            : base("DotNet build failed", command, output, error, exitCode, inner) { }
    }
}
