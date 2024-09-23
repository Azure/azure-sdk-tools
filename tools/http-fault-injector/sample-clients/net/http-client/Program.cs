using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.HttpFaultInjector.HttpClientSample
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var directClient = new HttpClient();
            var faultInjectionClient = new HttpClient(new FaultInjectionClientHandler(new Uri("http://localhost:7777")))
            {
                // Short timeout for testing no response
                Timeout = TimeSpan.FromSeconds(10)
            };

            Console.WriteLine("Sending request directly...");
            await Test(directClient);

            Console.WriteLine("Sending request through fault injector...");
            await Test(faultInjectionClient);
        }

        private static async Task Test(HttpClient client)
        {
            var baseUrl = "http://localhost:5000";

            var uploadStream = new LoggingStream(new MemoryStream(Encoding.UTF8.GetBytes(new string('a', 10 * 1024 * 1024))));

            var response = await client.PutAsync(baseUrl + "/upload", new StreamContent(uploadStream));

            var content = await response.Content.ReadAsStringAsync();
            var shortContent = (content.Length <= 40 ? content : content.Substring(0, 40) + "...");

            Console.WriteLine($"Status: {response.StatusCode}");
            Console.WriteLine($"Content: {shortContent}");
            Console.WriteLine($"Length: {content.Length}");
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

        class LoggingStream : Stream
        {
            private readonly Stream _stream;
            private long _totalBytesRead;

            public LoggingStream(Stream stream)
            {
                _stream = stream;
            }

            public override bool CanRead => _stream.CanRead;

            public override bool CanSeek => _stream.CanSeek;

            public override bool CanWrite => _stream.CanWrite;

            public override long Length => _stream.Length;

            public override long Position
            {
                get => _stream.Position;
                set => _stream.Position = value;
            }

            public override void Flush()
            {
                _stream.Flush();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                var bytesRead = _stream.Read(buffer, offset, count);
                _totalBytesRead += bytesRead;
                Console.WriteLine($"Read(buffer: byte[{buffer.Length}], offset: {offset}, count: {count}) => {bytesRead} (total {_totalBytesRead})");
                return bytesRead;
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                return _stream.Seek(offset, origin);
            }

            public override void SetLength(long value)
            {
                _stream.SetLength(value);
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                _stream.Write(buffer, offset, count);
            }
        }
    }
}
