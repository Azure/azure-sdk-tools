using CreateRuleFabricBot.Rules.IssueRouting;
using System;
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
        public bool IsValid
        {
            get
            {
                return !string.IsNullOrEmpty(PathExpression);
            }
        }
        private static IEnumerable<string> SplitLine(string line)
        {
            // Split the line into segments that are delimited by '@', '%' and the end of the string.

            int previousSplit = -1;
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == '@' || line[i] == '%')
                {

                    if (previousSplit != -1)
                    {
                        yield return line.Substring(previousSplit, i - previousSplit).Trim();
                    }
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

        internal static void ParseLabels(CodeOwnerEntry entry, string line)
        {
            // Parse a line that looks like # PRLabel: %Label, %Label

            // If we don't have a ':', nothing to do
            int colonPosition = line.IndexOf(':');
            if (colonPosition == -1)
            {
                return;
            }

            line = line.Substring(colonPosition + 1).Trim();
            foreach (string label in SplitLine(line).ToList())
            {
                if (label[0] == '%')
                {
                    entry.Labels.Add(label.Substring(1));
                }

            }
        }

        internal static void ParseOwnersAndPath(CodeOwnerEntry entry, string line)
        {
            if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
            {
                return;
            }

            // Get the start of the owner in the string
            int ownerStartPosition = line.IndexOf('@');
            if (ownerStartPosition == -1)
            {
                return;
            }

            string path = line.Substring(0, ownerStartPosition).Trim();
            // the first entry is the path/regex
            entry.PathExpression = path;
            entry.ContainsWildcard = path.Contains('*');
            // remove the '/' from the path
            if (entry.PathExpression.StartsWith("/"))
            {
                entry.PathExpression = entry.PathExpression.Substring(1);
            }

            // remove the path from the string.
            line = line.Substring(ownerStartPosition);

            // At this point, we know the line does not start with '#'
            List<string> entries = SplitLine(line).ToList();

            for (int i = 0; i < entries.Count; i++)
            {
                string author = entries[i].Trim();

                if (author[0] == '@')
                {
                    entry.Owners.Add(author.Substring(1));
                }
            }
        }
    }
}
