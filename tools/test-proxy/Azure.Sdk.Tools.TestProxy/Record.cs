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
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.TestProxy
{
    [ApiController]
    [Route("[controller]/[action]")]
    public sealed class Record : ControllerBase
    {
        private readonly RecordingHandler _recordingHandler;
        public Record(RecordingHandler recordingHandler) => _recordingHandler = recordingHandler;


        private static readonly ConcurrentDictionary<string, (string File, RecordSession Session)> s_sessions
            = new ConcurrentDictionary<string, (string, RecordSession)>();

        private static readonly RecordedTestSanitizer s_sanitizer = new RecordedTestSanitizer();

        private static readonly string[] s_excludedRequestHeaders = new string[] {
            // Only applies to request between client and proxy
            // TODO, we need to handle this properly, there are tests that actually test proxy functionality.
            "Proxy-Connection",
        };

        // Headers which must be set on HttpContent instead of HttpRequestMessage
        private static readonly string[] s_contentRequestHeaders = new string[] {
            "Content-Length",
            "Content-Type",
        };

        private static readonly HttpClient s_client = new HttpClient();

        [HttpPost]
        public void Start()
        {
            string file = GetHeader(Request, "x-recording-file");

            var id = Guid.NewGuid().ToString();
            var session = (file, new RecordSession());

            if (!s_sessions.TryAdd(id, session))
            {
                // This should not happen as the key is a new GUID.
                throw new InvalidOperationException("Failed to add new session.");
            }

            Response.Headers.Add("x-recording-id", id);
        }

        [HttpPost]
        public void Stop()
        {
            string id = GetHeader(Request, "x-recording-id");

            if (!s_sessions.TryRemove(id, out var fileAndSession))
            {
                return;
            }

            bool save = bool.Parse(GetHeader(Request, "x-recording-save"));
            if (save)
            {
                var (file, session) = fileAndSession;
                session.Sanitize(s_sanitizer);

                var targetPath = _recordingHandler.GetRecordingPath(file);

                // Create directories above file if they don't already exist
                var directory = Path.GetDirectoryName(targetPath);
                if (!String.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using var stream = System.IO.File.Create(targetPath);
                var options = new JsonWriterOptions { Indented = true };
                var writer = new Utf8JsonWriter(stream, options);
                session.Serialize(writer);
                writer.Flush();
            }
        }

        public async Task HandleRequest()
        {
            string id = GetHeader(Request, "x-recording-id");

            if (!s_sessions.TryGetValue(id, out var session))
            {
                throw new InvalidOperationException("No recording loaded with that ID.");
            }

            var entry = await Playback.CreateEntryAsync(Request).ConfigureAwait(false);
            var upstreamRequest = CreateUpstreamRequest(Request, entry.Request.Body);
            var upstreamResponse = await s_client.SendAsync(upstreamRequest).ConfigureAwait(false);

            var body = await upstreamResponse.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            entry.Response.Body = body.Length == 0 ? null : body;
            entry.StatusCode = (int)upstreamResponse.StatusCode;
            session.Session.Entries.Add(entry);

            Interlocked.Increment(ref Startup.RequestsRecorded);

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
