using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.TestProxy.Common
{
    public static class StringSanitizer
    {
        /// <summary>
        /// General purpose string replacement/subsitution given a set of inputs. Used in many regex substitution sanitizers.
        /// </summary>
        /// <param name="inputValue">The name of the header we're operating against.</param>
        /// <param name="replacementValue">The substitution or whole new header value, depending on "regex" setting.</param>
        /// <param name="regex">A regex. Can be defined as a simple regex replace OR if groupName is set, a subsitution operation.</param>
        /// <param name="groupName">The capture group that needs to be operated upon. Do not set if you're invoking a simple replacement operation. 
        /// Note that with this implementation, you can refer to a numbered group if you didn't name it, EG: '0'.</param>
        public static string SanitizeValue(string inputValue, string replacementValue, string regex = null, string groupName = null)
        {
            if (regex == null)
            {
                return replacementValue;
            }

            var rx = new Regex(regex);
            var replacement = String.Empty;

            if (groupName != null)
            {
                replacement = rx.Replace(inputValue, m =>
                {
                    var group = m.Groups[groupName];
                    var sb = new StringBuilder();
                    var previousCaptureEnd = 0;

                    foreach (Capture capture in group.Captures)
                    {
                        // it is possible to have an entire match multiple times within a string.
                        // to deal with that, we subtract the match index from each of the operations. This due
                        // to the fact that all indexes are given relative to the start of the entire string, NOT the 
                        // specific match we're operating on.
                        var currentCaptureEnd = capture.Index + capture.Length - m.Index;
                        var currentCaptureLength = capture.Index - m.Index - previousCaptureEnd;

                        // append everything up to our current capture. (also if we're between multiple captures in a single match string)
                        sb.Append(m.Value.Substring(previousCaptureEnd, currentCaptureLength));

                        // add the replacement value where the original resided
                        sb.Append(replacementValue);

                        // updating the end will both inform the next capture group AS WELL AS
                        // ensuring that if we're finished with the captures, we can just add the "rest"
                        // of the string.
                        previousCaptureEnd = currentCaptureEnd;
                    }

                    // one final append to pick up the remainder of the string after the capture
                    sb.Append(m.Value.Substring(previousCaptureEnd));

                    return sb.ToString();
                });
            }
            else
            {
                replacement = rx.Replace(inputValue, replacementValue);
            }

            return replacement;
        }
    }
}
