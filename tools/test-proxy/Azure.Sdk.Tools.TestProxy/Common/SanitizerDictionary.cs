using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Azure.Sdk.Tools.TestProxy.Common.Exceptions;
using Azure.Sdk.Tools.TestProxy.Sanitizers;
using Microsoft.AspNetCore.Authentication;

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

    public class SanitizerDictionary
    {
        private ConcurrentDictionary<string, RegisteredSanitizer> Sanitizers;

        // we have to know which sanitizers are session only
        // so that when we start a new recording we can properly 
        // apply only the sanitizers that have been registered at the global level
        public List<string> SessionSanitizers = new List<string>();
        private int CurrentId = 0;

        public SanitizerDictionary() { }

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
            var sessionSanitizers = DefaultSanitizerList.Select(x => x.Id);

            foreach (var id in SessionSanitizers)
            {
                if (!sessionSanitizers.Contains(id))
                {
                    if(!Sanitizers.TryRemove(id, out var sanitizer))
                    {
                        throw new HttpException(System.Net.HttpStatusCode.BadRequest, $"Unable to properly clean up  a session sanitizer under id {id}.");
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
                if (Sanitizers.TryGetValue(id, out RegisteredSanitizer sanitizer))
                {
                    sanitizers.Add(sanitizer.Sanitizer);
                }
            }

            return sanitizers;
        }

        public List<RecordedTestSanitizer> GetSanitizers()
        {
            var sanitizers = new List<RecordedTestSanitizer>();
            foreach (var id in SessionSanitizers)
            {
                if (Sanitizers.TryGetValue(id, out RegisteredSanitizer sanitizer))
                {
                    sanitizers.Add(sanitizer.Sanitizer);
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
        public bool Register(RecordedTestSanitizer sanitizer)
        {
            var strCurrent = CurrentId.ToString();

            if (_register(sanitizer, strCurrent))
            {
                SessionSanitizers.Add(strCurrent);
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
            var strCurrent = CurrentId.ToString();
            if (_register(sanitizer, strCurrent))
            {
                session.AppliedSanitizers.Add(strCurrent);
                session.ForRemoval.Add(strCurrent);

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
