using System.Threading.Tasks;
using System.Threading;
using Microsoft.Extensions.Logging;
using System;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Linq;
using System.IO;
using System.Buffers;
using System.Diagnostics;

namespace Azure.Sdk.Tools.HttpFaultInjector
{
    public class FaultInjectingMiddleware
    {
        private readonly ILogger<FaultInjectingMiddleware> _logger;
        private readonly HttpClient _httpClient;
        public FaultInjectingMiddleware(RequestDelegate _, IHttpClientFactory httpClientFactory, ILogger<FaultInjectingMiddleware> logger)
        {
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient("upstream");
        }

        public async Task InvokeAsync(HttpContext context)
        {
            string faultHeaderValue = context.Request.Headers[Utils.ResponseSelectionHeader];
            string upstreamBaseUri = context.Request.Headers[Utils.UpstreamBaseUriHeader];

            if (ValidateOrReadFaultMode(faultHeaderValue, out var fault))
            {
                await ProxyResponse(context, upstreamBaseUri, fault, context.RequestAborted);
            }
            else
            {
                context.Response.StatusCode = 400;
            }
        }


        private async Task<UpstreamResponse> SendUpstreamRequest(HttpRequest request, string uri, CancellationToken cancellationToken)
        {
            var incomingUriBuilder = new UriBuilder()
            {
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

            var upstreamUriBuilder = new UriBuilder(uri)
            {
                Path = request.Path.Value,
                Query = request.QueryString.Value,
            };

            var upstreamUri = upstreamUriBuilder.Uri;

            using (var upstreamRequest = new HttpRequestMessage(new HttpMethod(request.Method), upstreamUri))
            {
                if (Utils.HasBody(request))
                { 
                    upstreamRequest.Content = new StreamContent(request.Body);
                    foreach (var header in request.Headers.Where(h => Utils.ContentRequestHeaders.Contains(h.Key)))
                    {
                        upstreamRequest.Content.Headers.Add(header.Key, values: header.Value);
                    }
                }

                foreach (var header in request.Headers.Where(h => !Utils.ExcludedRequestHeaders.Contains(h.Key) && !Utils.ContentRequestHeaders.Contains(h.Key)))
                {
                    if (!upstreamRequest.Headers.TryAddWithoutValidation(header.Key, values: header.Value))
                    {
                        throw new InvalidOperationException($"Could not add header {header.Key} with value {header.Value}");
                    }
                }

                var upstreamResponseMessage = await _httpClient.SendAsync(upstreamRequest);
                var headers = new List<KeyValuePair<string, IEnumerable<string>>>();
                // Must skip "Transfer-Encoding" header, since if it's set manually Kestrel requires you to implement
                // your own chunking.
                headers.AddRange(upstreamResponseMessage.Headers.Where(header => !string.Equals(header.Key, "Transfer-Encoding", StringComparison.OrdinalIgnoreCase)));
                headers.AddRange(upstreamResponseMessage.Content.Headers);

                var upstreamResponse = await UpstreamResponseFromHttpContent(upstreamResponseMessage.Content, cancellationToken);
                upstreamResponse.StatusCode = (int)upstreamResponseMessage.StatusCode;
                upstreamResponse.Headers = headers.Select(h => new KeyValuePair<string, StringValues>(h.Key, h.Value.ToArray()));

                return upstreamResponse;
            }
        }


        private async Task<UpstreamResponse> UpstreamResponseFromHttpContent(HttpContent content, CancellationToken cancellationToken)
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

        private async Task<MemoryStream> BufferContentAsync(HttpContent content, CancellationToken cancellationToken)
        {
            Debug.Assert(content.Headers.ContentLength == null, "We should not buffer content if length is available.");

            _logger.LogWarning("Response does not have content length (is chunked or malformed) and is being buffered");
            byte[] contentBytes = await content.ReadAsByteArrayAsync(cancellationToken);
            _logger.LogInformation("Finished buffering response body ({length})", contentBytes.Length);
            return new MemoryStream(contentBytes);
        }

        private async Task ProxyResponse(HttpContext context, string upstreamUri, string fault, CancellationToken cancellationToken)
        {
            switch (fault)
            {
                case "nq":
                    // No request body, then wait indefinitely
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                    return;
                case "nqc":
                    // No request body, then close (TCP FIN)
                    Close(context);
                    return;
                case "nqa":
                    // No request body, then abort (TCP RST)
                    Abort(context);
                    return;
                case "pq":
                    // Partial request (50% of body), then wait indefinitely
                    await ReadPartialRequest(context.Request, cancellationToken);
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                    return;
                case "pqc":
                    // Partial request (50% of body), then close (TCP FIN)
                    await ReadPartialRequest(context.Request, cancellationToken);
                    Close(context);
                    return;
                case "pqa":
                    // Partial request (50% of body), then abort (TCP RST)
                    await ReadPartialRequest(context.Request, cancellationToken);
                    Abort(context);
                    return;
                default:
                    // Fall through and read full request body
                    break;
            }

            UpstreamResponse upstreamResponse = await SendUpstreamRequest(context.Request, upstreamUri, cancellationToken);

            switch (fault)
            {
                case "f":
                    // Full response
                    await SendDownstreamResponse(context.Response, upstreamResponse, upstreamResponse.ContentLength, cancellationToken);
                    return;
                case "p":
                    // Partial Response (full headers, 50% of body), then wait indefinitely
                    await SendDownstreamResponse(context.Response, upstreamResponse, upstreamResponse.ContentLength / 2, cancellationToken);
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                    return;
                case "pc":
                    // Partial Response (full headers, 50% of body), then close (TCP FIN)
                    await SendDownstreamResponse(context.Response, upstreamResponse, upstreamResponse.ContentLength / 2, cancellationToken);
                    Close(context);
                    return;
                case "pa":
                    // Partial Response (full headers, 50% of body), then abort (TCP RST)
                    await SendDownstreamResponse(context.Response, upstreamResponse, upstreamResponse.ContentLength / 2, cancellationToken);
                    Abort(context);
                    return;
                case "pn":
                    // Partial Response (full headers, 50% of body), then finish normally
                    await SendDownstreamResponse(context.Response, upstreamResponse, upstreamResponse.ContentLength / 2, cancellationToken);
                    return;
                case "n":
                    // No response, then wait indefinitely
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                    return;
                case "nc":
                    // No response, then close (TCP FIN)
                    Close(context);
                    return;
                case "na":
                    // No response, then abort (TCP RST)
                    Abort(context);
                    return;
                default:
                    // can't really happen since we validated options before calling into this method.
                    throw new ArgumentException($"Invalid fault mode: {fault}", nameof(fault));
            }
        }

        private static async Task ReadPartialRequest(HttpRequest request, CancellationToken cancellationToken)
        {
            var contentLength = request.ContentLength
                ?? throw new InvalidOperationException("Partial request options require content-length request headers");
            var bytesToRead = contentLength / 2;
            long totalBytesRead = 0;
            var buffer = ArrayPool<byte>.Shared.Rent(81920);
            try
            {
                while (true)
                {
                    var bytesRead = await request.Body.ReadAsync(
                        buffer,
                        0,
                        (int)Math.Min(buffer.Length, bytesToRead - totalBytesRead),
                        cancellationToken
                    );
                    totalBytesRead += bytesRead;
                    if (totalBytesRead >= bytesToRead || bytesRead == 0)
                    {
                        break;
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private async Task SendDownstreamResponse(HttpResponse response, UpstreamResponse upstreamResponse, long contentBytes, CancellationToken cancellationToken)
        {
            response.StatusCode = upstreamResponse.StatusCode;
            foreach (var header in upstreamResponse.Headers)
            {
                response.Headers.Append(header.Key, header.Value);
            }

            _logger.LogInformation("Started writing response body, {actualLength}", contentBytes);

            byte[] buffer = ArrayPool<byte>.Shared.Rent(81920);

            try
            {
                using Stream source = await upstreamResponse.GetContentStreamAsync(cancellationToken);
                for (long remaining = contentBytes; remaining > 0;)
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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Can't write response body");
            }
            finally
            {
                // disponse content as early as possible (before infinite wait that might happen later)
                // so that underlying connection returns to connection pool
                // and we won't run out of them
                upstreamResponse.Dispose();
                ArrayPool<byte>.Shared.Return(buffer);
                _logger.LogInformation("Finished writing response body");
            }
        }

        // Close the TCP connection by sending FIN
        private void Close(HttpContext context)
        {
            context.Abort();
        }

        // Abort the TCP connection by sending RST
        private void Abort(HttpContext context)
        {
            // SocketConnection registered "this" as the IConnectionIdFeature among other things.
            var socketConnection = context.Features.Get<IConnectionIdFeature>();
            var socket = (Socket)socketConnection.GetType().GetField("_socket", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(socketConnection);
            socket.LingerState = new LingerOption(true, 0);
            socket.Dispose();
        }

        private bool ValidateOrReadFaultMode(string headerValue, out string fault)
        {
            fault = headerValue ?? Utils.ReadSelectionFromConsole();
            if (!Utils.FaultModes.TryGetValue(fault, out var description))
            {
                _logger.LogError("Unknown {ResponseSelectionHeader} value - {fault}.", Utils.ResponseSelectionHeader, fault);
                return false;
            }

            _logger.LogInformation("Using response option '{description}' from header value.", description);
            return true;
        }
    }
}
