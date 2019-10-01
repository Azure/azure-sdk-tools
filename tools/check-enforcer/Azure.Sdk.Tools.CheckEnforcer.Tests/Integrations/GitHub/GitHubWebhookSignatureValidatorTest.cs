using Azure.Sdk.Tools.CheckEnforcer.Integrations.GitHub;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.CheckEnforcer.Tests.Integrations.GitHub
{
    public class GitHubWebhookSignatureValidatorTest
    {
        [Test]
        public void VerifyMatchesGitHubTestCase()
        {
            var signature = "sha1=d03207e4b030cf234e3447bac4d93add4c6643d8";
            var secret = "mysecret";
            var payload = "{\"foo\":\"bar\"}";

            var payloadBytes = Encoding.UTF8.GetBytes(payload);

            var isValid = GitHubWebhookSignatureValidator.IsValid(payloadBytes, signature, secret);

            Assert.IsTrue(isValid);
        }
    }
}
