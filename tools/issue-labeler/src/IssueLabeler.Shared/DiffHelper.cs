// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text;

namespace IssueLabeler.Shared
{
    public struct SegmentedDiff
    {
        public string[] FileDiffs { get; set; }
        public IEnumerable<string> Filenames { get; set; }
        public IEnumerable<string> Extensions { get; set; }
        public Dictionary<string, int> Folders { get; set; }
        public Dictionary<string, int> FolderNames { get; set; }
        public bool AddDocInfo { get; set; }
        public bool PossiblyExtensionsLabel { get; set; }
    }

    public class DiffHelper : IDiffHelper
    {
        /// <summary>
        /// name of files taken from fileDiffs
        /// </summary>
        public IEnumerable<string> FilenamesOf(string[] fileDiffs) => fileDiffs.Select(fileWithDiff => Path.GetFileNameWithoutExtension(fileWithDiff));

        /// <summary>
        /// file extensions taken from fileDiffs
        /// </summary>
        public IEnumerable<string> ExtensionsOf(string[] fileDiffs) => fileDiffs.Select(file => Path.GetExtension(file)).
                Select(extension => string.IsNullOrEmpty(extension) ? "no_extension" : extension);

        public SegmentedDiff SegmentDiff(string[] fileDiffs)
        {
            if (fileDiffs == null || string.IsNullOrEmpty(string.Join(';', fileDiffs)))
            {
                throw new ArgumentNullException(nameof(fileDiffs));
            }
            var folderNames = new Dictionary<string, int>();
            var folders = new Dictionary<string, int>();
            bool addDocInfo = false, possiblyExtensionsLabel = false;
            string folderWithDiff, subfolder;
            string[] folderNamesInPr;
            foreach (var fileWithDiff in fileDiffs)
            {
                folderWithDiff = Path.GetDirectoryName(fileWithDiff) ?? string.Empty;
                folderNamesInPr = folderWithDiff.Split(Path.DirectorySeparatorChar);
                subfolder = string.Empty;
                if (!string.IsNullOrEmpty(folderWithDiff))
                {
                    foreach (var folderNameInPr in folderNamesInPr)
                    {
                        if (folderNameInPr.Equals("ref", StringComparison.Ordinal) &&
                            subfolder.StartsWith("src" + Path.DirectorySeparatorChar + "libraries", StringComparison.Ordinal) &&
                            Path.GetExtension(fileWithDiff).Equals(".cs", StringComparison.OrdinalIgnoreCase))
                        {
                            addDocInfo = true;
                        }
                        if (subfolder.StartsWith("src" + Path.DirectorySeparatorChar + "libraries" + Path.DirectorySeparatorChar + "Microsoft.Extensions.", StringComparison.Ordinal) &&
                            Path.GetExtension(fileWithDiff).Equals(".cs", StringComparison.OrdinalIgnoreCase))
                        {
                            possiblyExtensionsLabel = true;
                        }
                        subfolder += folderNameInPr;
                        if (folderNames.ContainsKey(folderNameInPr))
                        {
                            folderNames[folderNameInPr] += 1;
                        }
                        else
                        {
                            folderNames.Add(folderNameInPr, 1);
                        }
                        if (folders.ContainsKey(subfolder))
                        {
                            folders[subfolder] += 1;
                        }
                        else
                        {
                            folders.Add(subfolder, 1);
                        }
                        subfolder += Path.DirectorySeparatorChar;
                    }
                }
            }
            return new SegmentedDiff()
            {
                FileDiffs = fileDiffs,
                Filenames = FilenamesOf(fileDiffs),
                Extensions = ExtensionsOf(fileDiffs),
                Folders = folders,
                FolderNames = folderNames,
                AddDocInfo = addDocInfo,
                PossiblyExtensionsLabel = possiblyExtensionsLabel
            };
        }

        /// <summary>
        /// flattens a dictionary to be repeated in a space separated format
        /// </summary>
        /// <param name="textToFlatten">a dictionary containing text and number of times they were repeated</param>
        /// <returns>space delimited text</returns>
        public string FlattenWithWhitespace(Dictionary<string, int> folder)
        {
            var folderSb = new StringBuilder();
            foreach (var f in folder.OrderByDescending(x => x.Value))
            {
                Debug.Assert(f.Value >= 1);
                folderSb.Append(f.Key);
                for (var j = 0; j < f.Value - 1; j++)
                {
                    folderSb.Append(" ").Append(f.Key);
                }
                folderSb.Append(" ");
            }
            if (folderSb.Length == 0)
            {
                return string.Empty;
            }
            folderSb.Length--;
            return folderSb.ToString();
        }
    }
}
