using Azure.Sdk.Tools.TestProxy.Common;
using System;
using System.Text;

namespace Azure.Sdk.Tools.TestProxy.Sanitizers
{
    /// <summary>
    /// This sanitizer operates on a RecordSession entry and applies value replacement to the Request and Response bodies contained therein. It only replaces \r\n with \n.
    /// </summary>
    public class BodyLineEndingSanitizer : BodyStringSanitizer
    {
        public static string _targetValue = "\r\n";
        public static string _newValue = "\n";

        /// <summary>
        /// This sanitizer 
        /// </summary>
        /// <param name="condition">
        /// A condition that dictates when this sanitizer applies to a request/response pair. The content of this key should be a JSON object that contains configuration keys. 
        /// Currently, that only includes the key "uriRegex". This translates to an object that looks like '{ "uriRegex": "when this regex matches, apply the sanitizer" }'. Defaults to "apply always."
        /// </param>
        public BodyLineEndingSanitizer(ApplyCondition condition = null) : base(target: _targetValue, value: _newValue, condition: condition)
        {
            Condition = condition;
        }

        public override string SanitizeTextBody(string contentType, string body)
        {
            return StringSanitizer.ReplaceValue(inputValue: body, targetValue: _targetValue, replacementValue: _newValue);
        }
    }
}
