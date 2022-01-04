using System.Text.RegularExpressions;
using Azure.Sdk.Tools.TestProxy.Common;
using Microsoft.AspNetCore.Http;

namespace Azure.Sdk.Tools.TestProxy.Transforms
{
    /// <summary>
    /// Transforms an existing header value in a response header.
    /// </summary>
    public class HeaderTransform : ResponseTransform
    {
        private readonly string _key;
        private readonly string _replacement;
        private readonly string _valueRegex;

        /// <summary>
        /// Constructs a new HeaderTransform instance.
        /// </summary>
        /// <param name="key">The header key for the header to transform.</param>
        /// <param name="replacement">The replacement value for the header.</param>
        /// <param name="valueRegex">An optional regex. If specified, the value for the header will be replaced only if
        /// the regex matches the existing value.</param>
        /// <param name="condition">
        /// A condition that dictates when this transform applies to a request/response pair. The content of this key should be a JSON object that contains configuration keys.
        /// Currently, that only includes the key "uriRegex". This translates to an object that looks like '{ "uriRegex": "when this regex matches, apply the sanitizer" }'.
        /// Defaults to "apply always."
        /// </param>
        public HeaderTransform(string key, string replacement, string valueRegex = null, ApplyCondition condition = null)
        {
            _key = key;
            _replacement = replacement;
            _valueRegex = valueRegex;
            Condition = condition;
        }

        public override void ApplyTransform(HttpRequest request, HttpResponse response)
        {
            if (response.Headers.ContainsKey(_key))
            {
                if (_valueRegex != null)
                {
                    var regex = new Regex(_valueRegex);
                    if (!regex.Match(response.Headers[_key]).Success)
                    {
                        return;
                    }
                }

                response.Headers[_key] = _replacement;
            }
        }
    }
}