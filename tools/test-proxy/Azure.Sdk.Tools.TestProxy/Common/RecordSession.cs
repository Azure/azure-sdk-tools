// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

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
                sanitizer.Sanitize(requestEntry);
            }
            // normalize request body with STJ using relaxed escaping to match behavior when Deserializing from session files
            RecordEntry.NormalizeJsonBody(requestEntry.Request);


            RecordEntry entry = matcher.FindMatch(requestEntry, Entries);
            if (remove)
            {
                Entries.Remove(entry);
                DebugLogger.LogDebug($"We successfully matched and popped request URI {entry.RequestUri} for recordingId {sessionId??"Unknown"}");
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
                sanitizer.Sanitize(this);
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
                    sanitizer.Sanitize(this);
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
    }
}
