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
        private static readonly Uri _faultInjectorUri = new Uri("http://localhost:7777");

        static async Task Main(string[] args)
        {
            var connectionString = Environment.GetEnvironmentVariable("STORAGE_CONNECTION_STRING");

            var blobClientOptions = new BlobClientOptions();

            // When using an HTTPS _faultInjectorUri, you must either trust the .NET developer certificate,
            // or uncomment the following line to disable SSL validation.
            // blobClientOptions.Transport = new HttpClientTransport(new System.Net.Http.HttpClient(new System.Net.Http.HttpClientHandler()
            // {
            //     ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            // }));

            // FaultInjectionPolicy should be per-retry to run as late as possible in the pipeline.  For example, some
            // clients compute a request signature as a per-retry policy, and FaultInjectionPolicy should run after the
            // signature is computed to avoid altering the signature.
            blobClientOptions.AddPolicy(new FaultInjectionPolicy(_faultInjectorUri), HttpPipelinePosition.PerRetry);

            // The request can be redirected to the HttpFaultInjector using either a Transport or a Policy.
            // A Policy is recommended by default.
            // blobClientOptions.Transport = new FaultInjectionTransport(blobClientOptions.Transport, _faultInjectorUri);

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

        class FaultInjectionPolicy : HttpPipelinePolicy
        {
            private readonly Uri _uri;

            public FaultInjectionPolicy(Uri uri)
            {
                _uri = uri;
            }

            public override void Process(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline)
            {
                RedirectToFaultInjector(message);
                ProcessNext(message, pipeline);
            }

            public override ValueTask ProcessAsync(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline)
            {
                RedirectToFaultInjector(message);
                return ProcessNextAsync(message, pipeline);
            }

            protected void RedirectToFaultInjector(HttpMessage message)
            {
                // Ensure X-Upstream-Base-Uri header is only set once, since the same HttpMessage will be reused on retries
                if (!message.Request.Headers.Contains("X-Upstream-Base-Uri"))
                {
                    var upstreamBaseUriBuilder = new UriBuilder()
                    {
                        Scheme = message.Request.Uri.Scheme,
                        Host = message.Request.Uri.Host,
                        Port = message.Request.Uri.Port,
                    };

                    message.Request.Headers.SetValue("X-Upstream-Base-Uri", upstreamBaseUriBuilder.ToString());
                }

                message.Request.Uri.Scheme = _uri.Scheme;
                message.Request.Uri.Host = _uri.Host;
                message.Request.Uri.Port = _uri.Port;
            }
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
                // Ensure X-Upstream-Base-Uri header is only set once, since the same HttpMessage will be reused on retries
                if (!message.Request.Headers.Contains("X-Upstream-Base-Uri"))
                {
                    var upstreamBaseUriBuilder = new UriBuilder()
                    {
                        Scheme = message.Request.Uri.Scheme,
                        Host = message.Request.Uri.Host,
                        Port = message.Request.Uri.Port,
                    };

                    message.Request.Headers.SetValue("X-Upstream-Base-Uri", upstreamBaseUriBuilder.ToString());
                }

                message.Request.Uri.Scheme = _uri.Scheme;
                message.Request.Uri.Host = _uri.Host;
                message.Request.Uri.Port = _uri.Port;
            }
        }
    }
}
