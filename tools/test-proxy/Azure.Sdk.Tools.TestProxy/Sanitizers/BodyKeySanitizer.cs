using Azure.Sdk.Tools.TestProxy.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Net;

namespace Azure.Sdk.Tools.TestProxy.Sanitizers
{
    /// <summary>
    /// This sanitizer operates on a RecordSession entry and applies regex replacement to the Request and Response bodies contained therein. It ONLY operates on the request/response bodies. Not header or URIs.
    /// </summary>
    public class BodyKeySanitizer : RecordedTestSanitizer
    {
        private string _jsonPath;
        private string _newValue;
        private string _regexValue = null;
        private string _groupForReplace = null;

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
        public BodyKeySanitizer(string jsonPath, string value = "Sanitized", string regex = ".+", string groupForReplace = null, ApplyCondition condition = null)
        {
            _jsonPath = jsonPath;
            _newValue = value;
            _regexValue = regex;
            _groupForReplace = groupForReplace;
            Condition = condition;

            StringSanitizer.ConfirmValidRegex(regex);
        }

        public override string SanitizeTextBody(string contentType, string body)
        {
            bool sanitized = false;
            JToken jsonO;

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
            catch(JsonReaderException)
            {
                return body;
            }

            
            foreach (JToken token in jsonO.SelectTokens(_jsonPath))
            {
                // HasValues is false for tokens with children. We will not apply sanitization if that is the case.
                if (!token.HasValues)
                {
                    var originalValue = token.Value<string>();

                    // regex replacement does not support null
                    if (originalValue == null)
                    {
                        continue;
                    }

                    var replacement = StringSanitizer.SanitizeValue(originalValue, _newValue, _regexValue, _groupForReplace);

                    // this sanitizer should only apply to actual values
                    // if we attempt to apply a regex update to a jtoken that has a more complex type, throw
                    token.Replace(JToken.FromObject(replacement));

                    if(originalValue != replacement)
                    {
                        sanitized = true;
                    }
                }
            }

            return sanitized ? JsonConvert.SerializeObject(jsonO, SerializerSettings) : body;
        }


        public override byte[] SanitizeBody(string contentType, byte[] body)
        {
            return body;
        }
    }
}
