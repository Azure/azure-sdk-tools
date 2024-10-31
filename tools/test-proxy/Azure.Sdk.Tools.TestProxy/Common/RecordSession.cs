// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Azure.Sdk.Tools.TestProxy.Common
{
    public class RecordSession
    {
        internal const string DateTimeOffsetNowVariableKey = "DateTimeOffsetNow";

        public List<RecordEntry> Entries { get; } = new List<RecordEntry>();

        public SortedDictionary<string, string> Variables { get; } = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        //Used only for deserializing track 1 session record files
        public Dictionary<string, Queue<string>> Names { get; set; } = new Dictionary<string, Queue<string>>();

        public SemaphoreSlim EntryLock { get; set; } = new SemaphoreSlim(1);

        public void Serialize(Utf8JsonWriter jsonWriter)
        {
            jsonWriter.WriteStartObject();
            jsonWriter.WriteStartArray(nameof(Entries));
            foreach (RecordEntry record in Entries)
            {
                record.Serialize(jsonWriter);
            }
            jsonWriter.WriteEndArray();

            jsonWriter.WriteStartObject(nameof(Variables));
            foreach (KeyValuePair<string, string> variable in Variables)
            {
                jsonWriter.WriteString(variable.Key, variable.Value);
            }
            jsonWriter.WriteEndObject();

            jsonWriter.WriteEndObject();
        }

        public static RecordSession Deserialize(JsonElement element)
        {
            var session = new RecordSession();
            if (element.TryGetProperty(nameof(Entries), out JsonElement property))
            {
                foreach (JsonElement item in property.EnumerateArray())
                {
                    session.Entries.Add(RecordEntry.Deserialize(item));
                }
            }

            if (element.TryGetProperty(nameof(Variables), out property))
            {
                foreach (JsonProperty item in property.EnumerateObject())
                {
                    session.Variables[item.Name] = item.Value.GetString();
                }
            }

            if (element.TryGetProperty(nameof(Names), out property))
            {
                foreach (JsonProperty item in property.EnumerateObject())
                {
                    var queue = new Queue<string>();
                    foreach (JsonElement subItem in item.Value.EnumerateArray())
                    {
                        queue.Enqueue(subItem.GetString());
                    }
                    session.Names[item.Name] = queue;
                }
            }
            return session;
        }

        public async Task Record(RecordEntry entry, bool shouldLock = true)
        {
            if (shouldLock)
            {
                await EntryLock.WaitAsync();
            }

            try
            {
                Entries.Add(entry);
            }
            finally
            {
                if (shouldLock)
                {
                    EntryLock.Release();
                }
            }
        }

        public RecordEntry Lookup(RecordEntry requestEntry, RecordMatcher matcher, IEnumerable<RecordedTestSanitizer> sanitizers, bool remove = true, string sessionId = null)
        {
            foreach (RecordedTestSanitizer sanitizer in sanitizers)
            {
                RecordEntry reqEntryPreSanitize = null;
                if (DebugLogger.CheckLogLevel(LogLevel.Debug))
                {
                    reqEntryPreSanitize = requestEntry.Clone();
                }

                sanitizer.Sanitize(requestEntry);

                if (DebugLogger.CheckLogLevel(LogLevel.Debug))
                {
                    LogSanitizerModification(sanitizer.SanitizerId, reqEntryPreSanitize, requestEntry);
                }
            }

            // normalize request body with STJ using relaxed escaping to match behavior when Deserializing from session files
            RecordEntry.NormalizeJsonBody(requestEntry.Request);

            RecordEntry entry = matcher.FindMatch(requestEntry, Entries);
            if (remove)
            {
                Entries.Remove(entry);
                DebugLogger.LogDebug($"We successfully matched and popped request URI {entry.RequestUri} for recordingId {sessionId ?? "Unknown"}");
            }

            return entry;
        }

        public async Task Remove(RecordEntry entry, bool shouldLock = true)
        {
            if (shouldLock)
            {
                await EntryLock.WaitAsync();
            }

            try
            {
                Entries.Remove(entry);
            }
            finally
            {
                if (shouldLock)
                {
                    EntryLock.Release();
                }
            }
        }

        public async Task Sanitize(RecordedTestSanitizer sanitizer, bool shouldLock = true)
        {
            if (shouldLock)
            {
                await EntryLock.WaitAsync();
            }

            try
            {
                var entriesPreSanitize = Array.Empty<RecordEntry>();
                if (DebugLogger.CheckLogLevel(LogLevel.Debug))
                {
                    entriesPreSanitize = this.Entries.Select(requestEntry => requestEntry.Clone()).ToArray();
                }

                sanitizer.Sanitize(this);

                if (DebugLogger.CheckLogLevel(LogLevel.Debug))
                {
                    if (entriesPreSanitize.Length > this.Entries.Count)
                    {
                        DebugLogger.LogDebug(GetSanitizerInfoLogPrefix(sanitizer.SanitizerId) + " has removed some entries");
                    }
                    else if (entriesPreSanitize.Length < this.Entries.Count)
                    {
                        throw new Exception("Something went wrong. The number of entries increased after sanitization with " + GetSanitizerInfoLogPrefix(sanitizer.SanitizerId));
                    }
                    else
                    {
                        for (int i = 0; i < entriesPreSanitize.Length; i++)
                        {

                            LogSanitizerModification(sanitizer.SanitizerId, entriesPreSanitize[i], this.Entries[i]);

                        }
                    }
                }
            }
            finally
            {
                if (shouldLock)
                {
                    EntryLock.Release();
                }
            }
        }

        public async Task Sanitize(IEnumerable<RecordedTestSanitizer> sanitizers, bool shouldLock = true)
        {
            if (shouldLock)
            {
                await EntryLock.WaitAsync();
            }

            try
            {
                foreach (var sanitizer in sanitizers)
                {
                    var entriesPreSanitize = Array.Empty<RecordEntry>();
                    if (DebugLogger.CheckLogLevel(LogLevel.Debug))
                    {
                        entriesPreSanitize = this.Entries.Select(requestEntry => requestEntry.Clone()).ToArray();
                    }

                    sanitizer.Sanitize(this);

                    if (DebugLogger.CheckLogLevel(LogLevel.Debug))
                    {
                        if (entriesPreSanitize.Length > this.Entries.Count)
                        {
                            DebugLogger.LogDebug(GetSanitizerInfoLogPrefix(sanitizer.SanitizerId) + " has removed some entries");
                        }
                        else if (entriesPreSanitize.Length < this.Entries.Count)
                        {
                            throw new Exception("Something went wrong. The number of entries increased after sanitization with " + GetSanitizerInfoLogPrefix(sanitizer.SanitizerId));
                        }
                        else
                        {
                            for (int i = 0; i < entriesPreSanitize.Length; i++)
                            {
                                LogSanitizerModification(sanitizer.SanitizerId, entriesPreSanitize[i], this.Entries[i]);
                            }
                        }
                    }
                }
            }
            finally
            {
                if (shouldLock)
                {
                    EntryLock.Release();
                }
            }
        }

        public string GetSanitizerInfoLogPrefix(string sanitizerId)
        {
            return (sanitizerId != null && sanitizerId.StartsWith("AZSDK") ? "Central sanitizer" : "User specified") + " rule " + sanitizerId;
        }

        /// <summary>
        /// Logs the modifications made by a sanitizer to a record entry.
        /// </summary>
        /// <param name="sanitizerId">The ID of the sanitizer.</param>
        /// <param name="entryPreSanitize">The record entry before sanitization.</param>
        /// <param name="entryPostSanitize">The record entry after sanitization.</param>
        private void LogSanitizerModification(string sanitizerId, RecordEntry entryPreSanitize, RecordEntry entryPostSanitize)
        {
            bool isRequestUriModified = entryPreSanitize.RequestUri != entryPostSanitize.RequestUri;
            bool areRequestHeadersModified = AreHeadersModified(entryPreSanitize.Request.Headers, entryPostSanitize.Request.Headers);
            bool isRequestBodyModified = IsBodyModified(entryPreSanitize.Request.Body, entryPostSanitize.Request.Body);
            bool areResponseHeadersModified = AreHeadersModified(entryPreSanitize.Response.Headers, entryPostSanitize.Response.Headers);
            bool isResponseBodyModified = IsBodyModified(entryPreSanitize.Response.Body, entryPostSanitize.Response.Body);

            bool isRecordModified = isRequestUriModified || areRequestHeadersModified || isRequestBodyModified || areResponseHeadersModified || isResponseBodyModified;
            if (!isRecordModified)
            {
                return;
            }

            StringBuilder logMessage = new StringBuilder();
            logMessage.AppendLine(GetSanitizerInfoLogPrefix(sanitizerId) + " modified the entry");

            var before = $"{Environment.NewLine}before:{Environment.NewLine} ";
            var after = $"{Environment.NewLine}after: {Environment.NewLine} ";

            if (isRequestUriModified)
            {
                logMessage.AppendLine($"RequestUri is modified{before}{entryPreSanitize.RequestUri}{after}{entryPostSanitize.RequestUri}");
            }

            void LogBodyModification(RequestOrResponse pre, RequestOrResponse post, bool isRequest)
            {
                if (pre.Body != null && pre.TryGetBodyAsText(out string bodyTextPre) &&
                    post.Body != null && post.TryGetBodyAsText(out string bodyTextPost) &&
                    !string.IsNullOrWhiteSpace(bodyTextPre) &&
                    !string.IsNullOrWhiteSpace(bodyTextPost))
                {
                    logMessage.AppendLine($"{(isRequest ? "Request" : "Response")} Body is modified{before}{bodyTextPre}{after}{bodyTextPost}");
                }
            }
            if (isRequestBodyModified)
            {
                LogBodyModification(entryPreSanitize.Request, entryPostSanitize.Request, true);
            }
            if (isResponseBodyModified)
            {
                LogBodyModification(entryPreSanitize.Response, entryPostSanitize.Response, false);
            }

            void LogHeadersModification(RequestOrResponse pre, RequestOrResponse post, bool isRequest)
            {
                logMessage.AppendLine($"{(isRequest ? "Request" : "Response")} Headers are modified{before}{HeadersAsString(pre.Headers)}{after}{HeadersAsString(post.Headers)}");
            }
            if (areRequestHeadersModified)
            {
                LogHeadersModification(entryPreSanitize.Request, entryPostSanitize.Request, true);
            }
            if (areResponseHeadersModified)
            {
                LogHeadersModification(entryPreSanitize.Response, entryPostSanitize.Response, false);
            }

            DebugLogger.LogDebug(logMessage.ToString());
        }

        /// <summary>
        /// Checks if the headers in two dictionaries are modified.
        /// </summary>
        /// <param name="dict1">The first dictionary of headers.</param>
        /// <param name="dict2">The second dictionary of headers.</param>
        /// <returns>True if the headers are modified, otherwise false.</returns>
        private bool AreHeadersModified(SortedDictionary<string, string[]> dict1, SortedDictionary<string, string[]> dict2)
        {
            if (dict1 == null || dict2 == null)
                return !(dict1 == dict2);

            if (dict1.Count != dict2.Count)
                return true;

            return !dict1.All(kvp => dict2.TryGetValue(kvp.Key, out var value) && kvp.Value.SequenceEqual(value));
        }

        /// <summary>
        /// Checks if the body content has been modified.
        /// </summary>
        /// <param name="preBody">The body content before modification.</param>
        /// <param name="postBody">The body content after modification.</param>
        /// <returns>True if the body content is modified, otherwise false.</returns>
        private bool IsBodyModified(byte[] preBody, byte[] postBody)
        {
            // Treat null and empty arrays as the same
            if ((preBody == null || preBody.Length == 0) && (postBody == null || postBody.Length == 0))
            {
                return false;
            }

            // If one is null or empty and the other is not, they are different
            if ((preBody == null || preBody.Length == 0) || (postBody == null || postBody.Length == 0))
            {
                return true;
            }

            // If lengths are different, they are different
            if (preBody.Length != postBody.Length)
            {
                return true;
            }

            // Compare byte by byte
            for (int i = 0; i < preBody.Length; i++)
            {
                if (preBody[i] != postBody[i])
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Converts the headers dictionary to a string representation.
        /// </summary>
        /// <param name="sortedDict">The dictionary of headers.</param>
        /// <returns>A string representation of the headers.</returns>
        private string HeadersAsString(SortedDictionary<string, string[]> sortedDict)
        {
            return string.Join(Environment.NewLine + " ", sortedDict.Select(kvp => $"{kvp.Key}: {string.Join(", ", kvp.Value)}"));
        }
    }
}
