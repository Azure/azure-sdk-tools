using System.Net.Http;
using NUnit.Framework;
using Azure.Sdk.Tools.TestProxy.HttpPipelineSample;
using Azure.Core.Pipeline;
using System;
using System.IO;
using System.Threading;
using Azure.Core;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.TestProxy.TestExample
{
    /// <summary>
    /// This class is an example integration with the test proxy. This particular implementation assumes that the test-proxy is already running
    /// for brevity. A user's test framework would likely spin up and spin down the test-proxy process as the .NET team does.
    /// 
    /// Reference https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/core/Azure.Core.TestFramework/src/TestProxy.cs#L63 for the full implementation.
    /// </summary>
    public class SampleTestImplementation : SampleTestBaseClass
    {
        private HttpMessage CreateSampleRequest()
        {
            var message = RequestPipeline.CreateMessage();
            message.Request.Uri.Reset(new Uri("https://example.org"));
            return message;
        }

        [Test]
        public async Task BasicTest()
        {
            var message = CreateSampleRequest();

            await RequestPipeline.SendAsync(message, CancellationToken.None);

            var body = (new StreamReader(message.Response.ContentStream)).ReadToEnd();

            Assert.NotNull(body);
        }
    }
}
