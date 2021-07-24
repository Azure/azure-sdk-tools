namespace Azure.Sdk.Tools.TestProxy.Sanitizers
{
    /// <summary>
    /// This sanitizer relies on UriRegexSanitizer to replace real subscriptionIds within a URI w/ a default or configured fake value.
    /// </summary>
    public class ReplaceRequestSubscriptionId : UriRegexSanitizer
    {

        public static string _regex = @"/(subscriptions)/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}";
        private string _value;

        /// <summary>
        /// Given a subscriptionIdReplacer sanitizer, you cannot modify the regex used to do this. You CAN modify the value
        /// that it is replaced WITH however. The default replacement value is "00000000-0000-0000-0000-000000000000".
        /// </summary>
        /// <param name="value">The fake subscriptionId that will be placed where the real one is in the real request.</param>
        public ReplaceRequestSubscriptionId(string value = "00000000-0000-0000-0000-000000000000"): base(_regex, value)
        {
            _value = value;
        }
    }
}
