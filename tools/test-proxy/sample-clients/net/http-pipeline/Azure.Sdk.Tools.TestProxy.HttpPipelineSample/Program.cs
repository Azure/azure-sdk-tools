using Azure.Core;
using Azure.Core.Pipeline;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.TestProxy.HttpPipelineSample
{
    /// <summary>
    /// This is a sample of using a "real" http pipeline passed to the implementing class. 
    /// Requests will go out as normal due to the HttpPipeline build with default configurations.
    /// 
    /// Check SampleTestImplementation.cs in the accompanying test project for an example of passing in a
    /// pipeline that has been modified to redirect to the test-proxy.
    /// </summary>
    class Program
    {
        public class ProgramClientOptions : ClientOptions { }

        // in production (or Startup.cs in asp.net projects) users will provide a standard HttpClient,
        // not one with overriden transport (like we do in SampleTestBaseClass when creating the RequestPipeline on line 106.
        static async Task Main(string[] args)
        {
            var pipeline = HttpPipelineBuilder.Build(new ProgramClientOptions());

            var concreteImplementation = new MyImplementerClass(pipeline);

            await concreteImplementation.SendRequest(new Uri("https://example.org"));
        }
    }
}
