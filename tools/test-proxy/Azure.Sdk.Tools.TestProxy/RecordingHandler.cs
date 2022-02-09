using Azure.Core;
using Azure.Sdk.Tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Transforms;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
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
        private const string SkipRecordingHeaderKey = "x-recording-skip";
        private const string SkipRecordingRequestBody = "request-body";
        private const string SkipRecordingRequestResponse = "request-response";

        public RecordingHandler(string targetDirectory)
        {
            RepoPath = targetDirectory;

            SetDefaultExtensions();
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

        public List<RecordedTestSanitizer> Sanitizers { get; set; }

        public List<ResponseTransform> Transforms { get; set; }

        public RecordMatcher Matcher { get; set; }

        public readonly ConcurrentDictionary<string, (string File, ModifiableRecordSession ModifiableSession)> RecordingSessions
            = new ConcurrentDictionary<string, (string, ModifiableRecordSession)>();

        public readonly ConcurrentDictionary<string, ModifiableRecordSession> InMemorySessions
            = new ConcurrentDictionary<string, ModifiableRecordSession>();

        public readonly ConcurrentDictionary<string, ModifiableRecordSession> PlaybackSessions
            = new ConcurrentDictionary<string, ModifiableRecordSession>();
        #endregion

        #region recording functionality
        public void StopRecording(string sessionId, IDictionary<string, string> variables = null)
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

            if(variables != null)
            {
                foreach(var kvp in variables)
                {
                    session.Session.Variables[kvp.Key] = kvp.Value;
                }
            }

            if (String.IsNullOrEmpty(file))
            {
                if (!InMemorySessions.TryAdd(sessionId, session))
                {
                    throw new HttpException(HttpStatusCode.InternalServerError, $"Unexpectedly failed to add new in-memory session under id {sessionId}.");
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
                stream.Write(Encoding.UTF8.GetBytes(Environment.NewLine));
            }
        }

        public void StartRecording(string sessionId, HttpResponse outgoingResponse)
        {
            var id = Guid.NewGuid().ToString();
            var session = (sessionId ?? String.Empty, new ModifiableRecordSession(new RecordSession()));

            if (!RecordingSessions.TryAdd(id, session))
            {
                throw new HttpException(HttpStatusCode.InternalServerError, $"Unexpectedly failed to add new recording session under id {id}.");
            }

            outgoingResponse.Headers.Add("x-recording-id", id);
        }

        public async Task HandleRecordRequestAsync(string recordingId, HttpRequest incomingRequest, HttpResponse outgoingResponse, HttpClient client)
        {
            await DebugLogger.LogRequestDetailsAsync(incomingRequest);

            if (!RecordingSessions.TryGetValue(recordingId, out var session))
            {
                throw new HttpException(HttpStatusCode.BadRequest, $"There is no active recording session under id {recordingId}.");
            }

            var entry = await CreateEntryAsync(incomingRequest).ConfigureAwait(false);

            var upstreamRequest = CreateUpstreamRequest(incomingRequest, entry.Request.Body);
            var upstreamResponse = await client.SendAsync(upstreamRequest).ConfigureAwait(false);

            var headerListOrig = incomingRequest.Headers.Select(x => String.Format("{0}: {1}", x.Key, x.Value.First())).ToList();
            var headerList = upstreamRequest.Headers.Select(x => String.Format("{0}: {1}", x.Key, x.Value.First())).ToList();


            byte[] body = Array.Empty<byte>();

            // HEAD requests do NOT have a body regardless of the value of the Content-Length header
            if (incomingRequest.Method.ToUpperInvariant() != "HEAD")
            {
                body = DecompressBody((MemoryStream)await upstreamResponse.Content.ReadAsStreamAsync().ConfigureAwait(false), upstreamResponse.Content.Headers);
            }

            entry.Response.Body = body.Length == 0 ? null : body;
            entry.StatusCode = (int)upstreamResponse.StatusCode;

            EntryRecordMode mode = GetRecordMode(incomingRequest);

            if (mode != EntryRecordMode.DontRecord)
            {
                session.ModifiableSession.Session.Entries.Add(entry);

                Interlocked.Increment(ref Startup.RequestsRecorded);
            }

            if (mode == EntryRecordMode.RecordWithoutRequestBody)
            {
                entry.Request.Body = null;
            }

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

        private static EntryRecordMode GetRecordMode(HttpRequest request)
        {
            EntryRecordMode mode = EntryRecordMode.Record;
            if (request.Headers.TryGetValue(SkipRecordingHeaderKey, out var values))
            {
                if (values.Count != 1)
                {
                    throw new HttpException(
                        HttpStatusCode.BadRequest,
                        $"'{SkipRecordingHeaderKey}' should contain a single value set to either '{SkipRecordingRequestBody}' or " +
                        $"'{SkipRecordingRequestResponse}'");
                }
                string skipMode = values.First();
                if (skipMode.Equals(SkipRecordingRequestResponse, StringComparison.OrdinalIgnoreCase))
                {
                    mode = EntryRecordMode.DontRecord;
                }
                else if (skipMode.Equals(SkipRecordingRequestBody, StringComparison.OrdinalIgnoreCase))
                {
                    mode = EntryRecordMode.RecordWithoutRequestBody;
                }
                else
                {
                    throw new HttpException(
                        HttpStatusCode.BadRequest,
                        $"{skipMode} is not a supported value for header '{SkipRecordingHeaderKey}'." +
                        $"It should be either omitted from the request headers, or set to either '{SkipRecordingRequestBody}' " +
                        $"or '{SkipRecordingRequestResponse}'");
                }
            }

            return mode;
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

        #endregion

        #region playback functionality
        public async Task StartPlaybackAsync(string sessionId, HttpResponse outgoingResponse, RecordingType mode = RecordingType.FilePersisted)
        {
            var id = Guid.NewGuid().ToString();
            ModifiableRecordSession session;

            if (mode == RecordingType.InMemory)
            {
                if (!InMemorySessions.TryGetValue(sessionId, out session))
                {
                    throw new HttpException(HttpStatusCode.BadRequest, $"There is no in-memory session with id {sessionId} available for playback retrieval.");
                }
                session.SourceRecordingId = sessionId;
            }
            else
            {
                var path = GetRecordingPath(sessionId);

                if (!File.Exists(path))
                {
                    throw new TestRecordingMismatchException($"Recording file path {path} does not exist.");
                }

                using var stream = System.IO.File.OpenRead(path);
                using var doc = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
                session = new ModifiableRecordSession(RecordSession.Deserialize(doc.RootElement));
            }

            if (!PlaybackSessions.TryAdd(id, session))
            {
                throw new HttpException(HttpStatusCode.InternalServerError, $"Unexpectedly failed to add new playback session under id {id}.");
            }

            outgoingResponse.Headers.Add("x-recording-id", id);


            var json = JsonSerializer.Serialize(session.Session.Variables);
            outgoingResponse.Headers.Add("Content-Type", "application/json");

            // Write to the response
            await outgoingResponse.WriteAsync(json);
        }

        public void StopPlayback(string recordingId, bool purgeMemoryStore = false)
        {
            if (!PlaybackSessions.TryRemove(recordingId, out var session))
            {
                throw new HttpException(HttpStatusCode.BadRequest, $"There is no active playback session under recording id {recordingId}.");
            }

            if (!String.IsNullOrEmpty(session.SourceRecordingId) && purgeMemoryStore)
            {
                if (!InMemorySessions.TryGetValue(session.SourceRecordingId, out var inMemorySession))
                {
                    throw new HttpException(HttpStatusCode.InternalServerError, $"Unexpectedly failed to retrieve in-memory session {session.SourceRecordingId}.");
                }

                Interlocked.Add(ref Startup.RequestsRecorded, -1 * inMemorySession.Session.Entries.Count);                

                if (!InMemorySessions.TryRemove(session.SourceRecordingId, out _))
                {
                    throw new HttpException(HttpStatusCode.InternalServerError, $"Unexpectedly failed to remove in-memory session {session.SourceRecordingId}.");
                }

                GC.Collect();
            }
        }

        public async Task HandlePlaybackRequest(string recordingId, HttpRequest incomingRequest, HttpResponse outgoingResponse)
        {
            await DebugLogger.LogRequestDetailsAsync(incomingRequest);

            if (!PlaybackSessions.TryGetValue(recordingId, out var session))
            {
                throw new HttpException(HttpStatusCode.BadRequest, $"There is no active playback session under recording id {recordingId}.");
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

            foreach (ResponseTransform transform in Transforms.Concat(session.AdditionalTransforms))
            {
                transform.Transform(match);
            }

            Interlocked.Increment(ref Startup.RequestsPlayedBack);

            outgoingResponse.StatusCode = match.StatusCode;

            foreach (var header in match.Response.Headers)
            {
                outgoingResponse.Headers.Add(header.Key, header.Value.ToArray());
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
            entry.RequestUri = GetRequestUri(request).AbsoluteUri;
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

        #endregion

        #region common functions
        public void AddSanitizerToRecording(string recordingId, RecordedTestSanitizer sanitizer)
        {
            if (PlaybackSessions.TryGetValue(recordingId, out var playbackSession))
            {
                lock (playbackSession)
                {
                    playbackSession.AdditionalSanitizers.Add(sanitizer);
                }
            }

            if (RecordingSessions.TryGetValue(recordingId, out var recordingSession))
            {
                lock (recordingSession.ModifiableSession)
                {
                    recordingSession.ModifiableSession.AdditionalSanitizers.Add(sanitizer);
                }
            }

            if (InMemorySessions.TryGetValue(recordingId, out var inMemSession))
            {
                lock (inMemSession)
                {
                    inMemSession.AdditionalSanitizers.Add(sanitizer);
                }
            }

            if (inMemSession == null && recordingSession == (null, null) && playbackSession == null)
            {
                throw new HttpException(HttpStatusCode.BadRequest, $"{recordingId} is not an active session for either record or playback. Check the value being passed and try again.");
            }
        }

        public void AddTransformToRecording(string recordingId, ResponseTransform transform)
        {
            if (!PlaybackSessions.TryGetValue(recordingId, out var session))
            {
                throw new HttpException(HttpStatusCode.BadRequest, $"{recordingId} is not an active playback session. Check the value being passed and try again.");
            }

            session.AdditionalTransforms.Add(transform);
        }


        public void SetMatcherForRecording(string recordingId, RecordMatcher matcher)
        {
            if (!PlaybackSessions.TryGetValue(recordingId, out var session))
            {
                throw new HttpException(HttpStatusCode.BadRequest, $"{recordingId} is not an active playback session. Check the value being passed and try again.");
            }

            session.CustomMatcher = matcher;
        }

        public void SetDefaultExtensions(string recordingId = null)
        {
            if (recordingId != null)
            {
                if (PlaybackSessions.TryGetValue(recordingId, out var playbackSession))
                {
                    playbackSession.ResetExtensions();
                }
                if (RecordingSessions.TryGetValue(recordingId, out var recordSession))
                {
                    recordSession.ModifiableSession.ResetExtensions();
                }
                if (InMemorySessions.TryGetValue(recordingId, out var inMemSession))
                {
                    inMemSession.ResetExtensions();
                }
            }
            else
            {
                var countPlayback = PlaybackSessions.Count;
                var countInMem = InMemorySessions.Count;
                var countRecording = RecordingSessions.Count;
                var countTotal = countPlayback + countInMem + countRecording;

                if (countTotal > 0)
                {
                    StringBuilder sb = new StringBuilder();

                    sb.Append($"There are a total of {countTotal} active sessions. Remove these sessions before hitting Admin/Reset." + Environment.NewLine);

                    if(countPlayback > 0)
                    {
                        sb.Append("Active Playback Sessions: [");
                        lock (PlaybackSessions)
                        {
                            sb.Append(string.Join(", ", PlaybackSessions.Keys.ToArray()));
                        }
                        sb.Append("]. ");
                    }

                    if (countInMem > 0)
                    {
                        sb.Append("Active InMem Sessions: [");
                        lock (InMemorySessions)
                        {
                            sb.Append(string.Join(", ", InMemorySessions.Keys.ToArray()));
                        }
                        sb.Append("]. ");
                    }

                    if (countRecording > 0)
                    {
                        sb.Append($"{countRecording} Active Recording Sessions: [");
                        lock (RecordingSessions)
                        {
                            sb.Append(string.Join(", ", RecordingSessions.Keys.ToArray()));
                        }
                        sb.Append("]. ");
                    }

                    throw new HttpException(HttpStatusCode.BadRequest, sb.ToString());               
                }
                Sanitizers = new List<RecordedTestSanitizer>
                {
                    new RecordedTestSanitizer()
                };

                Transforms = new List<ResponseTransform>
                {
                    new StorageRequestIdTransform(),
                    new ClientIdTransform(),
                    new HeaderTransform("Retry-After", "0")
                    {
                        Condition = new ApplyCondition
                        {
                            ResponseHeader = new HeaderCondition
                            {
                                Key = "Retry-After"
                            }
                        }
                    }
                };

                Matcher = new RecordMatcher();
            }
        }


        public string GetRecordingPath(string file)
        {
            var normalizedFileName = file.Replace('\\', '/');

            if (String.IsNullOrWhiteSpace(file))
            {
                throw new HttpException(HttpStatusCode.BadRequest, $"Recording file value of {file} is invalid. Try again with a populated filename.");
            }

            var path = file;

            if (!Path.IsPathFullyQualified(file))
            {
                path = Path.Join(RepoPath, file);
            }

            return (path + (!path.EndsWith(".json") ? ".json" : String.Empty));
        }

        public static string GetHeader(HttpRequest request, string name, bool allowNulls = false)
        {
            if (!request.Headers.TryGetValue(name, out var value))
            {
                if (allowNulls)
                {
                    return null;
                }
                throw new HttpException(HttpStatusCode.BadRequest, $"Expected header {name} is not populated in request.");
            }

            return value;
        }

        public static Uri GetRequestUri(HttpRequest request)
        {
            // Instead of obtaining the Path of the request from request.Path, we use this
            // more complicated method obtaining the raw string from the httpcontext. Unfortunately,
            // The native request functions implicitly decode the Path value. EG: "aa%27bb" is decoded into 'aa'bb'.
            // Using the RawTarget PREVENTS this automatic decode. We still lean on the URI constructors
            // to give us some amount of safety, but note that we explicitly disable escaping in that combination.
            var rawTarget = request.HttpContext.Features.Get<IHttpRequestFeature>().RawTarget;
            var hostValue = GetHeader(request, "x-recording-upstream-base-uri");
            
            // There is an ongoing issue where some libraries send a URL with two leading // after the hostname.
            // This will just handle the error explicitly rather than letting it slip through and cause random issues during record/playback sessions.
            if (rawTarget.StartsWith("//"))
            {
                throw new HttpException(HttpStatusCode.BadRequest, $"The URI being passed has two leading '/' in the Target, which will break URI combine with the hostname. Visible URI target: {rawTarget}.");
            }

            // it is easy to forget the x-recording-upstream-base-uri value
            if (string.IsNullOrWhiteSpace(hostValue))
            {
                throw new HttpException(HttpStatusCode.BadRequest, $"The value present in header 'x-recording-upstream-base-uri' is not a valid hostname: {hostValue}.");
            }

            var host = new Uri(hostValue);
            return new Uri(host, rawTarget);
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
