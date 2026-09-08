using System;
using System.Collections.Generic;
using System.Linq;
using Azure.Sdk.Tools.CodeownersUtils.Parsing;

namespace Azure.Sdk.Tools.CodeownersMigration.Verification
{
    /// <summary>
    /// The semantic projection of a <see cref="CodeownersEntry"/>: everything GitHub and the Azure SDK
    /// tooling actually acts on, with comments, blank lines, block ordering and team expansion removed.
    ///
    /// Owner lists are captured *unexpanded* (from the parser's Original* properties) so that comparisons
    /// are deterministic and do not depend on the live team membership blob. The parser's implicit owner
    /// inheritance is re-applied here because it is a semantic rule, not formatting: when a block ends in a
    /// source path/owner line, an empty AzureSdkOwners moniker and a ServiceLabel moniker both fall back to
    /// the source owners.
    /// </summary>
    public class EntryFacts
    {
        /// <summary>Normalized path expression, or null for a pathless (service label only) block.</summary>
        public string Path { get; set; }

        public List<string> SourceOwners { get; set; } = new List<string>();
        public List<string> PRLabels { get; set; } = new List<string>();
        public List<string> ServiceLabels { get; set; } = new List<string>();
        public List<string> ServiceOwners { get; set; } = new List<string>();
        public List<string> AzureSdkOwners { get; set; } = new List<string>();

        /// <summary>Line number of the originating block, for diagnostics.</summary>
        public int StartLine { get; set; }

        public bool HasPath => !string.IsNullOrEmpty(Path);

        public static EntryFacts FromEntry(CodeownersEntry entry)
        {
            var facts = new EntryFacts
            {
                Path = string.IsNullOrWhiteSpace(entry.PathExpression) ? null : NormalizePath(entry.PathExpression),
                SourceOwners = Clean(entry.OriginalSourceOwners),
                PRLabels = CleanLabels(entry.PRLabels),
                ServiceLabels = CleanLabels(entry.ServiceLabels),
                StartLine = entry.startLine
            };

            // ServiceOwners: prefer the explicitly declared (unexpanded) owners. If the parser produced
            // expanded service owners while none were declared, it inherited them from the source owners.
            facts.ServiceOwners = Clean(entry.OriginalServiceOwners);
            if (facts.ServiceOwners.Count == 0 && entry.ServiceOwners != null && entry.ServiceOwners.Count > 0)
            {
                facts.ServiceOwners = new List<string>(facts.SourceOwners);
            }

            facts.AzureSdkOwners = Clean(entry.OriginalAzureSdkOwners);
            if (facts.AzureSdkOwners.Count == 0 && entry.AzureSdkOwners != null && entry.AzureSdkOwners.Count > 0)
            {
                facts.AzureSdkOwners = new List<string>(facts.SourceOwners);
            }

            return facts;
        }

        /// <summary>
        /// Normalizes a path expression so that equivalent expressions written differently compare equal.
        /// A leading slash is added, and a trailing slash is added unless the final segment names a file or
        /// contains a glob.
        /// </summary>
        public static string NormalizePath(string path)
        {
            string normalized = path.Trim();
            if (normalized.Length == 0)
            {
                return normalized;
            }

            if (!normalized.StartsWith("/"))
            {
                normalized = "/" + normalized;
            }

            if (!normalized.EndsWith("/"))
            {
                string lastSegment = normalized.Substring(normalized.LastIndexOf('/') + 1);
                bool looksLikeFileOrGlob = lastSegment.Contains("*") || lastSegment.Contains(".");
                if (!looksLikeFileOrGlob)
                {
                    normalized += "/";
                }
            }

            return normalized;
        }

        /// <summary>Owner key used for comparison: case-insensitive and without the leading '@'.</summary>
        public static string OwnerKey(string owner)
        {
            string trimmed = (owner ?? string.Empty).Trim();
            if (trimmed.StartsWith("@"))
            {
                trimmed = trimmed.Substring(1);
            }
            return trimmed.ToLowerInvariant();
        }

        /// <summary>Label key used for comparison: case-insensitive and without the leading '%'.</summary>
        public static string LabelKey(string label)
        {
            string trimmed = (label ?? string.Empty).Trim();
            if (trimmed.StartsWith("%"))
            {
                trimmed = trimmed.Substring(1);
            }
            return trimmed.ToLowerInvariant();
        }

        public static SortedSet<string> OwnerSet(IEnumerable<string> owners)
        {
            var set = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string owner in owners ?? Enumerable.Empty<string>())
            {
                string key = OwnerKey(owner);
                if (key.Length > 0)
                {
                    set.Add(key);
                }
            }
            return set;
        }

        public static SortedSet<string> LabelSet(IEnumerable<string> labels)
        {
            var set = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string label in labels ?? Enumerable.Empty<string>())
            {
                string key = LabelKey(label);
                if (key.Length > 0)
                {
                    set.Add(key);
                }
            }
            return set;
        }

        private static List<string> Clean(List<string> values)
        {
            return (values ?? new List<string>())
                .Select(v => (v ?? string.Empty).Trim())
                .Where(v => v.Length > 0)
                .ToList();
        }

        private static List<string> CleanLabels(List<string> values)
        {
            return Clean(values).Select(v => v.StartsWith("%") ? v.Substring(1).Trim() : v).ToList();
        }
    }
}
