// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.TestProxy
{
    public sealed class WebSocketProxyHandler
    {
        private static readonly string[] s_restrictedHeaders = new[]
        {
            "Connection",
            "Upgrade",
            "Host",
            "Proxy-Connection",
            "Sec-WebSocket-Accept",
            "Sec-WebSocket-Extensions",
            "Sec-WebSocket-Key",
            "Sec-WebSocket-Protocol",
            "Sec-WebSocket-Version",
        };

        private readonly ILogger _logger;

        public WebSocketProxyHandler(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<WebSocketProxyHandler>();
        }

        public async Task ProxyWebSocketAsync(HttpContext context)
        {
            var upstreamUri = RecordingHandler.GetWebSocketRequestUri(context.Request);
            using var clientSocket = new ClientWebSocket();

            ConfigureClientSocketOptions(clientSocket.Options, context.Request.Headers);

            try
            {
                await clientSocket.ConnectAsync(upstreamUri, context.RequestAborted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to upstream WebSocket {UpstreamUri}", upstreamUri);
                context.Response.StatusCode = StatusCodes.Status502BadGateway;
                await context.Response.WriteAsync("Failed to connect to upstream WebSocket.");
                return;
            }

            using var serverSocket = await context.WebSockets.AcceptWebSocketAsync(clientSocket.SubProtocol);

            var relayTasks = new[]
            {
                RelayWebSocketAsync(serverSocket, clientSocket, context.RequestAborted),
                RelayWebSocketAsync(clientSocket, serverSocket, context.RequestAborted)
            };

            await Task.WhenAny(relayTasks);
            await CloseSocketsAsync(serverSocket, clientSocket);
            await Task.WhenAll(relayTasks);
        }

        private static void ConfigureClientSocketOptions(ClientWebSocketOptions options, IHeaderDictionary headers)
        {
            foreach (var header in headers)
            {
                if (header.Key.StartsWith("x-recording-", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (IsRestrictedHeader(header.Key))
                {
                    continue;
                }

                foreach (var value in header.Value)
                {
                    options.SetRequestHeader(header.Key, value);
                }
            }

            if (headers.TryGetValue("Sec-WebSocket-Protocol", out var protocols))
            {
                var values = protocols
                    .SelectMany(value => value.Split(','))
                    .Select(value => value.Trim())
                    .Where(value => value.Length > 0);

                foreach (var value in values)
                {
                    options.AddSubProtocol(value);
                }
            }
        }

        private static bool IsRestrictedHeader(string headerName)
        {
            return s_restrictedHeaders.Contains(headerName, StringComparer.OrdinalIgnoreCase);
        }

        private async Task RelayWebSocketAsync(WebSocket source, WebSocket destination, CancellationToken cancellationToken)
        {
            var buffer = new byte[16 * 1024];

            try
            {
                while (source.State == WebSocketState.Open && destination.State == WebSocketState.Open)
                {
                    var result = await source.ReceiveAsync(buffer, cancellationToken);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await destination.CloseAsync(
                            result.CloseStatus ?? WebSocketCloseStatus.NormalClosure,
                            result.CloseStatusDescription,
                            cancellationToken);
                        break;
                    }

                    await destination.SendAsync(
                        buffer.AsMemory(0, result.Count),
                        result.MessageType,
                        result.EndOfMessage,
                        cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (WebSocketException ex)
            {
                _logger.LogWarning(ex, "WebSocket relay terminated with exception.");
            }
        }

        private static async Task CloseSocketsAsync(WebSocket serverSocket, WebSocket clientSocket)
        {
            try
            {
                await CloseSocketAsync(serverSocket);
            }
            catch (Exception) { }
            try
            {
                await CloseSocketAsync(clientSocket);
            }
            catch (Exception) { }
        }

        private static async Task CloseSocketAsync(WebSocket socket)
        {
            if (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived)
            {
                await socket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Closing",
                    CancellationToken.None);
            }
        }
    }
}
