using Azure.Core;
using Azure.Sdk.Tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Transforms;
using LibGit2Sharp;
using Microsoft.AspNetCore.Http;
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
    public class RecordingHandler
    {
        #region constructor and common variables
        public string CurrentBranch = "master";
        public IRepository Repository;
        public string RepoPath;

        public RecordingHandler(string targetDirectory)
        {
            try
            {
                Repository = new Repository(targetDirectory);
            }
            catch(Exception)
            {
                Console.WriteLine("The configured storage directory is not a git repository. Git functionality will be unavailable.");
            }

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

        public readonly ConcurrentDictionary<string, (string File, ModifiableRecordSession ModifiableSession)> recording_sessions
            = new ConcurrentDictionary<string, (string, ModifiableRecordSession)>();

        private readonly ConcurrentDictionary<string, ModifiableRecordSession> playback_sessions 
            = new ConcurrentDictionary<string, ModifiableRecordSession>();

        public RecordMatcher Matcher = new RecordMatcher();
        #endregion

        #region recording functionality
        public void StopRecording(string id, bool saveToDisk)
        {
            if (!recording_sessions.TryRemove(id, out var fileAndSession))
            {
                return;
            }

            if (saveToDisk)
            {
                var (file, session) = fileAndSession;

                foreach (RecordedTestSanitizer sanitizer in Sanitizers.Concat(session.AdditionalSanitizers))
                {
                    session.Session.Sanitize(sanitizer);
                }

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

        public void StartRecording(string fileId, HttpResponse outgoingResponse)
        {
            var id = Guid.NewGuid().ToString();
            var session = (fileId, new ModifiableRecordSession(new RecordSession()));

            if (!recording_sessions.TryAdd(id, session))
            {
                // This should not happen as the key is a new GUID.
                throw new InvalidOperationException("Failed to add new session.");
            }


            outgoingResponse.Headers.Add("x-recording-id", id);
        }

        public async Task HandleRecordRequest(string recordingId, HttpRequest incomingRequest, HttpResponse outgoingResponse, HttpClient client)
        {
            if (!recording_sessions.TryGetValue(recordingId, out var session))
            {
                throw new InvalidOperationException("No recording loaded with that ID.");
            }

            var entry = await CreateEntryAsync(incomingRequest).ConfigureAwait(false);
            var upstreamRequest = CreateUpstreamRequest(incomingRequest, entry.Request.Body);
            var upstreamResponse = await client.SendAsync(upstreamRequest).ConfigureAwait(false);

            var body = await upstreamResponse.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            entry.Response.Body = body.Length == 0 ? null : body;
            entry.StatusCode = (int)upstreamResponse.StatusCode;
            session.ModifiableSession.Session.Entries.Add(entry);

            Interlocked.Increment(ref Startup.RequestsRecorded);

            outgoingResponse.StatusCode = (int)upstreamResponse.StatusCode;
            foreach (var header in upstreamResponse.Headers)
            {
                var values = new StringValues(header.Value.ToArray());
                outgoingResponse.Headers.Add(header.Key, values);
                entry.Response.Headers.Add(header.Key, values);
            }

            outgoingResponse.Headers.Remove("Transfer-Encoding");

            if (entry.Response.Body?.Length > 0)
            {
                outgoingResponse.ContentLength = entry.Response.Body.Length;
                await outgoingResponse.Body.WriteAsync(entry.Response.Body).ConfigureAwait(false);
            }
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

        public void AddRecordSanitizer(string recordingId, RecordedTestSanitizer sanitizer)
        {
            if (!recording_sessions.TryGetValue(recordingId, out var session))
            {
                throw new InvalidOperationException("No recording loaded with that ID.");
            }

            session.ModifiableSession.AdditionalSanitizers.Add(sanitizer);
        }

        #endregion

        #region playback functionality
        public async Task StartPlayback(string fileId, HttpResponse outgoingResponse)
        {
            var id = Guid.NewGuid().ToString();
            using var stream = System.IO.File.OpenRead(GetRecordingPath(fileId));
            using var doc = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
            var session = new ModifiableRecordSession(RecordSession.Deserialize(doc.RootElement));

            if (!playback_sessions.TryAdd(id, session))
            {
                // This should not happen as the key is a new GUID.
                throw new InvalidOperationException("Failed to add new session.");
            }

            outgoingResponse.Headers.Add("x-recording-id", id);
        }

        public void StopPlayback(string recordingId)
        {
            playback_sessions.TryRemove(recordingId, out _);
        }

        public async Task Playback(string recordingId, HttpRequest incomingRequest, HttpResponse outgoingResponse)
        {
            if (!playback_sessions.TryGetValue(recordingId, out var session))
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

            
            var match = session.Session.Lookup(entry, session.CustomMatcher??Matcher, session.AdditionalSanitizers.Count > 0 ? Sanitizers.Concat(session.AdditionalSanitizers) : Sanitizers, remove);

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
                outgoingResponse.ContentLength = match.Response.Body.Length;
                await outgoingResponse.Body.WriteAsync(match.Response.Body).ConfigureAwait(false);
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
            if (!playback_sessions.TryGetValue(recordingId, out var session))
            {
                throw new InvalidOperationException("No recording loaded with that ID.");
            }

            session.AdditionalSanitizers.Add(sanitizer);
        }

        public void SetPlaybackMatcher(string recordingId, RecordMatcher matcher)
        {
            if (!playback_sessions.TryGetValue(recordingId, out var session))
            {
                throw new InvalidOperationException("No recording loaded with that ID.");
            }

            session.CustomMatcher = matcher;
        }

        public void AddPlaybackTransform(string recordingId, ResponseTransform transform)
        {
            if (!playback_sessions.TryGetValue(recordingId, out var session))
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
            var target_uri = GetHeader(request, "x-recording-upstream-base-uri");
            return new Uri(target_uri);
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

        #region git functionality
        public void Commit()
        {
            foreach (var item in Repository.RetrieveStatus())
            {
                Commands.Stage(Repository, item.FilePath);
            }

            // TODO: pull the signature from local git creds, fall back to environment variable PAT. Some kind of generated message
            Repository.Commit("Updating Recordings.", new Signature("scbedd", "scbedd@microsoft.com", System.DateTimeOffset.Now), new Signature("scbedd", "scbedd@microsoft.com", System.DateTimeOffset.Now));
        }

        public void Checkout(string targetBranchName)
        {
            if (CurrentBranch != targetBranchName)
            {
                ResetAndCleanWorkingDirectory();

                var targetBranch = Repository.Branches[targetBranchName];

                if (targetBranch == null)
                {
                    Repository.CreateBranch(targetBranchName);
                }

                CurrentBranch = targetBranchName;
                Commands.Checkout(Repository, targetBranch);
            }
        }

        private void ResetAndCleanWorkingDirectory()
        {
            // Reset the index and the working tree.
            Repository.Reset(ResetMode.Hard);

            // Clean the working directory.
            Repository.RemoveUntrackedFiles();
        }
        #endregion
    }
}
