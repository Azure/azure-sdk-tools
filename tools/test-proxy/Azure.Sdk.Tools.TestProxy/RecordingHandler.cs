using Azure.Core;
using Azure.Sdk.Tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Common.Exceptions;
using Azure.Sdk.Tools.TestProxy.Sanitizers;
using Azure.Sdk.Tools.TestProxy.Store;
using Azure.Sdk.Tools.TestProxy.Transforms;
using Azure.Sdk.Tools.TestProxy.Vendored;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.TestProxy
{
    public class RecordingHandler
    {
        #region constructor and common variables
        public string ContextDirectory;
        public bool HandleRedirects = true;

        private const string SkipRecordingHeaderKey = "x-recording-skip";
        private const string SkipRecordingRequestBody = "request-body";
        private const string SkipRecordingRequestResponse = "request-response";

        public IAssetsStore Store;
        public StoreResolver Resolver;

        private static readonly string[] s_excludedRequestHeaders = new string[] {
            // Only applies to request between client and proxy
            // TODO, we need to handle this properly, there are tests that actually test proxy functionality.
            "Host",
            "Proxy-Connection",
        };

        public HttpClient BaseRedirectableClient = Startup.Insecure ?
            new HttpClient(new HttpClientHandler() { ServerCertificateCustomValidationCallback = (_, _, _, _) => true })
            {
                Timeout = TimeSpan.FromSeconds(600),
            } :
            new HttpClient()
            {
                Timeout = TimeSpan.FromSeconds(600)
            };

        public HttpClient BaseRedirectlessClient = Startup.Insecure ?
            new HttpClient(new HttpClientHandler() { AllowAutoRedirect = false, ServerCertificateCustomValidationCallback = (_, _, _, _) => true })
            {
                Timeout = TimeSpan.FromSeconds(600),
            } :
            new HttpClient(new HttpClientHandler() { AllowAutoRedirect = false })
            {
                Timeout = TimeSpan.FromSeconds(600)
            };

        public HttpClient RedirectlessClient;
        public HttpClient RedirectableClient;

        public List<RecordedTestSanitizer> Sanitizers { get; set; }

        public List<ResponseTransform> Transforms { get; set; }

        public RecordMatcher Matcher { get; set; }

        public readonly ConcurrentDictionary<string, ModifiableRecordSession> RecordingSessions
            = new ConcurrentDictionary<string, ModifiableRecordSession>();

        public readonly ConcurrentDictionary<string, ModifiableRecordSession> InMemorySessions
            = new ConcurrentDictionary<string, ModifiableRecordSession>();

        public readonly ConcurrentDictionary<string, ModifiableRecordSession> PlaybackSessions
            = new ConcurrentDictionary<string, ModifiableRecordSession>();

        public RecordingHandler(string targetDirectory, IAssetsStore store = null, StoreResolver storeResolver = null)
        {
            ContextDirectory = targetDirectory;

            SetDefaultExtensions();

            Store = store;
            if (store == null)
            {
                Store = new NullStore();
            }

            Resolver = storeResolver;
            if (Resolver == null)
            {
                Resolver = new StoreResolver();
            }
        }
        #endregion

        #region recording functionality
        public void StopRecording(string sessionId, IDictionary<string, string> variables = null, bool saveRecording = true)
        {

            var id = Guid.NewGuid().ToString();
            DebugLogger.LogTrace($"RECORD STOP BEGIN {id}.");

            if (!RecordingSessions.TryRemove(sessionId, out var recordingSession))
            {
                return;
            }

            foreach (RecordedTestSanitizer sanitizer in Sanitizers.Concat(recordingSession.AdditionalSanitizers))
            {
                recordingSession.Session.Sanitize(sanitizer);
            }

            if (variables != null)
            {
                foreach (var kvp in variables)
                {
                    recordingSession.Session.Variables[kvp.Key] = kvp.Value;
                }
            }

            if (saveRecording)
            {
                if (String.IsNullOrEmpty(recordingSession.Path))
                {
                    if (!InMemorySessions.TryAdd(sessionId, recordingSession))
                    {
                        throw new HttpException(HttpStatusCode.InternalServerError, $"Unexpectedly failed to add new in-memory session under id {sessionId}.");
                    }
                }
                else
                {
                    // Create directories above file if they don't already exist
                    var directory = Path.GetDirectoryName(recordingSession.Path);
                    if (!String.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    using var stream = System.IO.File.Create(recordingSession.Path);
                    var options = new JsonWriterOptions { Indented = true };
                    var writer = new Utf8JsonWriter(stream, options);
                    recordingSession.Session.Serialize(writer);
                    writer.Flush();
                    stream.Write(Encoding.UTF8.GetBytes(Environment.NewLine));
                }
            }

            DebugLogger.LogTrace($"RECORD STOP END {id}.");
        }

        /// <summary>
        /// Entrypoint handling an an optional parameter assets.json. If present, a restore option either MUST run or MAY run depending on if we're running in playback or adding new recordings.
        /// </summary>
        /// <param name="assetsJson">The absolute path to the targeted assets.json.</param>
        /// <param name="forceCheckout">If this is set to true, a restore MUST be run. Otherwise, we just need to ensure that the current assets Tag is selected.</param>
        /// <returns></returns>
        private async Task RestoreAssetsJson(string assetsJson = null, bool forceCheckout = false)
        {
            if (!string.IsNullOrWhiteSpace(assetsJson))
            {
                await this.Store.Restore(assetsJson);
            }
        }

        public async Task StartRecordingAsync(string sessionId, HttpResponse outgoingResponse, string assetsJson = null)
        {
            var id = Guid.NewGuid().ToString();
            DebugLogger.LogTrace($"RECORD START BEGIN {id}.");

            await RestoreAssetsJson(assetsJson, false);

            var session = new ModifiableRecordSession(new RecordSession())
            {
                Path = !string.IsNullOrWhiteSpace(sessionId) ? (await GetRecordingPath(sessionId, assetsJson)) : String.Empty,
                Client = null
            };

            if (!RecordingSessions.TryAdd(id, session))
            {
                throw new HttpException(HttpStatusCode.InternalServerError, $"Unexpectedly failed to add new recording session under id {id}.");
            }

            DebugLogger.LogTrace($"RECORD START END {id}.");
            outgoingResponse.Headers.Add("x-recording-id", id);
        }

        public async Task HandleRecordRequestAsync(string recordingId, HttpRequest incomingRequest, HttpResponse outgoingResponse)
        {
            if (!RecordingSessions.TryGetValue(recordingId, out var session))
            {
                throw new HttpException(HttpStatusCode.BadRequest, $"There is no active recording session under id {recordingId}.");
            }

            var sanitizers = session.AdditionalSanitizers.Count > 0 ? Sanitizers.Concat(session.AdditionalSanitizers) : Sanitizers;

            DebugLogger.LogRequestDetails(incomingRequest, sanitizers);

            (RecordEntry entry, byte[] requestBody) = await CreateEntryAsync(incomingRequest).ConfigureAwait(false);

            var upstreamRequest = CreateUpstreamRequest(incomingRequest, requestBody);

            HttpResponseMessage upstreamResponse = null;

            // The experience around Content-Length is a bit weird in .NET. We're using the .NET native HttpClient class to send our requests. This comes with
            // some automagic.
            //
            // If an incoming request...
            //    ...has a Content-Length 0 header, and no body. We should send along the Content-Length: 0 header with the upstreamrequest.
            //    ...has no Content-Length header, and no body. We _should not_ send along the Content-Length: 0 header.
            //    ...has no Content-Length header, a 0 length body, but a TransferEncoding header with value "chunked". We _should_ allow any other Content headers to stick around.
            //
            // The .NET http client is a bit weird about attaching the Content-Length header though. If you HAVE the .Content property defined, a Content-Length
            // header WILL be added. This is due to the fact that on send, the client considers a populated Client property as having a body, even if it's zero length.
            if (incomingRequest.ContentLength == null)
            {
                if(!incomingRequest.Headers["Transfer-Encoding"].ToString().Split(' ').Select(x => x.Trim()).Contains("chunked"))
                {
                    upstreamRequest.Content = null;
                }
            }

            if (HandleRedirects)
            {
                upstreamResponse = await (session.Client ?? RedirectableClient).SendAsync(upstreamRequest).ConfigureAwait(false);
            }
            else
            {
                upstreamResponse = await (session.Client ?? RedirectlessClient).SendAsync(upstreamRequest).ConfigureAwait(false);
            }

            byte[] body = Array.Empty<byte>();

            // HEAD requests do NOT have a body regardless of the value of the Content-Length header
            if (incomingRequest.Method.ToUpperInvariant() != "HEAD")
            {
                body = CompressionUtilities.DecompressBody((MemoryStream)await upstreamResponse.Content.ReadAsStreamAsync().ConfigureAwait(false), upstreamResponse.Content.Headers);
            }

            entry.Response.Body = body.Length == 0 ? null : body;
            entry.StatusCode = (int)upstreamResponse.StatusCode;

            EntryRecordMode mode = GetRecordMode(incomingRequest);

            if (mode != EntryRecordMode.DontRecord)
            {
                lock (session.Session.Entries)
                {
                    session.Session.Entries.Add(entry);
                }

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
                var bodyData = CompressionUtilities.CompressBody(entry.Response.Body, entry.Response.Headers);

                if (entry.Response.Headers.ContainsKey("Content-Length")){
                    outgoingResponse.ContentLength = bodyData.Length;
                }
                await outgoingResponse.Body.WriteAsync(bodyData).ConfigureAwait(false);
            }
        }

        public static EntryRecordMode GetRecordMode(HttpRequest request)
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

        public HttpRequestMessage CreateUpstreamRequest(HttpRequest incomingRequest, byte[] incomingBody)
        {
            var upstreamRequest = new HttpRequestMessage();
            upstreamRequest.RequestUri = GetRequestUri(incomingRequest);
            upstreamRequest.Method = new HttpMethod(incomingRequest.Method);
            upstreamRequest.Content = new ReadOnlyMemoryContent(incomingBody);

            foreach (var header in incomingRequest.Headers)
            {
                IEnumerable<string> values = header.Value;

                // can't handle PROXY_CONNECTION right now.
                if (s_excludedRequestHeaders.Contains(header.Key, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!header.Key.StartsWith("x-recording"))
                {
                    if (upstreamRequest.Headers.TryAddWithoutValidation(header.Key, values))
                    {
                        continue;
                    }

                    if (!upstreamRequest.Content.Headers.TryAddWithoutValidation(header.Key, values))
                    {
                        throw new HttpException(
                            HttpStatusCode.BadRequest,
                            $"Encountered an unexpected exception while mapping a content header during upstreamRequest creation. Header: \"{header.Key}\". Value: \"{String.Join(",", values)}\""
                        );
                    }
                }

                if (header.Key == "x-recording-upstream-host-header")
                {
                    upstreamRequest.Headers.Host = header.Value;
                }
            }

            return upstreamRequest;
        }

        #endregion

        #region playback functionality
        public async Task StartPlaybackAsync(string sessionId, HttpResponse outgoingResponse, RecordingType mode = RecordingType.FilePersisted, string assetsPath = null)
        {
            var id = Guid.NewGuid().ToString();
            DebugLogger.LogTrace($"PLAYBACK START BEGIN {id}.");

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
                await RestoreAssetsJson(assetsPath, true);
                var path = await GetRecordingPath(sessionId, assetsPath);
                var base64path = Convert.ToBase64String(Encoding.UTF8.GetBytes(path));
                outgoingResponse.Headers.Add("x-base64-recording-file-location", base64path);
                if (!File.Exists(path))
                {
                    throw new TestRecordingMismatchException($"Recording file path {path} does not exist.");
                }

                using var stream = System.IO.File.OpenRead(path);
                using var doc = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
                session = new ModifiableRecordSession(RecordSession.Deserialize(doc.RootElement))
                {
                    Path = path
                };
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

            DebugLogger.LogTrace($"PLAYBACK START END {id}.");
        }

        public void StopPlayback(string recordingId, bool purgeMemoryStore = false)
        {

            var id = Guid.NewGuid().ToString();
            DebugLogger.LogTrace($"PLAYBACK STOP BEGIN {id}.");

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

            DebugLogger.LogTrace($"PLAYBACK STOP END {id}.");
        }

        public async Task HandlePlaybackRequest(string recordingId, HttpRequest incomingRequest, HttpResponse outgoingResponse)
        {
            if (!PlaybackSessions.TryGetValue(recordingId, out var session))
            {
                throw new HttpException(HttpStatusCode.BadRequest, $"There is no active playback session under recording id {recordingId}.");
            }

            var sanitizers = session.AdditionalSanitizers.Count > 0 ? Sanitizers.Concat(session.AdditionalSanitizers) : Sanitizers;

            DebugLogger.LogRequestDetails(incomingRequest, sanitizers);

            var entry = (await CreateEntryAsync(incomingRequest).ConfigureAwait(false)).Item1;

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
                transform.Transform(incomingRequest, match);
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
                var bodyData = CompressionUtilities.CompressBody(match.Response.Body, match.Response.Headers);

                if (match.Response.Headers.ContainsKey("Content-Length"))
                {
                    outgoingResponse.ContentLength = bodyData.Length;
                }

                await WriteBodyBytes(bodyData, session.PlaybackResponseTime, outgoingResponse);
            }
        }

        public byte[][] GetBatches(byte[] bodyData, int batchCount)
        {
            if (bodyData.Length == 0 || bodyData.Length < batchCount)
            {
                var result = new byte[1][];
                result[0] = bodyData;

                return result;
            }

            int chunkLength = bodyData.Length / batchCount;
            int remainder = (bodyData.Length % batchCount);
            var batches = new byte[batchCount + (remainder > 0 ? 1 : 0)][];

            for(int i = 0; i < batches.Length; i++)
            {
                var calculatedChunkLength = ((i == batches.Length - 1) && (batches.Length > 1) && (remainder > 0)) ? remainder : chunkLength;
                var batch = new byte[calculatedChunkLength];
                Array.Copy(bodyData, i * chunkLength, batch, 0, calculatedChunkLength);

                batches[i] = batch;
            }

            return batches;
        }

        public async Task WriteBodyBytes(byte[] bodyData, int playbackResponseTime, HttpResponse outgoingResponse)
        {
            if (playbackResponseTime > 0)
            {
                int batchCount = 10;
                int sleepLength = playbackResponseTime / batchCount;

                byte[][] chunks = GetBatches(bodyData, batchCount);

                for(int i = 0; i < chunks.Length; i++)
                {
                    var chunk = chunks[i];

                    await outgoingResponse.Body.WriteAsync(chunk).ConfigureAwait(false);

                    if (i != chunks.Length - 1)
                    {
                        await Task.Delay(sleepLength);
                    }
                }

            }
            else
            {
                await outgoingResponse.Body.WriteAsync(bodyData).ConfigureAwait(false);
            }
        }

        public static async Task<(RecordEntry, byte[])> CreateEntryAsync(HttpRequest request)
        {
            var entry = CreateNoBodyRecordEntry(request);

            byte[] bytes = await ReadAllBytes(request.Body).ConfigureAwait(false);
            entry.Request.Body = CompressionUtilities.DecompressBody(bytes, request.Headers);

            return (entry, bytes);
        }

        public static RecordEntry CreateNoBodyRecordEntry(HttpRequest request)
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

            return entry;
        }

        #endregion

        #region SetRecordingOptions and store functionality
        public static string GetAssetsJsonLocation(string pathToAssetsJson, string contextDirectory)
        {
            if (pathToAssetsJson == null)
            {
                return null;
            }

            var path = pathToAssetsJson;

            if (!Path.IsPathFullyQualified(pathToAssetsJson))
            {
                path = Path.Join(contextDirectory, pathToAssetsJson);
            }

            return path.Replace("\\", "/");
        }

        public async Task Restore(string pathToAssetsJson)
        {
            var resultingPath = await Store.Restore(pathToAssetsJson);
            ContextDirectory = resultingPath;
        }

        public void SetRecordingOptions(IDictionary<string, object> options = null, string sessionId = null)
        {
            if (options != null)
            {
                if (options.Keys.Count == 0)
                {
                    throw new HttpException(HttpStatusCode.BadRequest, "At least one key is expected in the body being passed to SetRecordingOptions.");
                }

                if (options.TryGetValue("HandleRedirects", out var handleRedirectsObj))
                {
                    var handleRedirectsString = $"{handleRedirectsObj}";

                    if (bool.TryParse(handleRedirectsString, out var handleRedirectsBool))
                    {
                        HandleRedirects = handleRedirectsBool;
                    }
                    else if (handleRedirectsString.Equals("0", StringComparison.OrdinalIgnoreCase))
                    {
                        HandleRedirects = false;
                    }
                    else if (handleRedirectsString.Equals("1", StringComparison.OrdinalIgnoreCase))
                    {
                        HandleRedirects = true;
                    }
                    else
                    {
                        throw new HttpException(HttpStatusCode.BadRequest, $"The value of key \"HandleRedirects\" MUST be castable to a valid boolean value. Unparsable Value: \"{handleRedirectsString}\".");
                    }
                }

                if (options.TryGetValue("ContextDirectory", out var sourceDirectoryObj))
                {
                    var newSourceDirectory = sourceDirectoryObj.ToString();

                    if (!string.IsNullOrWhiteSpace(newSourceDirectory))
                    {
                        SetRecordingDirectory(newSourceDirectory);
                    }
                    else
                    {
                        throw new HttpException(HttpStatusCode.BadRequest, "Users must provide a valid value to the key \"ContextDirectory\" in the recording options dictionary.");
                    }
                }

                if (options.TryGetValue("AssetsStore", out var assetsStoreObj))
                {
                    var newAssetsStoreIdentifier = assetsStoreObj.ToString();

                    if (!string.IsNullOrWhiteSpace(newAssetsStoreIdentifier))
                    {
                        SetAssetsStore(newAssetsStoreIdentifier);
                    }
                    else
                    {
                        throw new HttpException(HttpStatusCode.BadRequest, "Users must provide a valid value when providing the key \"AssetsStore\" in the recording options dictionary.");
                    }
                }

                if (options.TryGetValue("Transport", out var transportConventions))
                {
                    if (transportConventions != null)
                    {
                        try
                        {
                            string transportObject;
                            if (transportConventions is JsonElement je)
                            {
                                transportObject = je.ToString();
                            }
                            else
                            {
                                throw new Exception("'Transport' object was not a JsonElement");
                            }

                            var serializerOptions = new JsonSerializerOptions
                            {
                                ReadCommentHandling = JsonCommentHandling.Skip,
                                AllowTrailingCommas = true,
                            };
                            var customizations = JsonSerializer.Deserialize<TransportCustomizations>(transportObject, serializerOptions);

                            SetTransportOptions(customizations, sessionId);
                        }
                        catch (HttpException)
                        {
                            throw;
                        }
                        catch (Exception e)
                        {
                            throw new HttpException(HttpStatusCode.BadRequest, $"Unable to deserialize the contents of the \"Transport\" key. Visible object: {transportConventions}. Json Deserialization Error: {e.Message}");
                        }
                    }
                    else
                    {
                        throw new HttpException(HttpStatusCode.BadRequest, "Users must provide a valid value when providing the key \"Transport\" in the recording options dictionary.");
                    }
                }
            }
            else
            {
                throw new HttpException(HttpStatusCode.BadRequest, "When setting recording options, the request body is expected to be non-null and of type Dictionary<string, string>.");
            }
        }

        public X509Certificate2 GetValidationCert(TransportCustomizations settings)
        {
            try
            {
                var span = new ReadOnlySpan<char>(settings.TLSValidationCert.ToCharArray());
                return PemReader.LoadCertificate(span, null, PemReader.KeyType.Auto, true);
            }
            catch (Exception e)
            {
                throw new HttpException(HttpStatusCode.BadRequest, $"Unable to instantiate a valid cert from the value provided in Transport settings key \"TLSValidationCert\". Value: \"{settings.TLSValidationCert}\". Message: \"{e.Message}\".");
            }
        }

        public HttpClientHandler GetTransport(bool allowAutoRedirect, TransportCustomizations customizations, bool insecure = false)
        {
            var clientHandler = new HttpClientHandler()
            {
                AllowAutoRedirect = allowAutoRedirect
            };

            if (customizations.Certificates != null)
            {
                foreach (var certPair in customizations.Certificates)
                {
                    try
                    {

                        var cert = X509Certificate2.CreateFromPem(certPair.PemValue, certPair.PemKey);
                        cert = new X509Certificate2(cert.Export(X509ContentType.Pfx));
                        clientHandler.ClientCertificates.Add(cert);
                    }
                    catch (Exception e)
                    {
                        throw new HttpException(HttpStatusCode.BadRequest, $"Unable to instantiate a new X509 certificate from the provided value and key. Failure Message: \"{e.Message}\".");
                    }
                }
            }

            if (customizations.TLSValidationCert != null && !insecure)
            {
                var ledgerCert = GetValidationCert(customizations);

                X509Chain certificateChain = new();
                certificateChain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                certificateChain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
                certificateChain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
                certificateChain.ChainPolicy.VerificationTime = DateTime.Now;
                certificateChain.ChainPolicy.UrlRetrievalTimeout = new TimeSpan(0, 0, 0);
                certificateChain.ChainPolicy.ExtraStore.Add(ledgerCert);

                clientHandler.ServerCertificateCustomValidationCallback = (HttpRequestMessage httpRequestMessage, X509Certificate2 cert, X509Chain x509Chain, SslPolicyErrors sslPolicyErrors) =>
                {
                    if (!string.IsNullOrWhiteSpace(customizations.TSLValidationCertHost) && httpRequestMessage.RequestUri.Host != customizations.TSLValidationCertHost)
                    {
                        if (sslPolicyErrors == SslPolicyErrors.None)
                        {
                            return true;
                        }

                        return false;
                    }
                    else
                    {
                        bool isChainValid = certificateChain.Build(cert);
                        if (!isChainValid) return false;
                        var isCertSignedByTheTlsCert = certificateChain.ChainElements.Cast<X509ChainElement>()
                            .Any(x => x.Certificate.Thumbprint == ledgerCert.Thumbprint);

                        return isCertSignedByTheTlsCert;
                    }
                };
            }
            else if (insecure)
            {
                clientHandler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
            }

            return clientHandler;
        }

        public void SetTransportOptions(TransportCustomizations customizations, string sessionId)
        {
            var timeoutSpan = TimeSpan.FromSeconds(600);

            // this will look a bit strange until we take care of #3488 due to the fact that this AllowAutoRedirect customizable from two places
            if (!string.IsNullOrWhiteSpace(sessionId))
            {
                var customizedClientHandler = GetTransport(customizations.AllowAutoRedirect, customizations);

                if (RecordingSessions.TryGetValue(sessionId, out var recordingSession))
                {
                    recordingSession.Client = new HttpClient(customizedClientHandler)
                    {
                        Timeout = timeoutSpan
                    };
                }

                if (customizations.PlaybackResponseTime > 0)
                {
                    if (PlaybackSessions.TryGetValue(sessionId, out var playbackSession))
                    {
                        playbackSession.PlaybackResponseTime = customizations.PlaybackResponseTime;
                    }
                    else
                    {
                        throw new HttpException(HttpStatusCode.BadRequest, $"Unable to set a transport customization on a recording session that is not active. Id: \"{sessionId}\"");
                    }
                }
            }
            else
            {
                // after #3488 we will swap to a single client instead of both of these
                var redirectableCustomizedHandler = GetTransport(true, customizations, Startup.Insecure);
                var redirectlessCustomizedHandler = GetTransport(false, customizations, Startup.Insecure);

                RedirectableClient = new HttpClient(redirectableCustomizedHandler)
                {
                    Timeout = timeoutSpan
                };

                RedirectlessClient = new HttpClient(redirectlessCustomizedHandler)
                {
                    Timeout = timeoutSpan
                };
            }
        }

        public void SetAssetsStore(string assetsStoreId)
        {
            Store = Resolver.ResolveStore(assetsStoreId);
        }

        public void SetRecordingDirectory(string targetDirectory)
        {
            try
            {
                // Given that it is perfectly valid to pass a directory that does not yet exist, we cannot get the file attributes to "properly"
                // determine if an incoming path is a valid one via <attr>.HasFlag(FileAttributes.Directory). We can shorthand this by checking
                // for a file extension.
                if (Path.GetExtension(targetDirectory) != String.Empty)
                {
                    targetDirectory = Path.GetDirectoryName(targetDirectory);
                }

                if (!String.IsNullOrEmpty(targetDirectory))
                {
                    Directory.CreateDirectory(targetDirectory);
                }
                ContextDirectory = targetDirectory;
            }
            catch (Exception ex)
            {
                throw new HttpException(HttpStatusCode.BadRequest, $"Unable set proxy context to target directory \"{targetDirectory}\". Unhandled exception was: \"{ex.Message}\".");
            }
        }
        #endregion

        #region utility and common-use functions
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
                lock (recordingSession)
                {
                    recordingSession.AdditionalSanitizers.Add(sanitizer);
                }
            }

            if (InMemorySessions.TryGetValue(recordingId, out var inMemSession))
            {
                lock (inMemSession)
                {
                    inMemSession.AdditionalSanitizers.Add(sanitizer);
                }
            }

            if (inMemSession == null && recordingSession == null && playbackSession == null)
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
                    recordSession.ResetExtensions();
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

                    if (countPlayback > 0)
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
                    new RecordedTestSanitizer(),
                    new BodyKeySanitizer("$..access_token"),
                    new BodyKeySanitizer("$..refresh_token")
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

                RedirectableClient = BaseRedirectableClient;
                RedirectlessClient = BaseRedirectlessClient;
            }
        }

        public async Task<string> GetRecordingPath(string file, string assetsPath = null)
        {
            var normalizedFileName = file.Replace('\\', '/');

            if (String.IsNullOrWhiteSpace(file))
            {
                throw new HttpException(HttpStatusCode.BadRequest, $"Recording file value of {file} is invalid. Try again with a populated filename.");
            }

            var path = file;

            // if an assets.json is provided, we have a bit of work to do here.
            if (!string.IsNullOrWhiteSpace(assetsPath))
            {
                var contextDirectory = await Store.GetPath(assetsPath);

                path = Path.Join(contextDirectory, file);
            }
            // otherwise, it's a basic restore like we're used to
            else
            {
                if (!Path.IsPathFullyQualified(file))
                {
                    path = Path.Join(ContextDirectory, file);
                }
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

            // it is easy to forget the x-recording-upstream-base-uri value
            if (string.IsNullOrWhiteSpace(hostValue))
            {
                throw new HttpException(HttpStatusCode.BadRequest, $"The value present in header 'x-recording-upstream-base-uri' is not a valid hostname: {hostValue}.");
            }

            // The host value from the header should include scheme and port. EG:
            //    https://portal.azure.com/
            //    http://localhost:8080
            //    http://user:pass@localhost:8080/ <-- this should be EXTREMELY rare given it's extremely insecure
            // 
            // The value from rawTarget is the _exact_ "rest of the URI" WITHOUT auto-decoding (as specified above) and could look like:
            //    ///request
            //    /hello/world?query=blah
            //    ""
            //    //hello/world
            //
            // We cannot use a URIBuilder to combine the hostValue and the rawTarget, as doing so results in auto-decoding of escaped 
            // characters that will BREAK the request that we actually wish to make.
            //
            // Given these limitations, and safe in the knowledge of both sides of this operation. We trim the trailing / off of the host,
            // and string concatenate them together.
            var rawUri = hostValue.TrimEnd('/') + rawTarget;
            return new Uri(rawUri);
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
