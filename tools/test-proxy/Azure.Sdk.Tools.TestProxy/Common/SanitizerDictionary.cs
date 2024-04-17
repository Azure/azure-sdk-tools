using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Azure.Sdk.Tools.TestProxy.Common.Exceptions;
using Azure.Sdk.Tools.TestProxy.Sanitizers;
using Microsoft.AspNetCore.Authentication;

namespace Azure.Sdk.Tools.TestProxy.Common
{
    public class SanitizerDictionary
    {
        private ConcurrentDictionary<int, RecordedTestSanitizer> Sanitizers;

        // we have to know which sanitizers are session only
        // so that when we start a new recording we can properly 
        // apply only the sanitizers that have been registered at the global level
        public List<int> SessionSanitizers = new List<int>();
        private int CurrentId = 0;

        public SanitizerDictionary() { }

        public List<RecordedTestSanitizer> DefaultSanitizerList = new List<RecordedTestSanitizer>
            {
                new RecordedTestSanitizer(),
                new BodyKeySanitizer("$..access_token"),
                new BodyKeySanitizer("$..refresh_token")
            };

        public void ResetSessionSanitizers()
        {
            var defaultSanitizerIds = Enumerable.Range(0, DefaultSanitizerList.Count).ToList();

            foreach(var id in SessionSanitizers)
            {
                if (!defaultSanitizerIds.Contains(id))
                {
                    if(Sanitizers.TryRemove(id, out var sanitizer))
                    {
                        SessionSanitizers.Remove(id);
                    }
                    else
                    {
                        // todo better error
                        throw new HttpException(System.Net.HttpStatusCode.BadRequest, "Unable to add sanitizer to global list.");
                    }
                }
            }
        }

        /// <summary>
        /// Get the complete set of sanitizers that apply to this recording/playback session
        /// </summary>
        /// <param name="session"></param>
        /// <returns></returns>
        public List<RecordedTestSanitizer> GetSanitizers(ModifiableRecordSession session)
        {
            var sanitizers = new List<RecordedTestSanitizer>();
            foreach(var id in session.AppliedSanitizers)
            {
                if (Sanitizers.TryGetValue(id, out RecordedTestSanitizer sanitizer))
                {
                    sanitizers.Add(sanitizer);
                }
            }

            return sanitizers;
        }

        public List<RecordedTestSanitizer> GetSanitizers()
        {
            var sanitizers = new List<RecordedTestSanitizer>();
            foreach (var id in SessionSanitizers)
            {
                if (Sanitizers.TryGetValue(id, out RecordedTestSanitizer sanitizer))
                {
                    sanitizers.Add(sanitizer);
                }
            }

            return sanitizers;
        }

        public bool _register(RecordedTestSanitizer sanitizer, int id)
        {
            if (Sanitizers.TryAdd(id, sanitizer))
            {
                SessionSanitizers.Add(id);

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
        public bool Register(RecordedTestSanitizer sanitizer)
        {
            if (_register(sanitizer, CurrentId))
            {
                SessionSanitizers.Add(CurrentId);
                CurrentId++;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Register a sanitizer the global cache, add it to the set that applies to the session, and ensure we clean up after.
        /// </summary>
        /// <param name="session"></param>
        /// <param name="sanitizer"></param>
        /// <returns></returns>
        /// <exception cref="HttpException"></exception>
        public bool Register(ModifiableRecordSession session, RecordedTestSanitizer sanitizer)
        {
            if (_register(sanitizer, CurrentId))
            {
                session.AppliedSanitizers.Add(CurrentId);
                session.ForRemoval.Add(CurrentId);

                CurrentId++;
                return true;
            }

            return false;
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
    }
}
