using System;
using System.Collections.Generic;
using System.Linq;

namespace Azure.Sdk.Tools.CodeownersMigration.Conversion
{
    /// <summary>
    /// Interprets the fragment location glob (for example <c>sdk/*/owners.yaml</c>) and decides which
    /// CODEOWNERS path expressions can be moved into a fragment and where that fragment lives.
    /// </summary>
    public class FragmentPrefix
    {
        private readonly List<string> _segments;

        public string FileName { get; private set; }

        private FragmentPrefix(List<string> segments, string fileName)
        {
            _segments = segments;
            FileName = fileName;
        }

        public static FragmentPrefix Parse(string glob)
        {
            List<string> parts = (glob ?? string.Empty)
                .Replace('\\', '/')
                .Trim('/')
                .Split('/')
                .Where(p => p.Length > 0)
                .ToList();

            if (parts.Count < 2)
            {
                throw new ArgumentException($"Fragment glob '{glob}' must contain at least one directory segment and a file name.");
            }

            string fileName = parts[parts.Count - 1];
            parts.RemoveAt(parts.Count - 1);
            return new FragmentPrefix(parts, fileName);
        }

        public static IEnumerable<string> AllowedGlobs(string glob)
        {
            yield return glob;
            if (glob.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))
            {
                yield return glob.Substring(0, glob.Length - 5) + ".yml";
            }
        }

        /// <summary>
        /// Returns the repo-relative fragment root directory for a normalized path expression, or null when the
        /// path cannot be owned by a fragment. A path fails attribution when it is shallower than the glob, when
        /// a literal segment does not match, or when a glob character appears in the part of the path that would
        /// become the fragment's location - a fragment directory has to be a real directory.
        /// </summary>
        public string RootFor(string normalizedPath)
        {
            List<string> segments = SplitPath(normalizedPath);
            if (segments.Count < _segments.Count)
            {
                return null;
            }

            for (int i = 0; i < _segments.Count; i++)
            {
                string expected = _segments[i];
                string actual = segments[i];

                if (actual.Contains("*"))
                {
                    return null;
                }

                if (expected == "*")
                {
                    continue;
                }

                if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }
            }

            return string.Join("/", segments.Take(_segments.Count));
        }

        /// <summary>
        /// Expresses a normalized path relative to its fragment root. The fragment directory itself is ".".
        /// A trailing '/' is significant in a CODEOWNERS expression -- it is what restricts the match to a
        /// directory -- so it is carried across, since splitting into segments discards it.
        /// </summary>
        public string RelativePath(string root, string normalizedPath)
        {
            List<string> segments = SplitPath(normalizedPath);
            List<string> remainder = segments.Skip(_segments.Count).ToList();
            if (remainder.Count == 0)
            {
                return ".";
            }

            string relative = string.Join("/", remainder);
            return normalizedPath.EndsWith("/", StringComparison.Ordinal) ? relative + "/" : relative;
        }

        private static List<string> SplitPath(string path)
        {
            return (path ?? string.Empty)
                .Trim('/')
                .Split('/')
                .Where(p => p.Length > 0)
                .ToList();
        }
    }
}
