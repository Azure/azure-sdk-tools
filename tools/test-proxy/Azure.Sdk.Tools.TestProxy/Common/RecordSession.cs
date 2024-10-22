// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

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
                    requestEntry.initializeIsModifiedFlag();
                    reqEntryPreSanitize = CopyRecordEntry(requestEntry);
                }

                sanitizer.Sanitize(requestEntry);

                if (DebugLogger.CheckLogLevel(LogLevel.Debug) && requestEntry.isModified())
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
                    entriesPreSanitize = this.Entries.Select(requestEntry => { requestEntry.initializeIsModifiedFlag(); return CopyRecordEntry(requestEntry); }).ToArray();
                }
                
                sanitizer.Sanitize(this);

                if (DebugLogger.CheckLogLevel(LogLevel.Debug))
                {
                    for (int i = 0; i < entriesPreSanitize.Length; i++)
                    {
                        if (this.Entries[i].isModified()) LogSanitizerModification(sanitizer.SanitizerId, entriesPreSanitize[i], this.Entries[i]);
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
                        entriesPreSanitize = this.Entries.Select(requestEntry => { requestEntry.initializeIsModifiedFlag(); return CopyRecordEntry(requestEntry); }).ToArray();
                    }
                    
                    sanitizer.Sanitize(this);

                    if (DebugLogger.CheckLogLevel(LogLevel.Debug))
                    {
                        for (int i = 0; i < entriesPreSanitize.Length; i++)
                        {
                            if (this.Entries[i].isModified()) LogSanitizerModification(sanitizer.SanitizerId, entriesPreSanitize[i], this.Entries[i]);
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

        /// <summary>
        /// Creates a copy of the provided record entry.
        /// Used primarily for sanitization logging.
        /// </summary>
        /// <param name="requestEntry">The record entry to copy.</param>
        /// <returns>The copied record entry.</returns>
        private RecordEntry CopyRecordEntry(RecordEntry requestEntry)
        {
            // Create a copy of the record entry
            var copiedRecordEntry = new RecordEntry();
            copiedRecordEntry.RequestUri = requestEntry.RequestUri;
            
            copiedRecordEntry.Request = new RequestOrResponse();
            copiedRecordEntry.Request.Headers = new SortedDictionary<string, string[]>(requestEntry.Request.Headers.ToDictionary(kvp => kvp.Key, kvp => (string[])kvp.Value.Clone()));
            copiedRecordEntry.Request.Body = requestEntry.Response.Body != null ? (byte[])requestEntry.Request.Body.Clone(): null;
            
            copiedRecordEntry.Response = new RequestOrResponse();
            copiedRecordEntry.Response.Headers = new SortedDictionary<string, string[]>(requestEntry.Response.Headers.ToDictionary(kvp => kvp.Key, kvp => (string[])kvp.Value.Clone()));
            copiedRecordEntry.Response.Body = requestEntry.Response.Body != null ? (byte[])requestEntry.Response.Body.Clone() : null;
            return copiedRecordEntry;
        }

        /// <summary>
        /// Logs the modifications made by a sanitizer to a record entry.
        /// </summary>
        /// <param name="sanitizerId">The ID of the sanitizer.</param>
        /// <param name="entryPreSanitize">The record entry before sanitization.</param>
        /// <param name="entryPostSanitize">The record entry after sanitization.</param>
        private void LogSanitizerModification(string sanitizerId, RecordEntry entryPreSanitize, RecordEntry entryPostSanitize)
        {
            DebugLogger.LogDebug((sanitizerId!= null && sanitizerId.StartsWith("AZSDK") ? $"Central sanitizer " : "User specified ") + $"rule {sanitizerId} modified the entry");
            DebugLogger.LogDebug("lalalala");
            if (entryPostSanitize.requestUriIsModified)
            {
                DebugLogger.LogDebug($"RequestUri is modified\nbefore:\n {entryPreSanitize.RequestUri}\nafter:\n {entryPostSanitize.RequestUri}");
            }

            if (entryPostSanitize.Request.IsModified.Headers)
            {
                DebugLogger.LogDebug($"Request Headers are modified\nbefore:\n {entryPreSanitize.Request.Headers}\nafter:\n {entryPostSanitize.Request.Headers}");
            }

            if (entryPostSanitize.Response.IsModified.Headers)
            {
                DebugLogger.LogDebug($"Response Headers are modified\nbefore:\n {entryPreSanitize.Response.Headers}\nafter:\n {entryPostSanitize.Response.Headers}");
            }

            if (entryPostSanitize.Request.IsModified.Body)
            {
                DebugLogger.LogDebug($"Request Body is modified\nbefore:\n {entryPreSanitize.Request.Body}\nafter:\n {entryPostSanitize.Request.Body}");
            }

            if (entryPostSanitize.Response.IsModified.Body)
            {
                DebugLogger.LogDebug($"Response Body is modified\nbefore:\n {entryPreSanitize.Response.Body}\nafter:\n {entryPostSanitize.Response.Body}");
            }
        }
    }
}
