// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.RegularExpressions;

namespace Azure.SDK.ChangelogGen.Report
{
    public class ReleaseNoteGroup
    {
        private const string PREFIX = "### ";

        public string Name { get; set; }
        public List<ReleaseNote> Notes { get; } = new List<ReleaseNote>();

        public ReleaseNoteGroup(string name)
        {
            this.Name = name;
        }

        public static bool TryParseGroupTitle(string line, out ReleaseNoteGroup? group)
        {
            Regex groupTitle = new Regex(@"^\s*###\s+(?<title>.+?)\s*$");
            Match match = groupTitle.Match(line);
            if (!match.Success)
            {
                group = null;
                return false;
            }
            else
            {
                group = new ReleaseNoteGroup(match.Groups["title"].Value);
                return true;
            }
        }

        public override string ToString()
        {
            if (string.IsNullOrWhiteSpace(this.Name))
            {
                string items = string.Join("\r\n", this.Notes).Trim('\r', '\n');
                return items;

            }
            else
            {
                string title = $"{PREFIX}{this.Name}";

                string items = string.Join("\r\n", this.Notes).Trim('\r', '\n');
                return $"{title}\r\n\r\n{items}";
            }
        }
    }
}
