using System;
using System.Collections.Generic;
using System.Linq;
using Azure.Sdk.Tools.CodeownersUtils.Parsing;

namespace Azure.Sdk.Tools.CodeownersMigration.Verification
{
    /// <summary>
    /// The semantic projection of a <see cref="CodeownersEntry"/>: everything GitHub and the Azure SDK
    /// tooling act on, with comments, blank lines and formatting removed.
    ///
    /// Owner lists are captured <em>unexpanded</em> (from the parser's <c>Original*</c> properties) so that
    /// a comparison is deterministic and does not depend on live GitHub team membership. Without this, a
    /// team gaining or losing a member between two runs would read as an ownership change.
    /// </summary>
    public class EntryFacts
    {
        /// <summary>
        /// The path expression, trimmed and given a leading slash. It is <em>not</em> otherwise normalized:
        /// '/sdk/foo' and '/sdk/foo/' select different files in GitHub's matcher, so treating them as equal
        /// would hide a real change.
        /// </summary>
        public string Path { get; set; }

        public List<string> SourceOwners { get; set; } = new List<string>();
        public List<string> PRLabels { get; set; } = new List<string>();
        public List<string> ServiceLabels { get; set; } = new List<string>();
        public List<string> ServiceOwners { get; set; } = new List<string>();
        public List<string> AzureSdkOwners { get; set; } = new List<string>();

        /// <summary>Zero-based line number of the originating block, for diagnostics.</summary>
        public int StartLine { get; set; }

        public static EntryFacts FromEntry(CodeownersEntry entry)
        {
            var facts = new EntryFacts
            {
                Path = string.IsNullOrWhiteSpace(entry.PathExpression) ? null : EnsureLeadingSlash(entry.PathExpression),
                SourceOwners = Clean(entry.OriginalSourceOwners),
                PRLabels = CleanLabels(entry.PRLabels),
                ServiceLabels = CleanLabels(entry.ServiceLabels),
                StartLine = entry.startLine
            };

            // The parser applies implicit owner inheritance: when a block carrying a '# ServiceLabel:' or an
            // empty '# AzureSdkOwners:' moniker ends in a source path/owner line, the source owners become
            // the service and Azure SDK owners. That is a semantic rule rather than formatting, and it does
            // not appear in the Original* properties, so it is re-applied here. The populated-but-undeclared
            // shape is what identifies it. Verified against the parser: an unexpandable team is retained
            // rather than dropped, so the expanded list is never empty merely because team data was
            // unavailable, and this test therefore does not depend on network access.
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

        /// <summary>A one-line summary of the entry, used when reporting an added or removed entry.</summary>
        public string Describe()
        {
            var parts = new List<string> { Path ?? "(no path)" };
            Append("source", SourceOwners);
            Append("service", ServiceOwners);
            Append("azuresdk", AzureSdkOwners);
            Append("pr-labels", PRLabels);
            Append("service-labels", ServiceLabels);
            return string.Join(", ", parts);

            void Append(string name, List<string> values)
            {
                if (values.Count > 0)
                {
                    parts.Add($"{name}: {string.Join(" ", values)}");
                }
            }
        }

        /// <summary>Trims the expression and guarantees a leading slash, changing nothing else.</summary>
        private static string EnsureLeadingSlash(string path)
        {
            string trimmed = path.Trim();
            return trimmed.Length == 0 || trimmed.StartsWith("/") ? trimmed : "/" + trimmed;
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

        /// <summary>
        /// Owners as a set. Ordering within a single entry is not semantic to GitHub, so it is not compared;
        /// the ordering that <em>is</em> semantic is the ordering of entries, which the comparer enforces.
        /// </summary>
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

        /// <summary>Labels as a set, on the same basis as <see cref="OwnerSet"/>.</summary>
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
