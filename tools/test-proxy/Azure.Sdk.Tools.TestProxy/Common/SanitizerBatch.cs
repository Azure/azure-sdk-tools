using System.Collections.Generic;
using Azure.Sdk.Tools.TestProxy.Sanitizers;

namespace Azure.Sdk.Tools.TestProxy.Common
{
    internal sealed class SanitizerBatch : RecordedTestSanitizer
    {
        private readonly List<RecordedTestSanitizer> _sanitizers;

        private SanitizerBatch(List<RecordedTestSanitizer> sanitizers)
        {
            _sanitizers = sanitizers;
        }

        internal static IEnumerable<RecordedTestSanitizer> Create(IEnumerable<RecordedTestSanitizer> sanitizers)
        {
            List<RecordedTestSanitizer> batch = null;
            foreach (var sanitizer in BodyXmlSanitizer.Batch(BodyKeySanitizer.Batch(sanitizers)))
            {
                var type = sanitizer.GetType();
                if (sanitizer.Condition == null && !sanitizer.LegacyConvertJsonDateTokens &&
                    (type == typeof(RecordedTestSanitizer) || type == typeof(GeneralRegexSanitizer) ||
                    type == typeof(BodyRegexSanitizer) || type == typeof(BodyKeySanitizer) ||
                    type == typeof(BodyXmlSanitizer) || type == typeof(HeaderRegexSanitizer) || type == typeof(UriRegexSanitizer)))
                {
                    batch ??= new List<RecordedTestSanitizer>();
                    batch.Add(sanitizer);
                    continue;
                }

                if (batch != null)
                {
                    yield return batch.Count == 1 ? batch[0] : new SanitizerBatch(batch);
                    batch = null;
                }

                yield return sanitizer;
            }

            if (batch != null)
            {
                yield return batch.Count == 1 ? batch[0] : new SanitizerBatch(batch);
            }
        }

        public override void Sanitize(RecordEntry entry, bool matchingBodies = true)
        {
            entry.Request.BeginTextSanitization();
            entry.Response.BeginTextSanitization();
            try
            {
                foreach (var sanitizer in _sanitizers)
                {
                    sanitizer.Sanitize(entry, matchingBodies);
                }
            }
            finally
            {
                entry.Request.EndTextSanitization();
                entry.Response.EndTextSanitization();
            }
        }
    }
}
