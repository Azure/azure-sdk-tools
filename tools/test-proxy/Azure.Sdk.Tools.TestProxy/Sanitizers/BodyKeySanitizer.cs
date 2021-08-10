using Azure.Sdk.Tools.TestProxy.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace Azure.Sdk.Tools.TestProxy.Sanitizers
{
    /// <summary>
    /// This sanitizer operates on a RecordSession entry and applies itself to the Request and Response bodies contained therein.
    /// </summary>
    public class BodyKeySanitizer : RecordedTestSanitizer
    {
        private string _jsonPath;
        private string _newValue;
        private string _regexValue = null;
        private string _groupForReplace = null;

        /// <summary>
        /// This sanitizer offers regex update of a specific JTokenPath. EG: "TableName" within a json response body having it's value replaced by
        /// whatever substitution is offered. This simply means that if you are attempting to replace a specific key wholesale, this sanitizer will be 
        /// simpler than configuring a BodyRegexSanitizer that has to match against the full "KeyName": "Value" that is part of the json structure. Further reading is available
        /// <a href="https://www.newtonsoft.com/json/help/html/SelectToken.htm#SelectTokenJSONPath">here.</a>
        /// </summary>
        /// <param name="jsonPath">The SelectToken path (which could possibly match multiple entries) that will be used to select JTokens for value replacement.</param>
        /// <param name="value">The substitution value.</param>
        /// <param name="regex">A regex. Can be defined as a simple regex replace OR if groupForReplace is set, a subsitution operation. Defaults to replacing the entire string.</param>
        /// <param name="groupForReplace">The capture group that needs to be operated upon. Do not set if you're invoking a simple replacement operation.</param>
        public BodyKeySanitizer(string jsonPath, string value = "Sanitized", string regex = ".*", string groupForReplace = null)
        {
            _jsonPath = jsonPath;
            _newValue = value;
            _regexValue = regex;
            _groupForReplace = groupForReplace;
        }

        public override string SanitizeTextBody(string contentType, string body)
        {
            JToken jsonO;
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

            
            foreach (JToken token in jsonO.SelectTokens(_jsonPath))
            {
                // HasValues is false for tokens with children. We will not apply sanitization if that is the case.
                if (!token.HasValues)
                {
                    var replacement = StringSanitizer.SanitizeValue(token.Value<string>(), _newValue, _regexValue, _groupForReplace);

                    // this sanitizer should only apply to actual values
                    // if we attempt to apply a regex update to a jtoken that has a more complex type, throw
                    token.Replace(JToken.FromObject(replacement));
                }
            }
            
            return JsonConvert.SerializeObject(jsonO, SerializerSettings);
        }


        public override byte[] SanitizeBody(string contentType, byte[] body)
        {
            throw new NotImplementedException("Current concept of sanitization doesn't apply to non-text payloads. If you are encountering this, contact scbedd with the example so as to improve the system.");
        }
    }
}
