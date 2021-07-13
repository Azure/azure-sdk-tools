using Azure.Core;
using Azure.Sdk.Tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Transforms;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.TestProxy
{
    public class RecordingHandler
    {
        #region constructor and common variables
        public string CurrentBranch = "master";
        public string RepoPath;

        public RecordingHandler(string targetDirectory)
        {
            RepoPath = targetDirectory;
        }

        private static readonly RecordedTestSanitizer defaultSanitizer = new RecordedTestSanitizer();

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

        public List<RecordedTestSanitizer> Sanitizers = new List<RecordedTestSanitizer>
        {
            new RecordedTestSanitizer()
        };

        public List<ResponseTransform> Transforms = new List<ResponseTransform>
        {
            new StorageRequestIdTransform(),
            new ClientIdTransform()
        };

        public readonly ConcurrentDictionary<string, (string File, ModifiableRecordSession ModifiableSession)> RecordingSessions
            = new ConcurrentDictionary<string, (string, ModifiableRecordSession)>();

        public readonly ConcurrentDictionary<string, ModifiableRecordSession> InMemorySessions
            = new ConcurrentDictionary<string, ModifiableRecordSession>();

        public readonly ConcurrentDictionary<string, ModifiableRecordSession> PlaybackSessions
            = new ConcurrentDictionary<string, ModifiableRecordSession>();

        public RecordMatcher Matcher = new RecordMatcher();
        #endregion

        #region recording functionality
        public void StopRecording(string sessionId)
        {
            if (!RecordingSessions.TryRemove(sessionId, out var fileAndSession))
            {
                return;
            }

            var (file, session) = fileAndSession;

            foreach (RecordedTestSanitizer sanitizer in Sanitizers.Concat(session.AdditionalSanitizers))
            {
                session.Session.Sanitize(sanitizer);
            }

            if (String.IsNullOrEmpty(file))
            {
                if (!InMemorySessions.TryAdd(sessionId, session))
                {
                    // This should not happen as the key is a new GUID.
                    throw new InvalidOperationException("Failed to save in-memory session.");
                }
            }
            else
            {
                var targetPath = GetRecordingPath(file);

                // Create directories above file if they don't already exist
                var directory = Path.GetDirectoryName(targetPath);
                if (!String.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using var stream = System.IO.File.Create(targetPath);
                var options = new JsonWriterOptions { Indented = true };
                var writer = new Utf8JsonWriter(stream, options);
                session.Session.Serialize(writer);
                writer.Flush();
            }
        }

        public void StartRecording(string sessionId, HttpResponse outgoingResponse)
        {
            var id = Guid.NewGuid().ToString();
            var session = (sessionId ?? String.Empty, new ModifiableRecordSession(new RecordSession()));

            if (!RecordingSessions.TryAdd(id, session))
            {
                // This should not happen as the key is a new GUID.
                throw new InvalidOperationException("Failed to add new session.");
            }

            outgoingResponse.Headers.Add("x-recording-id", id);
        }

        public async Task HandleRecordRequest(string recordingId, HttpRequest incomingRequest, HttpResponse outgoingResponse, HttpClient client)
        {
            if (!RecordingSessions.TryGetValue(recordingId, out var session))
            {
                throw new InvalidOperationException("No recording loaded with that ID.");
            }

            var entry = await CreateEntryAsync(incomingRequest).ConfigureAwait(false);

            var upstreamRequest = CreateUpstreamRequest(incomingRequest, entry.Request.Body);
            var upstreamResponse = await client.SendAsync(upstreamRequest).ConfigureAwait(false);

            var headerListOrig = incomingRequest.Headers.Select(x => String.Format("{0}: {1}", x.Key, x.Value.First())).ToList();
            var headerList = upstreamRequest.Headers.Select(x => String.Format("{0}: {1}", x.Key, x.Value.First())).ToList();

            var body = DecompressBody((MemoryStream) await upstreamResponse.Content.ReadAsStreamAsync().ConfigureAwait(false), upstreamResponse.Content.Headers);

            entry.Response.Body = body.Length == 0 ? null : body;
            entry.StatusCode = (int)upstreamResponse.StatusCode;
            session.ModifiableSession.Session.Entries.Add(entry);

            Interlocked.Increment(ref Startup.RequestsRecorded);

            outgoingResponse.StatusCode = (int)upstreamResponse.StatusCode;
            foreach (var header in upstreamResponse.Headers.Concat(upstreamResponse.Content.Headers))
            {
                var values = new StringValues(header.Value.ToArray());
                outgoingResponse.Headers.Add(header.Key, values);
                entry.Response.Headers.Add(header.Key, values);
            }

            outgoingResponse.Headers.Remove("Transfer-Encoding");

            if (entry.Response.Body?.Length > 0)
            {
                var bodyData = CompressBody(entry.Response.Body, entry.Response.Headers);
                outgoingResponse.ContentLength = bodyData.Length;
                await outgoingResponse.Body.WriteAsync(bodyData).ConfigureAwait(false);
            }
        }

        private byte[] CompressBody(byte[] incomingBody, SortedDictionary<string, string[]> headers)
        {
            if (headers.TryGetValue("Content-Encoding", out var values))
            {
                if (values.Contains("gzip"))
                {
                    using (var uncompressedStream = new MemoryStream(incomingBody))
                    using (var resultStream = new MemoryStream())
                    {
                        using (var compressedStream = new GZipStream(resultStream, CompressionMode.Compress))
                        {
                            uncompressedStream.CopyTo(compressedStream);
                        }
                        return resultStream.ToArray();
                    }
                }
            }

            return incomingBody;
        }

        private byte[] DecompressBody(MemoryStream incomingBody, HttpContentHeaders headers)
        {
            if (headers.TryGetValues("Content-Encoding", out var values))
            {
                if (values.Contains("gzip"))
                {
                    using (var uncompressedStream = new GZipStream(incomingBody, CompressionMode.Decompress))
                    using (var resultStream = new MemoryStream())
                    {
                        uncompressedStream.CopyTo(resultStream);
                        return resultStream.ToArray();
                    }
                }
            }

            return incomingBody.ToArray();
        }

        private HttpRequestMessage CreateUpstreamRequest(HttpRequest incomingRequest, byte[] incomingBody)
        {
            var upstreamRequest = new HttpRequestMessage();
            upstreamRequest.RequestUri = GetRequestUri(incomingRequest);
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
                        upstreamRequest.Content.Headers.TryAddWithoutValidation(header.Key, values);
                    }
                    else
                    {
                        if (!header.Key.StartsWith("x-recording"))
                        {
                            upstreamRequest.Headers.TryAddWithoutValidation(header.Key, values);
                        }
                    }
                }
                catch (Exception)
                {
                    // ignore
                }
            }

            upstreamRequest.Headers.Host = upstreamRequest.RequestUri.Host;

            return upstreamRequest;
        }

        public void AddRecordSanitizer(string recordingId, RecordedTestSanitizer sanitizer)
        {
            if (!RecordingSessions.TryGetValue(recordingId, out var session))
            {
                throw new InvalidOperationException("No recording loaded with that ID.");
            }

            session.ModifiableSession.AdditionalSanitizers.Add(sanitizer);
        }

        #endregion

        #region playback functionality
        public async Task StartPlayback(string sessionId, HttpResponse outgoingResponse, RecordingType mode = RecordingType.FilePersisted)
        {
            var id = Guid.NewGuid().ToString();
            ModifiableRecordSession session;

            if (mode == RecordingType.InMemory)
            {
                if (!InMemorySessions.TryGetValue(sessionId, out session))
                {
                    throw new InvalidOperationException("Failed to retrieve in-memory session.");
                }
                session.SourceRecordingId = sessionId;
            }
            else
            {
                using var stream = System.IO.File.OpenRead(GetRecordingPath(sessionId));
                using var doc = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
                session = new ModifiableRecordSession(RecordSession.Deserialize(doc.RootElement));
            }

            if (!PlaybackSessions.TryAdd(id, session))
            {
                // This should not happen as the key is a new GUID.
                throw new InvalidOperationException("Failed to add new session.");
            }

            outgoingResponse.Headers.Add("x-recording-id", id);
        }

        public void StopPlayback(string recordingId, bool purgeMemoryStore = false)
        {
            if (!PlaybackSessions.TryRemove(recordingId, out var session))
            {
                throw new InvalidOperationException("Unexpected failure to retrieve playback session.");
            }

            if (!String.IsNullOrEmpty(session.SourceRecordingId) && purgeMemoryStore)
            {
                if (!InMemorySessions.TryGetValue(session.SourceRecordingId, out var inMemorySession))
                {
                    throw new InvalidOperationException("Unexpected failure to retrieve in-memory session.");
                }

                Interlocked.Add(ref Startup.RequestsRecorded, -1 * inMemorySession.Session.Entries.Count);                

                if (!InMemorySessions.TryRemove(session.SourceRecordingId, out _))
                {
                    throw new InvalidOperationException("Unexpected failure to remove in-memory session.");
                }
            }
        }

        public async Task HandlePlaybackRequest(string recordingId, HttpRequest incomingRequest, HttpResponse outgoingResponse)
        {
            if (!PlaybackSessions.TryGetValue(recordingId, out var session))
            {
                throw new InvalidOperationException("No recording loaded with that ID.");
            }

            var entry = await CreateEntryAsync(incomingRequest).ConfigureAwait(false);

            // If request contains "x-recording-remove: false", then request is not removed from session after playback.
            // Used by perf tests to play back the same request multiple times.
            var remove = true;
            if (incomingRequest.Headers.TryGetValue("x-recording-remove", out var removeHeader))
            {
                remove = bool.Parse(removeHeader);
            }


            var match = session.Session.Lookup(entry, session.CustomMatcher ?? Matcher, session.AdditionalSanitizers.Count > 0 ? Sanitizers.Concat(session.AdditionalSanitizers) : Sanitizers, remove);

            Interlocked.Increment(ref Startup.RequestsPlayedBack);

            outgoingResponse.StatusCode = match.StatusCode;

            foreach (var header in match.Response.Headers)
            {
                outgoingResponse.Headers.Add(header.Key, header.Value.ToArray());
            }

            foreach (ResponseTransform transform in session.AdditionalTransforms.Count > 0 ? Transforms.Concat(session.AdditionalTransforms) : Transforms)
            {
                transform.ApplyTransform(incomingRequest, outgoingResponse);
            }

            outgoingResponse.Headers.Remove("Transfer-Encoding");

            if (match.Response.Body?.Length > 0)
            {
                var bodyData = CompressBody(match.Response.Body, match.Response.Headers);

                outgoingResponse.ContentLength = bodyData.Length;
                await outgoingResponse.Body.WriteAsync(bodyData).ConfigureAwait(false);
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

        public void AddPlaybackSanitizer(string recordingId, RecordedTestSanitizer sanitizer)
        {
            if (!PlaybackSessions.TryGetValue(recordingId, out var session))
            {
                throw new InvalidOperationException("No recording loaded with that ID.");
            }

            session.AdditionalSanitizers.Add(sanitizer);
        }

        public void SetPlaybackMatcher(string recordingId, RecordMatcher matcher)
        {
            if (!PlaybackSessions.TryGetValue(recordingId, out var session))
            {
                throw new InvalidOperationException("No recording loaded with that ID.");
            }

            session.CustomMatcher = matcher;
        }

        public void AddPlaybackTransform(string recordingId, ResponseTransform transform)
        {
            if (!PlaybackSessions.TryGetValue(recordingId, out var session))
            {
                throw new InvalidOperationException("No recording loaded with that ID.");
            }

            session.AdditionalTransforms.Add(transform);
        }

        #endregion

        #region common functions
        public string GetRecordingPath(string file)
        {
            return Path.Join(RepoPath, "recordings", file);
        }

        public static string GetHeader(HttpRequest request, string name, bool allowNulls = false)
        {
            if (!request.Headers.TryGetValue(name, out var value))
            {
                if (allowNulls)
                {
                    return null;
                }
                throw new InvalidOperationException("Missing header: " + name);
            }

            return value;
        }

        public static Uri GetRequestUri(HttpRequest request)
        {
            var uri = new RequestUriBuilder();
            uri.Reset(new Uri(GetHeader(request, "x-recording-upstream-base-uri")));
            uri.Path = request.Path;
            uri.Query = request.QueryString.ToUriComponent();
            var result = uri.ToUri();

            return result;
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
        #endregion
    }
}
