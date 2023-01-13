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
    class Program
    {
        public class ProgramClientOptions : ClientOptions { }

        // in production (or Startup.cs in asp.net projects) users will provide a standard HttpClient,
        // not one with overriden transport (like we do in SampleTestImplementation when creating the RequestPipeline on line 
        static async Task Main(string[] args)
        {
            var pipeline = HttpPipelineBuilder.Build(new ProgramClientOptions());

            var concreteImplementation = new MyImplementerClass(pipeline);

            await concreteImplementation.SendRequest(new Uri("https://example.org"));
        }
    }
}
