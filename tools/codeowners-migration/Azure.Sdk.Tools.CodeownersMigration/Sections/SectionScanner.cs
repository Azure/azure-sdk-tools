using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Azure.Sdk.Tools.CodeownersMigration.Sections
{
    /// <summary>
    /// A section discovered in a CODEOWNERS file.
    /// </summary>
    public class CodeownersSection
    {
        public string Name { get; set; }

        /// <summary>Index of the first line of the section heading.</summary>
        public int HeaderStart { get; set; }

        /// <summary>Index of the first line after the section heading.</summary>
        public int ContentStart { get; set; }

        /// <summary>Index one past the last content line (exclusive).</summary>
        public int SectionEnd { get; set; }

        /// <summary>True when the heading was the single line <c># #### Name ####</c> form.</summary>
        public bool IsCommentHeading { get; set; }
    }

    /// <summary>
    /// Enumerates every section in a CODEOWNERS file.
    ///
    /// Two heading styles are recognized. The first is the three line banner that
    /// <see cref="Azure.Sdk.Tools.CodeownersUtils.Utils.CodeownersSectionFinder"/> understands and that the
    /// tooling generates. The second is the single line <c># ######## Name ########</c> form that repositories
    /// use for sub-headings inside a banner section; azure-sdk-for-net keeps Core Libraries, Eng Sys and Code
    /// Generation that way. Migration has to see both, because the target design promotes them to real
    /// sections.
    /// </summary>
    public static class SectionScanner
    {
        private static readonly Regex CommentHeading = new Regex(@"^#\s*#{3,}\s*(?<name>.+?)\s*#{3,}\s*$", RegexOptions.Compiled);

        public static List<CodeownersSection> FindSections(List<string> lines, bool includeCommentHeadings = true)
        {
            var sections = new List<CodeownersSection>();

            for (int i = 0; i < lines.Count; i++)
            {
                if (i < lines.Count - 2 && IsSectionBorder(lines[i]) && IsSectionBorder(lines[i + 2]))
                {
                    string middle = lines[i + 1].Trim();
                    if (!IsSectionBorder(middle) && middle.StartsWith("#"))
                    {
                        string bannerName = middle.Substring(1).Trim();
                        if (bannerName.Length > 0)
                        {
                            sections.Add(new CodeownersSection
                            {
                                Name = bannerName,
                                HeaderStart = i,
                                ContentStart = i + 3
                            });
                            i += 2;
                            continue;
                        }
                    }
                }

                if (!includeCommentHeadings)
                {
                    continue;
                }

                Match match = CommentHeading.Match(lines[i].Trim());
                if (match.Success)
                {
                    sections.Add(new CodeownersSection
                    {
                        Name = match.Groups["name"].Value.Trim(),
                        HeaderStart = i,
                        ContentStart = i + 1,
                        IsCommentHeading = true
                    });
                }
            }

            // A section runs until the next heading of either style, or the end of the file.
            for (int i = 0; i < sections.Count; i++)
            {
                sections[i].SectionEnd = i + 1 < sections.Count ? sections[i + 1].HeaderStart : lines.Count;
            }

            return sections;
        }

        private static bool IsSectionBorder(string line)
        {
            string trimmed = line.Trim();
            return trimmed.Length >= 3 && trimmed.All(c => c == '#');
        }
    }
}
