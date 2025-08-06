using System;
using System.Collections.Generic;
using System.Linq;

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

            // FIRST PRIORITY: Prioritize ** wildcards at the very top globally
            string pathX = x.PathExpression ?? string.Empty;
            string pathY = y.PathExpression ?? string.Empty;
            
            bool xHasDoubleWildcard = pathX.Contains("**");
            bool yHasDoubleWildcard = pathY.Contains("**");

            if (xHasDoubleWildcard && !yHasDoubleWildcard)
            {
                return -1; // x (with **) comes before y (without **)
            }
            if (!xHasDoubleWildcard && yHasDoubleWildcard)
            {
                return 1; // y (with **) comes before x (without **)
            }

            // If both have ** wildcards, sort by path first (natural alphabetical order handles specificity)
            // e.g., "/**/" comes before "/**/*Management*/" comes before "/**/Azure.ResourceManager*/"
            if (xHasDoubleWildcard && yHasDoubleWildcard)
            {
                int pathComparison = string.Compare(pathX, pathY, StringComparison.Ordinal);
                if (pathComparison != 0)
                {
                    return pathComparison;
                }
            }

            // Second, compare by service label
            int serviceLabelComparison = CompareByServiceLabel(x, y);
            if (serviceLabelComparison != 0)
            {
                return serviceLabelComparison;
            }

            // If service labels are the same (or both empty), compare by path

            // Normalize paths by removing leading slashes for comparison
            pathX = pathX.TrimStart('/');
            pathX = pathX.TrimEnd('/');
            pathY = pathY.TrimStart('/');
            pathY = pathY.TrimEnd('/');

            // Handle cases where one has a path and the other doesn't
            if (string.IsNullOrWhiteSpace(pathX) && !string.IsNullOrWhiteSpace(pathY))
            {
                return 1; // y has path, x doesn't - y comes first (path-based entries before service-label-only entries)
            }
            if (!string.IsNullOrWhiteSpace(pathX) && string.IsNullOrWhiteSpace(pathY))
            {
                return -1; // x has path, y doesn't - x comes first (path-based entries before service-label-only entries)
            }
            
            // If both paths are empty or whitespace, they're equal at this level
            if (string.IsNullOrWhiteSpace(pathX) && string.IsNullOrWhiteSpace(pathY))
            {
                return 0;
            }

            // Both paths exist, handle parent/child path relationships first
            // Ensure parent paths come before their subpaths
            if (!string.IsNullOrWhiteSpace(pathX) && !string.IsNullOrWhiteSpace(pathY))
            {
                // Check if one path is a parent of the other
                if (pathY.StartsWith(pathX + "/", StringComparison.Ordinal))
                {
                    return -1; // pathX is parent of pathY, so pathX comes first
                }
                if (pathX.StartsWith(pathY + "/", StringComparison.Ordinal))
                {
                    return 1; // pathY is parent of pathX, so pathY comes first
                }
            }

            // Do alphabetical comparison of paths
            int alphabeticalComparison = string.Compare(pathX, pathY, StringComparison.Ordinal);

            // Before returning the alphabetical comparison, check for simple wildcard cases
            // Handle cases like "sdk/storage*" vs "sdk/storage"
            if (alphabeticalComparison != 0)
            {
                bool xHasWildcard = x.ContainsWildcard;
                bool yHasWildcard = y.ContainsWildcard;

                // Simple check: if one path ends with * and matches the other path, prioritize the wildcard
                if (xHasWildcard && !yHasWildcard && pathX.EndsWith("*") && pathX.TrimEnd('*') == pathY)
                {
                    return -1; // x (with wildcard) comes before y (without wildcard)
                }
                if (!xHasWildcard && yHasWildcard && pathY.EndsWith("*") && pathY.TrimEnd('*') == pathX)
                {
                    return 1; // y (with wildcard) comes before x (without wildcard)
                }

                // Otherwise, use alphabetical comparison
                return alphabeticalComparison;
            }

            // If paths are the same alphabetically, prioritize wildcard paths
            // (this handles cases where paths are identical but one has additional wildcards)
            bool xHasWildcardSame = x.ContainsWildcard;
            bool yHasWildcardSame = y.ContainsWildcard;

            if (xHasWildcardSame && !yHasWildcardSame)
            {
                return -1; // x (with wildcard) comes before y (without wildcard)
            }
            if (!xHasWildcardSame && yHasWildcardSame)
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
            // Get the primary service context for each entry (service label first, then PR label)
            string primaryLabelX = GetPrimaryServiceContext(x);
            string primaryLabelY = GetPrimaryServiceContext(y);

            // If both have primary labels, compare them alphabetically
            if (!string.IsNullOrEmpty(primaryLabelX) && !string.IsNullOrEmpty(primaryLabelY))
            {
                return string.Compare(primaryLabelX, primaryLabelY, StringComparison.OrdinalIgnoreCase);
            }

            // If only one has a primary label, prioritize the one with the label
            if (!string.IsNullOrEmpty(primaryLabelX) && string.IsNullOrEmpty(primaryLabelY))
            {
                return -1; // x has primary label, y doesn't - x comes first
            }
            if (string.IsNullOrEmpty(primaryLabelX) && !string.IsNullOrEmpty(primaryLabelY))
            {
                return 1; // y has primary label, x doesn't - y comes first
            }

            // If neither has labels, they're equal at this level
            return 0;
        }

        /// <summary>
        /// Get the primary service context for an entry. This prioritizes ServiceLabel but falls back to the first PRLabel.
        /// This ensures entries with combined PR labels like "AI Model Inference %AI Projects" are grouped with 
        /// individual "AI Model Inference" entries. For hierarchical services like "Communication - Call Automation",
        /// this extracts the base service name "Communication" for grouping.
        /// </summary>
        private string GetPrimaryServiceContext(CodeownersEntry entry)
        {
            // First priority: Service labels
            if (entry.ServiceLabels?.Count > 0)
            {
                var serviceLabel = entry.ServiceLabels[0].TrimStart('%').Trim();
                if (!string.IsNullOrEmpty(serviceLabel))
                {
                    return serviceLabel;
                }
            }

            // Second priority: PR labels (use the first one as primary context)
            if (entry.PRLabels?.Count > 0)
            {
                var prLabel = entry.PRLabels[0].TrimStart('%').Trim();
                if (!string.IsNullOrEmpty(prLabel))
                {
                    return prLabel;
                }
            }

            return string.Empty;
        }
    }
}
