using System;
using System.IO;

namespace Azure.Sdk.Tools.RetrieveCodeOwners.Tests
{
    /// <summary>
    /// The class is to redirect console output to string writer.
    /// </summary>
    public class ConsoleOutput : IDisposable
    {
        private readonly StringWriter stringWriter;
        private readonly TextWriter originalOutput;

        /// <summary>
        /// The constructor is where we take in the console output and output to string writer.
        /// </summary>
        public ConsoleOutput()
        {
            this.stringWriter = new StringWriter();
            this.originalOutput = Console.Out;
            Console.SetOut(this.stringWriter);
        }

        /// <summary>
        /// Writes the text representation of a string builder to the string.
        /// </summary>
        /// <returns>The string from console output.</returns>
        public string GetOutput()
        {
            return this.stringWriter.ToString();
        }

        /// <summary>
        /// Releases all resources used by the originalOutput and stringWriter object.
        /// </summary>
        public void Dispose()
        {
            Console.SetOut(this.originalOutput);
            this.stringWriter.Dispose();
            this.originalOutput.Dispose();
            // https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1816
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Closes the current writer and releases any system resources associated with the writer
        /// </summary>
        public void Close()
        {
            this.stringWriter.Close();
            this.originalOutput.Close();
        }
    }
    
}
