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
        public readonly ConcurrentDictionary<string, RecordSession> sessions
            = new ConcurrentDictionary<string, RecordSession>();

        private static readonly RecordedTestSanitizer s_sanitizer = new RecordedTestSanitizer();

        // only ever should be invoked once, as this class is registered as a singleton in Startup.cs
        public InMemorySessionManager()
        {
            this.LoadSessions();
        }


        /// <summary>
        /// Saves all sessions. TODO: only saved updated sessions
        /// </summary>
        public void SaveSessions()
        {
            foreach(var session_kvp in sessions)
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
        }

        /// <summary>
        /// Loads saved sessions from disk. only run at startup.
        /// </summary>
        public void LoadSessions()
        {

        }
    }
}
