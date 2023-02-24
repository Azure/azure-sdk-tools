using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Core.Pipeline;

namespace Azure.Sdk.Tools.TestProxy.HttpPipelineSample
{
    /// <summary>
    /// This is the concrete implementation that a user would implement. A Helper class. An API Layer, etc.
    /// 
    /// It takes a single argument of type transport, meaning that at runtime in production, one could pass in a new HttpPipelineTransport, 
    /// versus TestProxyTransport during testing.
    /// </summary>
    public class MyImplementerClass
    {
        public HttpPipeline RequestPipeline { get; set; }

        public MyImplementerClass(HttpPipeline pipeline) {
            RequestPipeline = pipeline;
        }

        /// <summary>
        /// This is just a function that uses the passed HttpPipeline to send a request.
        /// 
        /// Check Program.cs to see a real usage.
        /// </summary>
        /// <param name="pipeline"></param>
        /// <returns></returns>
        public async Task<string> SendRequest(Uri uri)
        {
            Console.WriteLine("Request");

            var message = RequestPipeline.CreateMessage();
            message.Request.Uri.Reset(uri);

            await RequestPipeline.SendAsync(message, CancellationToken.None);
            var body = (new StreamReader(message.Response.ContentStream)).ReadToEnd();

            Console.WriteLine("Headers:");
            Console.WriteLine($"  Date: {message.Response.Headers.Date.Value.LocalDateTime}");
            Console.WriteLine($"Body: {(body.Replace("\r", string.Empty).Replace("\n", string.Empty).Substring(0, 80))}");
            Console.WriteLine();

            return body;
        }
    }
}
