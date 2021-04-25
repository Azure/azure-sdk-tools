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
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.TestProxy
{
    // !! TODO: Lots of Record/Playback duplication
    // !! TODO: Replace statics with dependency injection, expiration policy of some kind
    [ApiController]
    [Route("[controller]/[action]")]
    public sealed class Playback : ControllerBase
    {
        private static readonly ConcurrentDictionary<string, RecordSession> s_sessions
            = new ConcurrentDictionary<string, RecordSession>();

        // !! Neither matching nor sanitization can be customized yet.
        private static readonly RecordMatcher s_matcher = new RecordMatcher();
        private static readonly RecordedTestSanitizer s_sanitizer = new RecordedTestSanitizer();

        private readonly RecordingHandler _recordingHandler;
        public Playback(RecordingHandler recordingHandler) => _recordingHandler = recordingHandler;

        [HttpPost]
        public async Task Start()
        {
            string file = GetHeader(Request, "x-recording-file");

            var id = Guid.NewGuid().ToString();
            using var stream = System.IO.File.OpenRead(_recordingHandler.GetRecordingPath(file));
            using var doc = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
            var session = RecordSession.Deserialize(doc.RootElement);

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
            s_sessions.TryRemove(id, out _);
        }

        public async Task HandleRequest()
        {
            string id = GetHeader(Request, "x-recording-id");

            if (!s_sessions.TryGetValue(id, out var session))
            {
                throw new InvalidOperationException("No recording loaded with that ID.");
            }

            var entry = await CreateEntryAsync(Request).ConfigureAwait(false);

            // If request contains "x-recording-remove: false", then request is not removed from session after playback.
            // Used by perf tests to play back the same request multiple times.
            var remove = true;
            if (Request.Headers.TryGetValue("x-recording-remove", out var removeHeader))
            {
                remove = bool.Parse(removeHeader);
            }

            var match = session.Lookup(entry, s_matcher, s_sanitizer, remove);

            Interlocked.Increment(ref Startup.RequestsPlayedBack);

            Response.StatusCode = match.StatusCode;

            foreach (var header in match.Response.Headers)
            {
                Response.Headers.Add(header.Key, header.Value.ToArray());
            }

            // TODO, allow per-test extension of the manipulations, to do this we need 
            foreach (ResponseTransform transform in SessionConfiguration.Transforms){
                transform.ApplyTransform(Request, Response);
            }

            Response.Headers.Remove("Transfer-Encoding");

            if (match.Response.Body?.Length > 0)
            {
                Response.ContentLength = match.Response.Body.Length;
                await Response.Body.WriteAsync(match.Response.Body).ConfigureAwait(false);
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
