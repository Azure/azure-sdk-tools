using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Storage.Blobs;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.HttpFaultInjector.StorageBlobsSample
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var connectionString = Environment.GetEnvironmentVariable("STORAGE_CONNECTION_STRING");

            var httpClientTransport = HttpClientTransport.Shared;

            // You must either trust the .NET developer certificate, or uncomment the following lines to disable SSL validation.
            // httpClientTransport = new HttpClientTransport(new System.Net.Http.HttpClient(new System.Net.Http.HttpClientHandler()
            // {
            //     ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            // }));

            var blobClientOptions = new BlobClientOptions
            {
                Transport = new FaultInjectionTransport(httpClientTransport, new Uri("https://localhost:7778"))
            };
            
            // Use a single fast retry instead of the default settings
            blobClientOptions.Retry.Mode = RetryMode.Fixed;
            blobClientOptions.Retry.Delay = TimeSpan.Zero;
            blobClientOptions.Retry.MaxRetries = 1;

            var blobClient = new BlobClient(connectionString, "sample", "sample.txt", blobClientOptions);

            Console.WriteLine("Sending request...");
            var response = await blobClient.DownloadAsync();
            var content = (new StreamReader(response.Value.Content)).ReadToEnd();
            Console.WriteLine($"Content: {content}");
        }

        class FaultInjectionTransport : HttpPipelineTransport
        {
            private readonly HttpPipelineTransport _transport;
            private readonly Uri _uri;

            public FaultInjectionTransport(HttpPipelineTransport transport, Uri uri)
            {
                _transport = transport;
                _uri = uri;
            }

            public override Request CreateRequest()
            {
                return _transport.CreateRequest();
            }

            public override void Process(HttpMessage message)
            {
                RedirectToFaultInjector(message);
                _transport.Process(message);
            }

            public override ValueTask ProcessAsync(HttpMessage message)
            {
                RedirectToFaultInjector(message);
                return _transport.ProcessAsync(message);
            }

            protected void RedirectToFaultInjector(HttpMessage message)
            {
                // Ensure X-Upstream-Host header is only set once, since the same HttpMessage will be reused on retries
                if (!message.Request.Headers.Contains("X-Upstream-Host"))
                {
                    message.Request.Headers.SetValue("X-Upstream-Host", $"{message.Request.Uri.Host}:{message.Request.Uri.Port}");
                }

                message.Request.Uri.Host = _uri.Host;
                message.Request.Uri.Port = _uri.Port;
            }
        }
    }
}
