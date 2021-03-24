// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Sdk.Tools.TestProxy.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.TestProxy
{
    // !! TODO: Lots of Record/Playback duplication
    // !! TODO: Replace statics with dependency injection, expiration policy of some kind
    [ApiController]
    [Route("[controller]/[action]")]
    public sealed class Record : ControllerBase
    {
        private readonly InMemorySessionManager _sessionManager;

        private static readonly string[] s_excludedRequestHeaders = new string[] {
            // Only applies to request between client and proxy
            "Proxy-Connection",
        };

        // Headers which must be set on HttpContent instead of HttpRequestMessage
        private static readonly string[] s_contentRequestHeaders = new string[] {
            "Content-Length",
            "Content-Type",
        };

        private static readonly HttpClient s_client = new HttpClient();

        public Record(InMemorySessionManager sessionManager) => _sessionManager = sessionManager;

        [HttpPost]
        public void Start()
        {
            var id = this._sessionManager.StartRecording();


            Response.Headers.Add("x-recording-id", id);
        }

        [HttpPost]
        public void Stop()
        {
            string id = GetHeader(Request, "x-recording-id");

            this._sessionManager.StopRecording(id);

            bool save = bool.Parse(GetHeader(Request, "x-recording-save"));
            if (save)
            {
                this._sessionManager.SaveSessions();
            }
        }

        public async Task HandleRequest()
        {
            string id = GetHeader(Request, "x-recording-id");

            var entry = await Playback.CreateEntryAsync(Request).ConfigureAwait(false);
            var upstreamRequest = CreateUpstreamRequest(Request, entry.Request.Body);
            var upstreamResponse = await s_client.SendAsync(upstreamRequest).ConfigureAwait(false);

            var body = await upstreamResponse.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            entry.Response.Body = body.Length == 0 ? null : body;
            entry.StatusCode = (int)upstreamResponse.StatusCode;

            if (!upstreamRequest.RequestUri.ToString().Contains("login.microsoft")) {
                Console.WriteLine($"Recorded a request to \u001b[32m{upstreamRequest.RequestUri}\u001b[0m");
                this._sessionManager.UpdateRecording(id, entry);
            }
            else
            {
                Console.WriteLine($"\u001b[33mPassthrough request {upstreamRequest.RequestUri}\u001b[0m");
            }

            Response.StatusCode = (int)upstreamResponse.StatusCode;
            foreach (var header in upstreamResponse.Headers)
            {
                var values = new StringValues(header.Value.ToArray());
                Response.Headers.Add(header.Key, values);
                entry.Response.Headers.Add(header.Key, values);
            }

            

            Response.Headers.Remove("Transfer-Encoding");

            if (entry.Response.Body?.Length > 0)
            {
                Response.ContentLength = entry.Response.Body.Length;
                await Response.Body.WriteAsync(entry.Response.Body).ConfigureAwait(false);
            }
        }

        private HttpRequestMessage CreateUpstreamRequest(HttpRequest incomingRequest, byte[] incomingBody)
        {
            var upstreamRequest = new HttpRequestMessage();
            upstreamRequest.RequestUri = Playback.GetRequestUri(incomingRequest);
            upstreamRequest.Method = new HttpMethod(incomingRequest.Method);
            upstreamRequest.Content = new ReadOnlyMemoryContent(incomingBody);

            foreach (var header in incomingRequest.Headers)
            {
                IEnumerable<string> values = header.Value;

                if (s_excludedRequestHeaders.Contains(header.Key, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    if (s_contentRequestHeaders.Contains(header.Key, StringComparer.OrdinalIgnoreCase))
                    {
                        upstreamRequest.Content.Headers.Add(header.Key, values);
                    }
                    else
                    {
                        upstreamRequest.Headers.Add(header.Key, values);
                    }
                }
                catch (FormatException)
                {
                    // ignore
                }
            }

            upstreamRequest.Headers.Host = upstreamRequest.RequestUri.Host;
            return upstreamRequest;
        }

        private static string GetHeader(HttpRequest request, string name)
        {
            if (!request.Headers.TryGetValue(name, out var value))
            {
                throw new InvalidOperationException("Missing header: " + name);
            }

            return value;
        }
    }
}
