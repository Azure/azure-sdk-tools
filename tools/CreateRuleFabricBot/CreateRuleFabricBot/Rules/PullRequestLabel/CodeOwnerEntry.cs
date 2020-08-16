using CreateRuleFabricBot.Rules.IssueRouting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;

namespace CreateRuleFabricBot.Rules.PullRequestLabel
{
    public class CodeOwnerEntry
    {
        const char LabelSeparator = '%';
        const char OwnerSeparator = '@';
        internal const string LabelMoniker = "PRLabel";

        public CodeOwnerEntry(string entryLine, string labelsLine)
        {
            ParseLabels(labelsLine);
            ParseOwnersAndPath(entryLine);
        }

        public CodeOwnerEntry()
        {

        }

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

        private static string[] SplitLine(string line, char splitOn)
        {
            return line.Split(splitOn, StringSplitOptions.RemoveEmptyEntries);
        }

        public override string ToString()
        {
            return $"HasWildcard:{ContainsWildcard} Expression:{PathExpression} Owners:{string.Join(',', Owners)}  Labels:{string.Join(',', Labels)}";
        }

        public void ParseLabels(string line)
        {
            // Parse a line that looks like # PRLabel: %Label, %Label
            if (line.IndexOf(LabelMoniker, StringComparison.OrdinalIgnoreCase) == -1)
            {
                return;
            }

            // If we don't have a ':', nothing to do
            int colonPosition = line.IndexOf(':');
            if (colonPosition == -1)
            {
                return;
            }

            line = line.Substring(colonPosition + 1).Trim();
            foreach (string label in SplitLine(line, LabelSeparator).ToList())
            {
                if (!string.IsNullOrWhiteSpace(label))
                {
                    Labels.Add(label.Trim());
                }
            }
        }

        public void ParseOwnersAndPath(string line)
        {
            if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
            {
                return;
            }

            line = ParsePath(line);

            foreach (string author in SplitLine(line, OwnerSeparator).ToList())
            {
                if (!string.IsNullOrWhiteSpace(author))
                {
                    Owners.Add(author.Trim());
                }
            }
        }

        private string ParsePath(string line)
        {
            // Get the start of the owner in the string
            int ownerStartPosition = line.IndexOf('@');
            if (ownerStartPosition == -1)
            {
                return line;
            }

            string path = line.Substring(0, ownerStartPosition).Trim();
            // the first entry is the path/regex
            PathExpression = path;
            ContainsWildcard = path.Contains('*');

            // remove the path from the string.
            return line.Substring(ownerStartPosition);
        }
    }
}
