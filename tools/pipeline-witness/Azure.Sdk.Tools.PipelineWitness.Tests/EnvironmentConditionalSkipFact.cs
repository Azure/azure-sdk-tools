using System;
using Xunit;

namespace Azure.Sdk.Tools.PipelineWitness.Tests
{
    /// <summary>
    /// Used to skip integration tests in our CI builds. If a test decorated with EnvironmentConditionalSkipFact attribute is collected, it will only run
    /// on a machine if the environment variables PROXY_GIT_TOKEN are available to the test suite.
    /// </summary>
    public sealed class EnvironmentConditionalSkipFact : FactAttribute
    {
        public EnvironmentConditionalSkipFact()
        {
            string devopsPat = Environment.GetEnvironmentVariable("AZURESDK_DEVOPS_TOKEN");
            string blobToken = Environment.GetEnvironmentVariable("AZURESDK_BLOB_CS");

            // and if we don't, skip this test
            if (string.IsNullOrWhiteSpace(devopsPat) || string.IsNullOrWhiteSpace(blobToken))
            {
                Skip = "Ignore integration test when necessary environment variables AZURESDK_DEVOPS_PAT, AZURESDK_BLOB_CS are partially or fully unset.";
            }
        }
    }
}
