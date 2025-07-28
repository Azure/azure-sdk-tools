using System;
using System.Collections.Generic;

namespace Azure.Sdk.Tools.CodeownersUtils.Parsing
{
    /// <summary>
    /// Custom comparer for CodeownersEntry that sorts by path alphabetically,
    /// with wildcard paths appearing before non-wildcard paths when they share the same base.
    /// </summary>
    public class CodeownersEntryPathComparer : IComparer<CodeownersEntry>
    {
        public int Compare(CodeownersEntry x, CodeownersEntry y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            string pathX = x.PathExpression ?? string.Empty;
            string pathY = y.PathExpression ?? string.Empty;

            // Normalize paths by removing leading slashes for comparison
            pathX = pathX.TrimStart('/');
            pathX = pathX.TrimEnd('/');
            pathY = pathY.TrimStart('/');
            pathY = pathY.TrimEnd('/');

            // If both paths are empty or whitespace, sort by service label
            if (string.IsNullOrWhiteSpace(pathX) && string.IsNullOrWhiteSpace(pathY))
            {
                return CompareByServiceLabel(x, y);
            }

            // If one path is empty and the other isn't, prioritize the non-empty path
            if (string.IsNullOrWhiteSpace(pathX) && !string.IsNullOrWhiteSpace(pathY))
            {
                return 1; // y (with path) comes before x (without path)
            }
            if (!string.IsNullOrWhiteSpace(pathX) && string.IsNullOrWhiteSpace(pathY))
            {
                return -1; // x (with path) comes before y (without path)
            }

            // Both paths exist, compare alphabetically
            int alphabeticalComparison = string.Compare(pathX, pathY, StringComparison.Ordinal);

            // If paths are different alphabetically, return that comparison
            if (alphabeticalComparison != 0)
            {
                return alphabeticalComparison;
            }

            // If paths are the same alphabetically, prioritize wildcard paths
            // (this handles cases like "sdk/path/service*" vs "sdk/path/service")
            bool xHasWildcard = x.ContainsWildcard;
            bool yHasWildcard = y.ContainsWildcard;

            if (xHasWildcard && !yHasWildcard)
            {
                return -1; // x (with wildcard) comes before y (without wildcard)
            }
            if (!xHasWildcard && yHasWildcard)
            {
                return 1; // y (with wildcard) comes before x (without wildcard)
            }

            // If both have wildcards or both don't have wildcards, they're equal
            return 0;
        }

        /// <summary>
        /// Compare two CodeownersEntry objects by their service labels.
        /// </summary>
        private int CompareByServiceLabel(CodeownersEntry x, CodeownersEntry y)
        {
            // Get the first service label from each entry (if any)
            string serviceLabelX = x.ServiceLabels?.Count > 0 ? x.ServiceLabels[0] : string.Empty;
            string serviceLabelY = y.ServiceLabels?.Count > 0 ? y.ServiceLabels[0] : string.Empty;

            // Compare service labels alphabetically
            return string.Compare(serviceLabelX, serviceLabelY, StringComparison.OrdinalIgnoreCase);
        }
    }
}
