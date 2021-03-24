using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Storage.Blobs;
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.HttpFaultInjector.StorageBlobsSample
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var connectionString = Environment.GetEnvironmentVariable("STORAGE_CONNECTION_STRING");

            var httpClient = new HttpClient(new HttpClientHandler()
            {
                // Allow insecure SSL certs
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            });

            var blobClient = new BlobClient(connectionString, "sample", "sample.txt", new BlobClientOptions
            {
                Transport = new FaultInjectionTransport(new HttpClientTransport(httpClient), new Uri("https://localhost:7778"))
            });

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
                // Set "Host" header to upstream host:port
                message.Request.Headers.Add("Host", $"{message.Request.Uri.Host}:{message.Request.Uri.Port}");

                message.Request.Uri.Host = _uri.Host;
                message.Request.Uri.Port = _uri.Port;
            }
        }
    }
}
