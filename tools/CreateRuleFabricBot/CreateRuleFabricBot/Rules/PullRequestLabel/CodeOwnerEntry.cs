using System;
using System.Collections.Generic;

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

            if (string.IsNullOrEmpty(line) || line.TrimStart().StartsWith('#'))
            {
                return null;
            }

            line = line.Replace('\t', ' ');
            string[] entries = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (entries.Length == 0)
            {
                return null;
            }

            CodeOwnerEntry coe = new CodeOwnerEntry
            {
                // the first entry is the path/regex
                PathExpression = entries[0].Trim(),
                ContainsWildcard = entries[0].Contains('*')
            };

            // remove the '/' from the path
            if (coe.PathExpression.StartsWith("/"))
            {
                coe.PathExpression = coe.PathExpression.Substring(1);
            }

            for (int i = 1; i < entries.Length; i++)
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

        public override string ToString()
        {
            return $"RegEx:{ContainsWildcard} Expression:{PathExpression} Owners:{string.Join(',', Owners)}  Labels:{string.Join(',', Labels)}";
        }
    }
}
