using System.Collections.Generic;
using System.IO;

namespace Azure.Sdk.Tools.PerfAutomation
{
    static class StringExtensions
    {
        public static IEnumerable<string> ToLines(this string input)
        {
            using var reader = new StringReader(input);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                yield return line;
            }
        }
    }
}
