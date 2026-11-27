// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Sdk.Tools.TestProxy.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Azure.Sdk.Tools.TestProxy.Sanitizers
{
    /// <summary>
    /// This sanitizer operates on a RecordSession entry and applies regex replacement to the Request and Response bodies contained therein. It ONLY operates on the request/response bodies. Not header or URIs.
    /// </summary>
    public class BodyKeySanitizer : RecordedTestSanitizer
    {
        private readonly string _jsonPath;
        private readonly string _newValue;
        private readonly string _groupForReplace = null;
        private readonly Regex _regex;
        private readonly List<BodyKeySanitizer> _batchedSanitizers;

        /// <summary>
        /// This sanitizer offers regex update of a specific JTokenPath. EG: "TableName" within a json response body having its value replaced by
        /// whatever substitution is offered. This simply means that if you are attempting to replace a specific key wholesale, this sanitizer will be
        /// simpler than configuring a BodyRegexSanitizer that has to match against the full "KeyName": "Value" that is part of the json structure. Further reading is available
        /// <a href="https://www.newtonsoft.com/json/help/html/SelectToken.htm#SelectTokenJSONPath">here.</a> If the body is NOT a JSON object, this sanitizer will NOT be applied.
        /// </summary>
        /// <param name="jsonPath">The SelectToken path (which could possibly match multiple entries) that will be used to select JTokens for value replacement.</param>
        /// <param name="value">The substitution value.</param>
        /// <param name="regex">A regex. Can be defined as a simple regex replace OR if groupForReplace is set, a subsitution operation. Defaults to replacing the entire string.</param>
        /// <param name="groupForReplace">The regex capture group that needs to be operated upon. Do not set if you're invoking a simple replacement operation.</param>
        /// <param name="condition">
        /// A condition that dictates when this sanitizer applies to a request/response pair. The content of this key should be a JSON object that contains various configuration keys.
        /// Currently, that only includes the key "uriRegex". This translates to an object that looks like '{ "uriRegex": "when this regex matches, apply the sanitizer" }'. Defaults to "apply always."
        /// </param>
        public BodyKeySanitizer(string jsonPath, string value = "Sanitized", string regex = null, string groupForReplace = null, ApplyCondition condition = null)
        {
            _scope = SanitizerScope.Body;
            _jsonPath = jsonPath;
            _newValue = value;
            _groupForReplace = groupForReplace;
            Condition = condition;

            _regex = regex == null ? SharedRegexes.DotAll() : GetRegex(regex);
        }

        private BodyKeySanitizer(List<BodyKeySanitizer> sanitizers)
        {
            _scope = SanitizerScope.Body;
            _batchedSanitizers = sanitizers;
        }

        internal static IEnumerable<RecordedTestSanitizer> Batch(IEnumerable<RecordedTestSanitizer> sanitizers)
        {
            List<BodyKeySanitizer> batch = null;
            foreach (var sanitizer in sanitizers)
            {
                if (sanitizer.GetType() == typeof(BodyKeySanitizer) &&
                    sanitizer.Condition == null && !sanitizer.LegacyConvertJsonDateTokens &&
                    ((BodyKeySanitizer)sanitizer)._batchedSanitizers == null)
                {
                    batch ??= [];
                    batch.Add((BodyKeySanitizer)sanitizer);
                    continue;
                }

                if (batch != null)
                {
                    yield return batch.Count == 1 ? batch[0] : new BodyKeySanitizer(batch);
                    batch = null;
                }

                yield return sanitizer;
            }

            if (batch != null)
            {
                yield return batch.Count == 1 ? batch[0] : new BodyKeySanitizer(batch);
            }
        }

        public override string SanitizeTextBody(string contentType, string body)
        {
            if (_batchedSanitizers != null)
            {
                return SanitizeTextBodyBatch(contentType, body);
            }

            bool sanitized = false;
            JToken jsonO = null;

            if (contentType.Contains("json", StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    // Prevent default behavior where JSON.NET will convert DateTimeOffset
                    // into a DateTime.
                    if (!LegacyConvertJsonDateTokens)
                    {
                        jsonO = JsonConvert.DeserializeObject<JToken>(body, SerializerSettings);
                    }
                    else
                    {
                        jsonO = JToken.Parse(body);
                    }
                }
                catch (JsonReaderException)
                {
                    return body;
                }
            }

            if (jsonO != null)
            {
                try
                {
                    sanitized = SanitizeJsonBody(jsonO);
                }
                catch (Exception e)
                {
                    DebugLogger.LogError($"Ran into exception \"{e.Message}\" while attempting to run regex \"{_regex}\" against body value \"{body}\"");
                    return body;
                }
            }

            return sanitized ? JsonConvert.SerializeObject(jsonO, SerializerSettings) : body;
        }

        private bool SanitizeJsonBody(JToken body, List<(JToken Original, JToken Replacement)> replacements = null)
        {
            bool sanitized = false;
            foreach (JToken token in body.SelectTokens(_jsonPath))
            {
                if (token.Parent != null && !token.HasValues)
                {
                    var originalValue = token.Value<string>();
                    if (originalValue == null)
                    {
                        // No value to replace.
                        continue;
                    }

                    var replacement = StringSanitizer.SanitizeValue(originalValue, _newValue, _regex, _groupForReplace);
                    if (token.Type == JTokenType.String && originalValue == replacement)
                    {
                        // Nothing to change, skip further work.
                        continue;
                    }

                    var replacementToken = new JValue(replacement);
                    token.Replace(replacementToken);
                    if (replacementToken.Parent != null)
                    {
                        replacements?.Add((token, replacementToken));
                    }
                    sanitized |= originalValue != replacement;
                }
            }

            return sanitized;
        }

        private string SanitizeTextBodyBatch(string contentType, string body)
        {
            if (!contentType.Contains("json", StringComparison.CurrentCultureIgnoreCase))
            {
                return body;
            }

            JToken json;
            try
            {
                json = JsonConvert.DeserializeObject<JToken>(body, SerializerSettings);
            }
            catch (JsonReaderException)
            {
                return body;
            }

            if (json == null)
            {
                return body;
            }

            if (json is JContainer container)
            {
                foreach (var token in container.Descendants())
                {
                    if (token.Type == JTokenType.Float && !double.IsFinite(token.Value<double>()))
                    {
                        return SanitizeTextBodySequentially(contentType, body);
                    }
                }
            }

            bool sanitized = false;
            var replacements = new List<(JToken Original, JToken Replacement)>();
            try
            {
                foreach (var sanitizer in _batchedSanitizers)
                {
                    if (sanitizer.SanitizeJsonBody(json, replacements))
                    {
                        sanitized = true;
                    }
                    else
                    {
                        for (int index = replacements.Count - 1; index >= 0; index--)
                        {
                            replacements[index].Replacement.Replace(replacements[index].Original);
                        }
                    }

                    replacements.Clear();
                }
            }
            catch (Exception)
            {
                return SanitizeTextBodySequentially(contentType, body);
            }

            return sanitized ? JsonConvert.SerializeObject(json, SerializerSettings) : body;
        }

        private string SanitizeTextBodySequentially(string contentType, string body)
        {
            foreach (var sanitizer in _batchedSanitizers)
            {
                body = sanitizer.SanitizeTextBody(contentType, body);
            }

            return body;
        }

        public override byte[] SanitizeBody(string contentType, byte[] body)
        {
            return body;
        }
    }
}
