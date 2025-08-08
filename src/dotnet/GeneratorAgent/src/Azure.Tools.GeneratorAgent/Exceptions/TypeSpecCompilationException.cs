using System;

namespace Azure.Tools.GeneratorAgent.Exceptions
{
    /// <summary>
    /// Exception thrown when TypeSpec compilation fails.
    /// This exception type is suitable for AI analysis and automated fixing.
    /// </summary>
    internal class TypeSpecCompilationException : ProcessExecutionException
    {
        public TypeSpecCompilationException(
            string command,
            string output,
            string error,
            int exitCode,
            Exception? inner = null)
            : base("TypeSpec compilation failed", command, output, error, exitCode, inner) { }
    }
}
