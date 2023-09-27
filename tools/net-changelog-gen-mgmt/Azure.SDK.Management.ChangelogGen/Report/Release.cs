// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text;
using System.Text.RegularExpressions;
using Azure.SDK.ChangelogGen.Utilities;

namespace Azure.SDK.ChangelogGen.Report
{
    public class Release
    {
        private const string PREFIX = "## ";
        private const string CHANGELOG_FIRST_LINE = "# Release History";

        public string Version { get; set; }
        public string ReleaseDate { get; set; }
        public List<ReleaseNoteGroup> Groups { get; } = new List<ReleaseNoteGroup>();

        public Release(string version, string releaseDate)
        {
            this.ReleaseDate = releaseDate;
            this.Version = version;
        }

        public override string ToString()
        {
            return this.ToString(true);
        }

        public string ToString(bool ignoreEmptyGroup)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"{PREFIX}{Version} ({ReleaseDate})");
            sb.AppendLine();

            foreach (var group in this.Groups)
            {
                if (group.Notes.Sum(s => s.Note.Length) > 0 || !ignoreEmptyGroup)
                {
                    sb.AppendLine(group.ToString());
                    sb.AppendLine();
                }
            }
            return sb.ToString().Trim('\r', '\n');
        }

        public void MergeTo(Release to, MergeMode mode)
        {
            switch (mode)
            {
                case MergeMode.Line:
                    this.MergeByLine(to);
                    break;
                case MergeMode.Group:
                    this.MergeByGrup(to);
                    break;
                case MergeMode.OverWrite:
                    this.MergeByOverwrite(to);
                    break;
                default:
                    throw new InvalidOperationException("Unknown merge mode " + mode);
            }
        }

        private void MergeByOverwrite(Release to)
        {
            to.Groups.Clear();
            to.Groups.AddRange(this.Groups);
        }

        private void MergeByGrup(Release to)
        {
            foreach (var fromGroup in this.Groups)
            {
                var found = to.Groups.FirstOrDefault(g => g.Name == fromGroup.Name);
                if (found != null)
                    to.Groups.Remove(found);
            }
            to.Groups.AddRange(this.Groups);
        }

        private void MergeByLine(Release to)
        {
            foreach (var fromGroup in this.Groups)
            {
                var toGroup = to.Groups.FirstOrDefault(g => string.Equals(g.Name, fromGroup.Name, StringComparison.OrdinalIgnoreCase));
                if (toGroup == null)
                {
                    to.Groups.Add(fromGroup);
                }
                else
                {
                    int indexToInsert = toGroup.Notes.FindLastIndex(n => !string.IsNullOrEmpty(n.Note)) + 1;
                    int lastNonEmptyIndex = fromGroup.Notes.FindLastIndex(n => !string.IsNullOrEmpty(n.Note));
                    for (int i = lastNonEmptyIndex; i >= 0; i--)
                    {
                        var fromItem = fromGroup.Notes[i];
                        var toItem = toGroup.Notes.FirstOrDefault(t => string.Equals(fromItem.ToString(), t.ToString(), StringComparison.OrdinalIgnoreCase));
                        if (toItem == null)
                        {
                            toGroup.Notes.Insert(indexToInsert, fromItem);
                        }
                        else
                        {
                            Logger.Verbose($"Release Note: {fromGroup.Name} -> {fromItem} ignored in merge");
                        }
                    }
                }
            }
        }

        public static string ToChangeLog(List<Release> releases)
        {
            return CHANGELOG_FIRST_LINE + "\r\n\r\n" +
                string.Join("\r\n\r\n", releases.Select(r => r.ToString()));
        }

        public static List<Release> FromChangelog(string changelog)
        {
            if (changelog == null)
                throw new ArgumentNullException(nameof(changelog));

            List<Release> releases = new List<Release>();
            var lines = changelog.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None);
            if (lines[0] != CHANGELOG_FIRST_LINE)
                throw new InvalidOperationException($"Unexpected first line in change log. '{CHANGELOG_FIRST_LINE}' expected");
            int i = 1;
            while (string.IsNullOrWhiteSpace(lines[i]))
                i++;

            if (i >= lines.Length)
                return releases;

            if (!TryParseReleaseTitle(lines[i], out Release? firstRelease))
                throw new InvalidOperationException("Can't find first release info in changelog: " + lines[i]);
            Release curRelease = firstRelease!;

            ReleaseNoteGroup curGroup = new ReleaseNoteGroup("");
            for (i = i + 1; i < lines.Length; i++)
            {
                if (ReleaseNoteGroup.TryParseGroupTitle(lines[i], out ReleaseNoteGroup? newGroup))
                {
                    curRelease.Groups.Add(curGroup);
                    curGroup = newGroup!;
                }
                else if (Release.TryParseReleaseTitle(lines[i], out Release? newRelease))
                {
                    curRelease.Groups.Add(curGroup);
                    curGroup = new ReleaseNoteGroup("");
                    releases.Add(curRelease);
                    curRelease = newRelease!;
                }
                else
                {
                    ReleaseNote item = new ReleaseNote(lines[i]);
                    curGroup.Notes.Add(item);
                }
            }
            curRelease.Groups.Add(curGroup);
            releases.Add(curRelease);
            return releases;
        }

        public static bool TryParseReleaseTitle(string line, out Release? release)
        {
            Regex reg = new Regex(@"^##\s+(?<version>[\w\.\-]+?)\s+\((?<release>\d{4}-\d{2}-\d{2}|Unreleased)\)\s*$",
                RegexOptions.Multiline | RegexOptions.IgnoreCase);

            Match m = reg.Match(line);
            if (!m.Success)
            {
                release = null;
                return false;
            }
            else
            {
                release = new Release(m.Groups["version"].Value, m.Groups["release"].Value);
                return true;
            }
        }

        public static Release ParseReleaseTitle(string line)
        {
            if (TryParseReleaseTitle(line, out Release? release))
            {
                return release!;
            }
            else
            {
                throw new InvalidOperationException(
                    $"Unexpected release version/date format. Expected: 'version (xxxx-xx-xx or Unreleased)' Actual: '${line}'");
            }
        }
    }
}
