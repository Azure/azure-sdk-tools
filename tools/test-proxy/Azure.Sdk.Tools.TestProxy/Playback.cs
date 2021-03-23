// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Core;
using Azure.Sdk.Tools.TestProxy.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.TestProxy
{
    // !! TODO: Lots of Record/Playback duplication
    // !! TODO: Replace statics with dependency injection, expiration policy of some kind
    [ApiController]
    [Route("[controller]/[action]")]
    public sealed class Playback : ControllerBase
    {
        private readonly InMemorySessionManager _sessionManager;
        public Playback(InMemorySessionManager sessionManager) => _sessionManager = sessionManager;
      

        // !! Neither matching nor sanitization can be customized yet.
        private static readonly RecordMatcher s_matcher = new RecordMatcher();
        private static readonly RecordedTestSanitizer s_sanitizer = new RecordedTestSanitizer();

        [HttpPost]
        public async Task Start()
        {
        }

        [HttpPost]
        public void Stop()
        {
        }

        public async Task HandleRequest()
        {
            string id = GetHeader(Request, "x-recording-id");


            var session = _sessionManager.GetRecording(id);
            var entry = await CreateEntryAsync(Request).ConfigureAwait(false);
            try
            {
                var match = session.NoneDestructiveLookup(entry, s_matcher, s_sanitizer);


                Response.StatusCode = match.StatusCode;

                foreach (var header in match.Response.Headers)
                {
                    Response.Headers.Add(header.Key, header.Value.ToArray());
                }

                if (Request.Headers.TryGetValue("x-ms-client-id", out var clientId))
                {
                    Response.Headers.Add("x-ms-client-id", clientId);
                }

                // Storage Blobs requires "x-ms-client-request-id" header in request and response to match
                if (Request.Headers.TryGetValue("x-ms-client-request-id", out var clientRequestId))
                {
                    Response.Headers["x-ms-client-request-id"] = clientRequestId;
                }

                Response.Headers.Remove("Transfer-Encoding");

                if (match.Response.Body?.Length > 0)
                {

                    Console.WriteLine($"Playing back a request for {entry.RequestUri}");

                    Response.ContentLength = match.Response.Body.Length;
                    await Response.Body.WriteAsync(match.Response.Body).ConfigureAwait(false);
                }
            }
            catch(Exception e)
            {
                // nothing
            }
        }

        public static async Task<RecordEntry> CreateEntryAsync(HttpRequest request)
        {
            var entry = new RecordEntry();
            entry.RequestUri = GetRequestUri(request).ToString();
            entry.RequestMethod = new RequestMethod(request.Method);

            foreach (var header in request.Headers)
            {
                if (IncludeHeader(header.Key))
                {
                    entry.Request.Headers.Add(header.Key, header.Value.ToArray());
                }
            }

            entry.Request.Body = await ReadAllBytes(request.Body).ConfigureAwait(false);
            return entry;
        }

        private static bool IncludeHeader(string header)
        {
            return !header.Equals("Host", StringComparison.OrdinalIgnoreCase)
                && !header.StartsWith("x-recording-", StringComparison.OrdinalIgnoreCase);
        }

        private static async Task<byte[]> ReadAllBytes(Stream stream)
        {
            using var memory = new MemoryStream();
            using (stream)
            {
                await stream.CopyToAsync(memory).ConfigureAwait(false);
            }
            return memory.Length == 0 ? null : memory.ToArray();
        }

        public static Uri GetRequestUri(HttpRequest request)
        {
            var uri = new RequestUriBuilder();

            var target_uri = GetHeader(request, "x-recording-upstream-base-uri");
            return new Uri(target_uri);
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
