
using System;

namespace Azure.Sdk.Tools.TestProxy.Console
{
    /// <summary>
    /// Implementation of IConsoleWrapper that's simply a passthrough to the Console functions.
    /// </summary>
    public class ConsoleWrapper : IConsoleWrapper
    {
        public void Write(string message)
        {
            System.Console.Write(message);
        }
        public void WriteLine(string message)
        {
            System.Console.WriteLine(message);
        }
        public string ReadLine()
        {
            return System.Console.ReadLine();
        }
    }
}
