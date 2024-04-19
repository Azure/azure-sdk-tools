using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Azure.Sdk.Tools.TestProxy.Common.Exceptions;
using Azure.Sdk.Tools.TestProxy.Sanitizers;

namespace Azure.Sdk.Tools.TestProxy.Common
{
    public class RegisteredSanitizer
    {
        public string Id { get; set; }
        public RecordedTestSanitizer Sanitizer { get; set; }

        public RegisteredSanitizer(RecordedTestSanitizer sanitizer, string id)
        {
            Id = id;
            Sanitizer = sanitizer;
        }
    }

    public static class IdFactory
    {
        private static int CurrentId = 0;

        public static int GetNextId()
        {
            return Interlocked.Increment(ref CurrentId);
        }
    }

    public class SanitizerDictionary
    {
        private ConcurrentDictionary<string, RegisteredSanitizer> Sanitizers = new ConcurrentDictionary<string, RegisteredSanitizer>();

        // we have to know which sanitizers are session only
        // so that when we start a new recording we can properly 
        // apply only the sanitizers that have been registered at the global level
        public List<string> SessionSanitizers = new List<string>();

        public SanitizerDictionary() {
            ResetSessionSanitizers();
        }

        public List<RegisteredSanitizer> DefaultSanitizerList = new List<RegisteredSanitizer>
            {
                new RegisteredSanitizer(
                    new RecordedTestSanitizer(),
                    "AZSDK001"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..access_token"),
                    "AZSDK002"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..refresh_token"),
                    "AZSDK003"
                )
            };

        public void ResetSessionSanitizers()
        {
            var expectedSanitizers = DefaultSanitizerList;

            for (int i = 0; i < expectedSanitizers.Count; i++)
            {
                var id = expectedSanitizers[i].Id;
                var sanitizer = expectedSanitizers[i].Sanitizer;

                if (!Sanitizers.ContainsKey(id))
                {
                    _register(sanitizer, id);
                }
            }

            SessionSanitizers = DefaultSanitizerList.Select(x => x.Id).ToList();
        }

        /// <summary>
        /// Get the complete set of sanitizers that apply to this recording/playback session
        /// </summary>
        /// <param name="session"></param>
        /// <returns></returns>
        public List<RecordedTestSanitizer> GetSanitizers(ModifiableRecordSession session)
        {
            return GetRegisteredSanitizers(session).Select(x => x.Sanitizer).ToList();
        }

        public List<RecordedTestSanitizer> GetSanitizers()
        {
            return GetRegisteredSanitizers().Select(x => x.Sanitizer).ToList();
        }

        public List<RegisteredSanitizer> GetRegisteredSanitizers(ModifiableRecordSession session)
        {
            var sanitizers = new List<RegisteredSanitizer>();
            foreach (var id in session.AppliedSanitizers)
            {
                if (Sanitizers.TryGetValue(id, out RegisteredSanitizer sanitizer))
                {
                    sanitizers.Add(sanitizer);
                }
            }

            return sanitizers;
        }

        public List<RegisteredSanitizer> GetRegisteredSanitizers()
        {
            var sanitizers = new List<RegisteredSanitizer>();
            foreach (var id in SessionSanitizers)
            {
                if (Sanitizers.TryGetValue(id, out RegisteredSanitizer sanitizer))
                {
                    sanitizers.Add(sanitizer);
                }
            }

            return sanitizers;
        }

        public bool _register(RecordedTestSanitizer sanitizer, string id)
        {

            if (Sanitizers.TryAdd(id, new RegisteredSanitizer(sanitizer, id)))
            {
                return true;
            }
            else
            {
                // todo better error
                throw new HttpException(System.Net.HttpStatusCode.BadRequest, "Unable to add sanitizer to global list.");
            }
        }

        /// <summary>
        /// Ensuring that session level sanitizers can be identified internally
        /// </summary>
        /// <param name="sanitizer"></param>
        /// <returns></returns>
        /// <exception cref="HttpException"></exception>
        public string Register(RecordedTestSanitizer sanitizer)
        {
            var strCurrent = IdFactory.GetNextId().ToString();

            if (_register(sanitizer, strCurrent))
            {
                SessionSanitizers.Add(strCurrent);
                return strCurrent;
            }
            return string.Empty;
        }

        /// <summary>
        /// Register a sanitizer the global cache, add it to the set that applies to the session, and ensure we clean up after.
        /// </summary>
        /// <param name="session"></param>
        /// <param name="sanitizer"></param>
        /// <returns></returns>
        /// <exception cref="HttpException"></exception>
        public string Register(ModifiableRecordSession session, RecordedTestSanitizer sanitizer)
        {
            var strCurrent = IdFactory.GetNextId().ToString();
            if (_register(sanitizer, strCurrent))
            {
                session.AppliedSanitizers.Add(strCurrent);
                session.ForRemoval.Add(strCurrent);

                return strCurrent;
            }

            return string.Empty;
        }

        public string Unregister(string sanitizerId)
        {
            return null;
        }

        public string Unregister(string sanitizerId, ModifiableRecordSession session)
        {
            return null;
        }

        /// <summary>
        /// Fired at the end of a recording/playback session so that we can clean up the global dictionary.
        /// </summary>
        /// <param name="session"></param>
        public void Cleanup(ModifiableRecordSession session)
        {
            foreach(var sanitizerId in session.ForRemoval)
            {
                Sanitizers.TryRemove(sanitizerId, out var RemovedSanitizer);
            }
        }
        public void Clear()
        {
            foreach(var sanitizerId in SessionSanitizers)
            {
                Sanitizers.TryRemove(sanitizerId.ToString(), out var RemovedSanitizer);
            }

            SessionSanitizers.Clear();
        }
    }
}
