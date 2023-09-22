using Azure.Sdk.Tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Common.Exceptions;
using System.Linq;
using System.Text.RegularExpressions;

namespace Azure.Sdk.Tools.TestProxy.Sanitizers
{
    /// <summary>
    /// This sanitizer applies at the session level, just before saving a recording to disk.
    /// 
    /// It cleans out all request/response pairs that that match the defined settings. A match against URI, Header, or Body regex will result in the entire RecordEntry being omit from the recording.
    /// </summary>
    public class RegexEntrySanitizer : RecordedTestSanitizer
    {
        private Regex rx;
        private string section;
        private string[] validValues = new string[] { "uri", "header", "body" };

        public string ValidValues
        {
            get { return string.Join(", ", validValues.Select(x => "\"" + x + "\"")); }
        }

        /// <summary>
        /// During sanitization, each RecordEntry within a session is checked against a target (URI, Header, Body) and a regex. If there is any match within the request, the whole request/response pair is omitted from the recording.
        /// </summary>
        /// <param name="target">Possible values are [ "URI", "Header", "Body"]. Only requests with text-like body values will be checked when targeting "Body". The value is NOT case-sensitive.</param>
        /// <param name="regex">During sanitization, any entry where the 'target' is matched by the regex will be fully omitted. Request/Reponse both.</param>
        public RegexEntrySanitizer(string target, string regex)
        {
            section = target.ToLowerInvariant();

            if (!validValues.Contains(section))
            {
                throw new HttpException(System.Net.HttpStatusCode.BadRequest, $"When defining which section of a request the regex should target, only values [ {ValidValues} ] are valid.");
            }

            rx = StringSanitizer.GetRegex(regex);
        }

        public bool CheckMatch(RecordEntry x)
        {
            switch (section)
            {
                case "uri":
                    return rx.IsMatch(x.RequestUri);
                case "header":
                    foreach (var headerKey in x.Request.Headers.Keys)
                    {
                        // Accessing 0th key safe due to the fact that we force header values in without splitting them on ;. 
                        // We do this because letting .NET split and then reassemble header values introduces a space into the header itself
                        // Ex: "application/json;odata=minimalmetadata" with .NET default header parsing becomes "application/json; odata=minimalmetadata"
                        // Given this breaks signature verification, we have to avoid it.
                        var originalValue = x.Request.Headers[headerKey][0];
                        
                        if (rx.IsMatch(originalValue))
                        {
                            return true;
                        }
                    }
                    break;
                case "body":
                    if (x.Request.TryGetBodyAsText(out string text))
                    {
                        return rx.IsMatch(text);
                    }
                    else
                    {
                        return false;
                    }
                default:
                    throw new HttpException(System.Net.HttpStatusCode.BadRequest, $"The RegexEntrySanitizer can only match against a target of [ {ValidValues} ].");
            }

            return false;
        }

        public override void Sanitize(RecordSession session)
        {
            session.Entries.RemoveAll(x => CheckMatch(x));
        }
    }
}
