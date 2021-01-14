using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HttpFaultInjectorClient
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var httpClient = new HttpClient(new FaultInjectionClientHandler("localhost", 7778));

            Console.WriteLine("Sending request...");
            var response = await httpClient.GetAsync("https://www.example.org");
            Console.WriteLine(response.StatusCode);
        }

        class FaultInjectionClientHandler : HttpClientHandler
        {
            private readonly string _host;
            private readonly int _port;

            public FaultInjectionClientHandler(string host, int port)
            {
                _host = host;
                _port = port;

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
                    Host = _host,
                    Port = _port
                };
                request.RequestUri = builder.Uri;

                return base.SendAsync(request, cancellationToken);
            }
        }
    }
}
