using System.Text.RegularExpressions;
using Azure.Sdk.Tools.TestProxy.Common;
using Microsoft.AspNetCore.Http;

namespace Azure.Sdk.Tools.TestProxy.Transforms
{
    public class HeaderTransform : ResponseTransform
    {
        private readonly string _key;
        private readonly string _replacement;
        private readonly string _valueRegex;

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