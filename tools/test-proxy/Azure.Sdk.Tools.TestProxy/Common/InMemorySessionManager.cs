using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.TestProxy.Common
{
    public class InMemorySessionManager
    {
        // in recording progress
        public readonly ConcurrentDictionary<string, RecordSession> i_sessions
            = new ConcurrentDictionary<string, RecordSession>();

        // completed
        public readonly ConcurrentDictionary<string, RecordSession> c_sessions
            = new ConcurrentDictionary<string, RecordSession>();

        // in playback progress
        public readonly ConcurrentDictionary<string, RecordSession> p_sessions
            = new ConcurrentDictionary<string, RecordSession>();

        private static readonly RecordedTestSanitizer s_sanitizer = new RecordedTestSanitizer();

        // only ever should be invoked once, as this class is registered as a singleton in Startup.cs
        public InMemorySessionManager()
        {
            this.LoadSessions();
        }


        public string StartRecording()
        {
            var id = Guid.NewGuid().ToString();
            if (!i_sessions.TryAdd(id, new RecordSession()))
            {
                // This should not happen as the key is a new GUID.
                throw new InvalidOperationException("Failed to add new session.");
            }

            return id;
        }

        public void StopRecording(string id)
        {
            if (!i_sessions.TryRemove(id, out var completedSession)){
                throw new InvalidOperationException($"Failed to complete session {id}");
            }

            if (!c_sessions.TryAdd(id, completedSession))
            {
                // This should not happen as the key is a new GUID.
                throw new InvalidOperationException("Failed to add new session.");
            }
        }

        public void UpdateRecording(string id, RecordEntry newEntry)
        {
            if (!i_sessions.TryGetValue(id, out var session))
            {
                throw new InvalidOperationException("No recording loaded with that ID.");
            }

            session.Entries.Add(newEntry);
        }


        public RecordSession GetRecording(string id)
        {
            if (!c_sessions.TryGetValue(id, out var session))
            {
                throw new InvalidOperationException("No recording loaded with that ID.");
            }

            return session;
        }

        /// <summary>
        /// Saves all sessions. TODO: only saved updated sessions
        /// </summary>
        public async void SaveSessions()
        {
            // not certain this really does what I want. I just don't want to block on disk IO, as inmemory is the source of truth
            // TODO: only save updated recordings to disk. Don't need to iterate across c_sessions
            await Task.Run(() => {
                foreach (var session_kvp in c_sessions)
                {
                    var file = session_kvp.Key;
                    var session = session_kvp.Value;
                    session.Sanitize(s_sanitizer);

                    // Create directories above file if they don't already exist
                    var directory = Path.GetDirectoryName(file);
                    if (!String.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    using var stream = System.IO.File.Create(file);
                    var options = new JsonWriterOptions { Indented = true };
                    var writer = new Utf8JsonWriter(stream, options);
                    session.Serialize(writer);
                    writer.Flush();
                }
            });
        }

        /// <summary>
        /// Loads saved sessions from disk. only run at startup.
        /// </summary>
        public void LoadSessions()
        {
            // TODO
        }
    }
}
