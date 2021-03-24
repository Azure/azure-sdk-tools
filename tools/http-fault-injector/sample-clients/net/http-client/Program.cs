using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.HttpFaultInjector.HttpClientSample
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var httpClient = new HttpClient(new FaultInjectionClientHandler(new Uri("https://localhost:7778")));

            Console.WriteLine("Sending request...");
            var response = await httpClient.GetAsync("https://www.example.org");
            Console.WriteLine(response.StatusCode);
        }

        class FaultInjectionClientHandler : HttpClientHandler
        {
            private readonly Uri _uri;

            public FaultInjectionClientHandler(Uri uri)
            {
                _uri = uri;

                // Allow insecure SSL certs
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                // Set "Host" header to upstream host:port
                request.Headers.Add("Host", $"{request.RequestUri.Host}:{request.RequestUri.Port}");

                // Set URI to fault injector
                var builder = new UriBuilder(request.RequestUri)
                {
                    Scheme = _uri.Scheme,
                    Host = _uri.Host,
                    Port = _uri.Port,
                };
                request.RequestUri = builder.Uri;

                return base.SendAsync(request, cancellationToken);
            }
        }
    }
}
