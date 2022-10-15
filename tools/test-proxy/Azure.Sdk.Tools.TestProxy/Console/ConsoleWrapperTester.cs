using System;

namespace Azure.Sdk.Tools.TestProxy.Console
{
    /// <summary>
    /// Implementation of IConsoleWrapper that will be used to test commands, like Reset, that require user input.
    /// </summary>
    public class ConsoleWrapperTester : IConsoleWrapper
    {
        private string _readLineResponse;

        public ConsoleWrapperTester() { }

        /// <summary>
        /// Overloaded constructor takes in a string that'll be returned as the ReadLine response.
        /// </summary>
        /// <param name="readLineResponse">string that'll be returned as the ReadLine response</param>
        public ConsoleWrapperTester(string readLineResponse)
        {
            _readLineResponse = readLineResponse;
        }

        /// <summary>
        /// Set the ReadLine response.
        /// </summary>
        /// <param name="readLineResponse">string that'll be returned as the ReadLine response</param>
        public void SetReadLineResponse(string readLineResponse)
        {
            _readLineResponse = readLineResponse;
        }
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
            System.Console.WriteLine($"ReadLine response for test: '{_readLineResponse}'");
            return _readLineResponse;
        }
    }
}
