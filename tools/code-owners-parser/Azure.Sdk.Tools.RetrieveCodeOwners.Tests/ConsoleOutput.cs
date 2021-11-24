using System;
using System.IO;

namespace Azure.Sdk.Tools.RetrieveCodeOwners.Tests
{
    public class ConsoleOutput : IDisposable
    {
        private StringWriter stringWriter;
        private TextWriter originalOutput;

        public ConsoleOutput()
        {
            this.stringWriter = new StringWriter();
            this.originalOutput = Console.Out;
            Console.SetOut(this.stringWriter);
        }

        public string GetOuput()
        {
            return this.stringWriter.ToString();
        }

        public void Dispose()
        {
            Console.SetOut(this.originalOutput);
            this.stringWriter.Dispose();
            this.originalOutput.Dispose();
        }

        public void Close()
        {
            this.stringWriter.Close();
            originalOutput.Close();
        }
    }
    
}
