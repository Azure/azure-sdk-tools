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
            var httpClient = new HttpClient(new FaultInjectionClientHandler(new Uri("http://localhost:7777")));

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

                // When using an HTTPS Uri, you must either trust the .NET developer certificate,
                // or uncomment the following line to disable SSL validation.
                // ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                // Set "X-Upstream-Base-Uri" header to upstream scheme://host:port
                var upstreamBaseUriBuilder = new UriBuilder()
                {
                    Scheme = request.RequestUri.Scheme,
                    Host = request.RequestUri.Host,
                    Port = request.RequestUri.Port,
                };
                request.Headers.Add("X-Upstream-Base-Uri", upstreamBaseUriBuilder.ToString());

                // Set URI to fault injector
                var faultInjectorUriBuilder = new UriBuilder(request.RequestUri)
                {
                    Scheme = _uri.Scheme,
                    Host = _uri.Host,
                    Port = _uri.Port,
                };
                request.RequestUri = faultInjectorUriBuilder.Uri;

                return base.SendAsync(request, cancellationToken);
            }
        }
    }
}
