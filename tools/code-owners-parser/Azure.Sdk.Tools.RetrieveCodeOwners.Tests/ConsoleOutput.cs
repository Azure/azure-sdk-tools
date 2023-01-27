using System;
using System.IO;

namespace Azure.Sdk.Tools.RetrieveCodeOwners.Tests
{
    /// <summary>
    /// The class is to redirect console STDOUT and STDERR to string writer.
    /// </summary>
    public class ConsoleOutput : IDisposable
    {
        private readonly StringWriter stdoutWriter, stderrWriter;
        private readonly TextWriter originalStdout, originalStderr;

        /// <summary>
        /// The constructor is where we take in the console output and output to string writer.
        /// </summary>
        public ConsoleOutput()
        {
            this.stdoutWriter = new StringWriter();
            this.stderrWriter = new StringWriter();
            this.originalStdout = Console.Out;
            this.originalStderr = Console.Error;
            Console.SetOut(this.stdoutWriter);
            Console.SetError(this.stderrWriter);
        }

        /// <summary>
        /// Writes the text representation of a string builder to the string.
        /// </summary>
        /// <returns>The string from console output.</returns>
        public string GetStdout()
            => this.stdoutWriter.ToString();

        public string[] GetStdoutLines()
            => this.stdoutWriter.ToString().Split(Environment.NewLine);

        public string GetStderr()
            => this.stderrWriter.ToString();

        public string[] GetStderrLines()
            => this.stderrWriter.ToString().Split(Environment.NewLine);

        /// <summary>
        /// Releases all resources used by the originalOutput and stringWriter object.
        /// </summary>
        public void Dispose()
        {
            Console.SetOut(this.originalStdout);
            Console.SetError(this.originalStderr);
            this.stdoutWriter.Dispose();
            this.stderrWriter.Dispose();
            this.originalStdout.Dispose();
            this.originalStderr.Dispose();
            // https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1816
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Closes the current writer and releases any system resources associated with the writer
        /// </summary>
        public void Close()
        {
            this.stdoutWriter.Close();
            this.stderrWriter.Close();
            this.originalStderr.Close();
            this.originalStdout.Close();
        }
    }
    
}
