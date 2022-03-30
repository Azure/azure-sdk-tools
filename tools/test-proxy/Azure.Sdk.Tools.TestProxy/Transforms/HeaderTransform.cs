using Azure.Sdk.Tools.TestProxy.Common;
using Microsoft.AspNetCore.Http;

namespace Azure.Sdk.Tools.TestProxy.Transforms
{
    /// <summary>
    /// Sets a header in a response.
    /// </summary>
    public class HeaderTransform : ResponseTransform
    {
        private readonly string _key;
        private readonly string _value;

        /// <summary>
        /// Constructs a new HeaderTransform instance.
        /// </summary>
        /// <param name="key">The header key for the header.</param>
        /// <param name="value">The value for the header.</param>
        /// <param name="condition">
        /// A condition that dictates when this transform applies to a request/response pair. The content of this key should be a JSON object that contains configuration keys.
        /// Currently, that only includes the key "uriRegex". This translates to an object that looks like '{ "uriRegex": "when this regex matches, apply the sanitizer" }'.
        /// Defaults to "apply always."
        /// </param>
        /// <remarks>
        /// By default, the header will be set in the response whether or not the header key is already
        /// present.
        /// If the header should only be set if the header key is already present in the response,
        /// include a Condition populated with a ResponseHeader in the HeaderTransform JSON.
        /// </remarks>
        public HeaderTransform(string key, string value, ApplyCondition condition = null)
        {
            _key = key;
            _value = value;
            Condition = condition;
        }

        /// <summary>
        /// This transform applies during playback mode. It copies the header "api-version" of the request
        /// onto the response before sending the response back to the client.
        /// </summary>
        /// <param name="request">The request from which transformations will be pulled.</param>
        /// <param name="match">The matched playback entry that can be transformed with a new apiversion header.</param>
        public override void ApplyTransform(HttpRequest request, RecordEntry match)
        {
            match.Response.Headers[_key] = new string[] { _value };
        }
    }
}
