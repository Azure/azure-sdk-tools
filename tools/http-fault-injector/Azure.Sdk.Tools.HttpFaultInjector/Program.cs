using CommandLine;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.HttpFaultInjector
{
    public static class Program
    {
        private static HttpClient _httpClient;

        private static readonly List<(string Option, string Description)> _selectionDescriptions = new List<(string Option, string Description)>()
        {
            ("f", "Full response"),
            ("p", "Partial Response (full headers, 50% of body), then wait indefinitely"),
            ("pc", "Partial Response (full headers, 50% of body), then close (TCP FIN)"),
            ("pa", "Partial Response (full headers, 50% of body), then abort (TCP RST)"),
            ("pn", "Partial Response (full headers, 50% of body), then finish normally"),
            ("n", "No response, then wait indefinitely"),
            ("nc", "No response, then close (TCP FIN)"),
            ("na", "No response, then abort (TCP RST)")
        };

        private static readonly string[] _excludedRequestHeaders = new string[] {
            // Only applies to request between client and proxy
            "Proxy-Connection",

            // "X-Upstream-Base-Uri" in original request is used as the Base URI in the upstream request
            "X-Upstream-Base-Uri",
            "Host",

            _responseSelectionHeader
        };

        // Headers which must be set on HttpContent instead of HttpRequestMessage
        private static readonly string[] _contentRequestHeaders = new string[] {
            "Content-Length",
            "Content-Type",
        };

        private const string _responseSelectionHeader = "x-ms-faultinjector-response-option";

        private class Options
        {
            [Option('i', "insecure", Default = false, HelpText = "Allow insecure upstream SSL certs")]
            public bool Insecure { get; set; }

            [Option('t', "keep-alive-timeout", Default = 120, HelpText = "Keep-alive timeout (in seconds)")]
            public int KeepAliveTimeout { get; set; }
        }

        public static void Main(string[] args)
        {
            var parser = new Parser(settings =>
            {
                settings.CaseSensitive = false;
                settings.HelpWriter = Console.Error;
            });

            parser.ParseArguments<Options>(args).WithParsed(options => Run(options));
        }

        private static void Run(Options options)
        {
            TimeSpan keepAlive = TimeSpan.FromSeconds(options.KeepAliveTimeout);
            if (options.Insecure)
            {
                _httpClient = new HttpClient(new HttpClientHandler()
                {
                    // Allow insecure SSL certs
                    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
                });
            }
            else
            {
                _httpClient = new HttpClient();
            }

            // TODO: we can switch to SocketsHttpHandler and configure read/write/connect timeouts separately
            // for now let's just set upstream timeout to be slightly bigger than client timeout.
            _httpClient.Timeout = keepAlive + TimeSpan.FromSeconds(1);
            new WebHostBuilder()
                .UseKestrel(kestrelOptions =>
                {
                    kestrelOptions.Listen(IPAddress.Any, 7777);
                    kestrelOptions.Listen(IPAddress.Any, 7778, listenOptions =>
                    {
                        listenOptions.UseHttps();
                    });
                    kestrelOptions.Limits.KeepAliveTimeout = keepAlive;
                })
                .Configure(app => app.Run(async context =>
                {
                    try
                    {
                        using var upstreamResponse = await SendUpstreamRequest(context.Request, context.RequestAborted);

                        // Attempt to remove the response selection header and use its value to handle the response selection.
                        if (context.Request.Headers.Remove(_responseSelectionHeader, out var selection))
                        {
                            string optionDescription = _selectionDescriptions.FirstOrDefault(kvp => kvp.Option.Equals(selection)).Description;
                            if (string.IsNullOrEmpty(optionDescription))
                            {
                                Console.WriteLine($"Invalid {_responseSelectionHeader} value {selection}.");
                            }
                            else if (await TryHandleResponseOption(selection, context, upstreamResponse))
                            {
                                Console.WriteLine($"Using response option {optionDescription} from header value.");
                                return;
                            }
                        }

                        // If we were passed an invalid response selection header, or none, continue prompting the user for input and attempt to handle the response.
                        while (true)
                        {
                            Console.WriteLine();

                            Console.WriteLine("Select a response then press ENTER:");
                            foreach (var selectionDescription in _selectionDescriptions)
                            {
                                Console.WriteLine($"{selectionDescription.Option}: {selectionDescription.Description}");
                            }

                            Console.WriteLine();

                            selection = Console.ReadLine();

                            if (await TryHandleResponseOption(selection, context, upstreamResponse))
                            {
                                return;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                        throw;
                    }
                }))
                .Build()
                .Run();
        }

        private static async Task<UpstreamResponse> SendUpstreamRequest(HttpRequest request, CancellationToken cancellationToken)
        {
            Console.WriteLine();
            Log("Incoming Request");

            var incomingUriBuilder = new UriBuilder() {
                Scheme = request.Scheme,
                Host = request.Host.Host,
                Path = request.Path.Value,
                Query = request.QueryString.Value,
            };
            if (request.Host.Port.HasValue)
            {
                incomingUriBuilder.Port = request.Host.Port.Value;
            }
            var incomingUri = incomingUriBuilder.Uri;

            Log($"URL: {incomingUri}");

            Log("Headers:");
            foreach (var header in request.Headers)
            {
                Log($"  {header.Key}:{header.Value}");
            }

            var upstreamUriBuilder = new UriBuilder(request.Headers["X-Upstream-Base-Uri"])
            {
                Path = request.Path.Value,
                Query = request.QueryString.Value,
            };

            var upstreamUri = upstreamUriBuilder.Uri;

            Console.WriteLine();
            Log("Upstream Request");
            Log($"URL: {upstreamUri}");

            using var upstreamRequest = new HttpRequestMessage(new HttpMethod(request.Method), upstreamUri);
            Log("Headers:");

            if (request.ContentLength > 0)
            {
                upstreamRequest.Content = new StreamContent(request.Body);

                foreach (var header in request.Headers.Where(h => _contentRequestHeaders.Contains(h.Key)))
                {
                    Log($"  {header.Key}:{header.Value.First()}");
                    upstreamRequest.Content.Headers.Add(header.Key, values: header.Value);
                }
            }

            foreach (var header in request.Headers.Where(h => !_excludedRequestHeaders.Contains(h.Key) && !_contentRequestHeaders.Contains(h.Key)))
            {
                Log($"  {header.Key}:{header.Value.First()}");
                if (!upstreamRequest.Headers.TryAddWithoutValidation(header.Key, values: header.Value))
                {
                    throw new InvalidOperationException($"Could not add header {header.Key} with value {header.Value}");
                }
            }

            Log("Sending request to upstream server...");
            var upstreamResponseMessage = await _httpClient.SendAsync(upstreamRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            Console.WriteLine();
            Log("Upstream Response");
            var headers = new List<KeyValuePair<string, StringValues>>();

            Log($"StatusCode: {upstreamResponseMessage.StatusCode}");

            Log("Headers:");

            foreach (var header in upstreamResponseMessage.Headers)
            {
                Log($"  {header.Key}:{header.Value.First()}");

                // Must skip "Transfer-Encoding" header, since if it's set manually Kestrel requires you to implement
                // your own chunking.
                if (string.Equals(header.Key, "Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                headers.Add(new KeyValuePair<string, StringValues>(header.Key, header.Value.ToArray()));
            }

            foreach (var header in upstreamResponseMessage.Content.Headers)
            {
                Log($"  {header.Key}:{header.Value.First()}");
                headers.Add(new KeyValuePair<string, StringValues>(header.Key, header.Value.ToArray()));
            }

            Log($"ContentLength: {upstreamResponseMessage.Content.Headers.ContentLength}, is chunked: {upstreamResponseMessage.Headers.TransferEncodingChunked}");

            var response = await UpstreamResponse.FromContent(upstreamResponseMessage.Content, cancellationToken);
            response.StatusCode = (int)upstreamResponseMessage.StatusCode;
            response.Headers = headers.ToArray();

            return response;
        }

        private static async Task<bool> TryHandleResponseOption(string selection, HttpContext context, UpstreamResponse upstreamResponse)
        {
            switch (selection)
            {
                case "f":
                    // Full response
                    await SendDownstreamResponse(upstreamResponse, context.Response, upstreamResponse.ContentLength, context.RequestAborted);
                    return true;
                case "p":
                    // Partial Response (full headers, 50% of body), then wait indefinitely
                    await SendDownstreamResponse(upstreamResponse, context.Response, upstreamResponse.ContentLength / 2, context.RequestAborted);
                    await Task.Delay(Timeout.InfiniteTimeSpan, context.RequestAborted);
                    return true;
                case "pc":
                    // Partial Response (full headers, 50% of body), then close (TCP FIN)
                    await SendDownstreamResponse(upstreamResponse, context.Response, upstreamResponse.ContentLength / 2, context.RequestAborted);
                    Close(context);
                    return true;
                case "pa":
                    // Partial Response (full headers, 50% of body), then abort (TCP RST)
                    await SendDownstreamResponse(upstreamResponse, context.Response, upstreamResponse.ContentLength / 2, context.RequestAborted);
                    Abort(context);
                    return true;
                case "pn":
                    // Partial Response (full headers, 50% of body), then finish normally
                    await SendDownstreamResponse(upstreamResponse, context.Response, upstreamResponse.ContentLength / 2, context.RequestAborted);
                    return true;
                case "n":
                    // No response, then wait indefinitely
                    await Task.Delay(Timeout.InfiniteTimeSpan, context.RequestAborted);
                    return true;
                case "nc":
                    // No response, then close (TCP FIN)
                    Close(context);
                    return true;
                case "na":
                    // No response, then abort (TCP RST)
                    Abort(context);
                    return true;
                default:
                    Console.WriteLine($"Invalid selection: {selection}");
                    return false;
            }
        }

        private static async Task SendDownstreamResponse(UpstreamResponse upstreamResponse, HttpResponse response, long? contentBytes, CancellationToken cancellationToken)
        {
            Console.WriteLine();

            Log("Sending downstream response...");

            response.StatusCode = upstreamResponse.StatusCode;

            Log($"StatusCode: {upstreamResponse.StatusCode}");

            Log("Headers:");
            foreach (var header in upstreamResponse.Headers)
            {
                Log($"  {header.Key}:{header.Value}");
                response.Headers.Add(header.Key, header.Value);
            }

            long count = contentBytes ?? long.MaxValue;

            Log($"Writing response body of {count} bytes...");

            using Stream source = await upstreamResponse.GetContentStreamAsync(cancellationToken);
            byte[] buffer = new byte[8192];

            try
            {
                for (long remaining = count; remaining > 0;)
                {
                    int read = await source.ReadAsync(buffer, 0, (int)Math.Min(buffer.Length, remaining), cancellationToken);
                    if (read <= 0)
                    {
                        break;
                    }

                    remaining -= read;
                    await response.Body.WriteAsync(buffer, 0, read, cancellationToken);
                }

                await response.Body.FlushAsync();
                Log($"Finished writing response body");
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
            }
            finally
            {
                // disponse content as early as possible (before infinite wait that might happen later)
                // so that underlying connection returns to connection pool
                // and we won't run out of them
                upstreamResponse.Dispose();
            }
        }

        // Close the TCP connection by sending FIN
        private static void Close(HttpContext context)
        {
            context.Abort();
        }

        // Abort the TCP connection by sending RST
        private static void Abort(HttpContext context)
        {
            // SocketConnection registered "this" as the IConnectionIdFeature among other things.
            var socketConnection = context.Features.Get<IConnectionIdFeature>();
            var socket = (Socket)socketConnection.GetType().GetField("_socket", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(socketConnection);
            socket.LingerState = new LingerOption(true, 0);
            socket.Dispose();
        }

        private static void Log(object value)
        {
            Console.WriteLine($"[{DateTime.Now:hh:mm:ss.fff}] {value}");
        }

        private class UpstreamResponse : IDisposable
        {
            private readonly HttpContent _content = null;
            private readonly Stream _contentStream = null;
            private UpstreamResponse(HttpContent content)
            {
                _content = content;
                ContentLength = _content.Headers.ContentLength ?? throw new ArgumentNullException("ContentLength must not be null.");
            }

            private UpstreamResponse(MemoryStream contentStream)
            {
                _contentStream = contentStream;
                ContentLength = contentStream.Length;
            }

            public int StatusCode { get; set; }
            public KeyValuePair<string, StringValues>[] Headers { get; set; }
            public async Task<Stream> GetContentStreamAsync(CancellationToken cancellationToken)
            {
                return _contentStream != null ? _contentStream : await _content.ReadAsStreamAsync(cancellationToken);
            }

            public long ContentLength { get; }

            public void Dispose()
            {
                _content?.Dispose();
                _contentStream?.Dispose();
            }

            public static async Task<UpstreamResponse> FromContent(HttpContent content, CancellationToken cancellationToken)
            {
                if (content.Headers.ContentLength == null)
                {
                    MemoryStream contentStream = await BufferContentAsync(content, cancellationToken);
                    // we no longer need that content and can let the connection go back to the pool.
                    content.Dispose();
                    return new UpstreamResponse(contentStream);
                }
                
                return new UpstreamResponse(content);
            }

            private static async Task<MemoryStream> BufferContentAsync(HttpContent content, CancellationToken cancellationToken)
            {
                Debug.Assert(content.Headers.ContentLength == null, "We should not buffer content if length is available.");

                Log("Response does not have content length (is chunked or malformed) and is being buffered");
                byte[] contentBytes = await content.ReadAsByteArrayAsync(cancellationToken);
                Log($"Content was buffered, total length - {contentBytes.Length}.");

                // comparing to buffering full content memory stream allocation is not such a big deal.
                return new MemoryStream(contentBytes);
            }
        }
    }
}
