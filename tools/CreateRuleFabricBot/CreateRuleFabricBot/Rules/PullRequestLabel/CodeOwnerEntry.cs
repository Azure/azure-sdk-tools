using System.Collections.Generic;
using System.Linq;

namespace CreateRuleFabricBot.Rules.PullRequestLabel
{
    internal class CodeOwnerEntry
    {
        public string PathExpression { get; set; }

        public bool ContainsWildcard { get; set; }

        public List<string> Owners { get; set; } = new List<string>();

        public List<string> Labels { get; set; } = new List<string>();

        public static CodeOwnerEntry Parse(string line)
        {
            // The format for the Codeowners File will be:
            // <path> @<owner> ... @<owner> %<label> ... %<label>

            // get rid of tabs
            line = line.Replace('\t', ' ');
            // trim the line to get rid of whitespace before and after.
            line = line.Trim();

            if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
            {
                return null;
            }

            List<string> entries = SplitLine(line).ToList();

            if (!entries.Any())
            {
                return null;
            }

            CodeOwnerEntry coe = new CodeOwnerEntry
            {
                // the first entry is the path/regex
                PathExpression = entries[0],
                ContainsWildcard = entries[0].Contains('*')
            };

            // remove the '/' from the path
            if (coe.PathExpression.StartsWith("/"))
            {
                coe.PathExpression = coe.PathExpression.Substring(1);
            }

            for (int i = 1; i < entries.Count; i++)
            {
                string entry = entries[i].Trim();

                if (entry[0] == '@')
                {
                    coe.Owners.Add(entry.Substring(1));
                }

                if (entry[0] == '%')
                {
                    coe.Labels.Add(entry.Substring(1));
                }
            }

            return coe;
        }

        private static IEnumerable<string> SplitLine(string line)
        {
            // Split the line into segments that are delimited by '@', '%' and the end of the string.

            int previousSplit = 0;
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == '@' || line[i] == '%')
                {
                    yield return line.Substring(previousSplit, i - previousSplit).Trim();
                    previousSplit = i;
                }
            }

            // add the last entry
            yield return line.Substring(previousSplit, line.Length - previousSplit).Trim();
        }

        public override string ToString()
        {
            return $"RegEx:{ContainsWildcard} Expression:{PathExpression} Owners:{string.Join(',', Owners)}  Labels:{string.Join(',', Labels)}";
        }
    }
}
