using NUnit.Framework;
using System;
using System.IO;
using System.Threading;
using Azure.Core;
using System.Threading.Tasks;
using Azure.Sdk.Tools.TestProxy.HttpPipelineSample;

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

        [Test] // we decorate with [Test] so that the requisite Test-Proxy start/stop post commands are fired (to get a recording id)
        public async Task BasicTest()
        {
            MyImplementerClass classForTesting = new MyImplementerClass(RequestPipeline);
            var recordedResult = await classForTesting.SendRequest(new Uri("https://example.org"));
            
            Assert.IsNotNull(recordedResult);
        }
    }
}
