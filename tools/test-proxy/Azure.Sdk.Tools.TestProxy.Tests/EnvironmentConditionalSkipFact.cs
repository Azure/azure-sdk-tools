using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Sdk.Tools.TestProxy.Store;
using Xunit;

namespace Azure.Sdk.Tools.TestProxy.Tests
{
    /// <summary>
    /// Used to skip integration tests in our CI builds. If a test decorated with EnvironmentConditionalSkipTheory attribute is collected, it will only run
    /// on a CI machine if the environment variable PROXY_GIT_TOKEN is valid. On a normal dev machine, the variable TF_BUILD is unset, so the test should still
    /// run as part of the test suite.
    /// </summary>
    public sealed class EnvironmentConditionalSkipFact : FactAttribute
    {
        public EnvironmentConditionalSkipFact()
        {
            var token = Environment.GetEnvironmentVariable(GitStore.GIT_TOKEN_ENV_VAR);
            var inCI = Environment.GetEnvironmentVariable("TF_BUILD");

            // If we are in CI, we MUST have environment variable for PROXY_GIT_TOKEN populated.
            // On a normal user's machine, this will still trigger appropriately using default git cred
            if (!string.IsNullOrWhiteSpace(inCI))
            {
                // and if we don't, skip this test
                if (string.IsNullOrWhiteSpace(token))
                {
                    Skip = "Ignore integration test when PROXY_GIT_TOKEN is unset.";
                }
            }
        }
    }
}
