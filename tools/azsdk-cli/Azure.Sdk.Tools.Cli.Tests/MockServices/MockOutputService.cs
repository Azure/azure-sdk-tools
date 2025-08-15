using Azure.Sdk.Tools.Cli.Services;

namespace Azure.Sdk.Tools.Cli.Tests.MockServices
{
    /// <summary>
    /// A really simple mock that just accumulates all the outputs into a List, which you can assert on
    /// after the test is complete.
    /// </summary>
    internal class MockOutputService : IOutputService
    {
        /// <summary>
        /// All the collected outputs, in the order they were added.
        /// The Method is the actual name of the method in the OutputService
        /// </summary>
        public IEnumerable<(string Method, object OutputValue)> Outputs => outputs;
        private readonly List<(string Method, object OutputValue)> outputs;

        public MockOutputService()
        {
            outputs = [];
        }

        public string Format(object response)
        {
            lock (outputs)
            {
                outputs.Add((nameof(Format), response));
            }

            return response?.ToString() ?? string.Empty;
        }

        public void Output(object output)
        {
            lock (outputs)
            {
                outputs.Add((nameof(Output), output));
            }
        }

        public void Output(string output)
        {
            lock (outputs)
            {
                outputs.Add((nameof(Output), output));
            }
        }

        public void OutputError(object output)
        {
            lock (outputs)
            {
                outputs.Add((nameof(OutputError), output));
            }
        }

        public void OutputError(string output)
        {
            lock (outputs)
            {
                outputs.Add((nameof(OutputError), output));
            }
        }

        public string ValidateAndFormat<T>(string response)
        {
            lock (outputs)
            {
                outputs.Add((nameof(ValidateAndFormat), response));
            }

            return response ?? string.Empty;
        }
    }
}
