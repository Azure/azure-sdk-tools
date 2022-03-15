using Azure.Sdk.Tools.TestProxy.Common;

namespace Azure.Sdk.Tools.TestProxy.Sanitizers
{
    /// <summary>
    /// This sanitizer relies on UriRegexSanitizer to replace real subscriptionIds within a URI w/ a default or configured fake value. This sanitizer ONLY affects
    /// the URI of a request/response pair.
    /// </summary>
    public class UriSubscriptionIdSanitizer : UriRegexSanitizer
    {

        public static string _regex = @"/subscriptions/(?<subid>[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})";
        public static string _groupForReplace = "subid";
        private string _value;

        /// <summary>
        /// This sanitizer is targeted using the regex "/subscriptions/([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})". This is not a setting
        /// that can be changed for this sanitizer. For full regex support, take a look at UriRegexSanitizer. You CAN modify the value
        /// that the subscriptionId is replaced WITH however.
        /// </summary>
        /// <param name="value">The fake subscriptionId that will be placed where the real one is in the real request. The default replacement value is "00000000-0000-0000-0000-000000000000".</param>
        /// <param name="condition">
        /// A condition that dictates when this sanitizer applies to a request/response pair. The content of this key should be a JSON object that contains configuration keys. 
        /// Currently, that only includes the key "uriRegex". This translates to an object that looks like '{ "uriRegex": "when this regex matches, apply the sanitizer" }'. Defaults to "apply always."
        /// </param>
        public UriSubscriptionIdSanitizer(string value = "00000000-0000-0000-0000-000000000000", ApplyCondition condition = null): base(value: value, regex: _regex, groupForReplace: _groupForReplace)
        {
            Condition = condition;

            _value = value;
        }
    }
}
