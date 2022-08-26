
using System;

namespace Azure.Sdk.Tools.TestProxy.ConsoleWrapper
{
    /// <summary>
    /// Implementation of IConsoleWrapper that's simply a passthrough to the Console functions.
    /// </summary>
    public class ConsoleWrapperImpl : IConsoleWrapper
    {
        public void Write(string message)
        {
            Console.Write(message);
        }
        public void WriteLine(string message)
        {
            Console.WriteLine(message);
        }
        public string ReadLine()
        {
            return Console.ReadLine();
        }
    }
}
