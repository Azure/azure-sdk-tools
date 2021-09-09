namespace Azure.Sdk.Tools.TestProxy.Sanitizers
{
    /// <summary>
    /// This sanitizer relies on UriRegexSanitizer to replace real subscriptionIds within a URI w/ a default or configured fake value.
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
        public UriSubscriptionIdSanitizer(string value = "00000000-0000-0000-0000-000000000000"): base(value: value, regex: _regex, groupForReplace: _groupForReplace)
        {
            _value = value;
        }
    }
}
