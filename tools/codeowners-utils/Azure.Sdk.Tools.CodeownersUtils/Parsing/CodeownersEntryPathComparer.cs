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

            // First, compare by service label
            int serviceLabelComparison = CompareByServiceLabel(x, y);
            if (serviceLabelComparison != 0)
            {
                return serviceLabelComparison;
            }

            // If service labels are the same (or both empty), compare by path
            string pathX = x.PathExpression ?? string.Empty;
            string pathY = y.PathExpression ?? string.Empty;

            // Normalize paths by removing leading slashes for comparison
            pathX = pathX.TrimStart('/');
            pathX = pathX.TrimEnd('/');
            pathY = pathY.TrimStart('/');
            pathY = pathY.TrimEnd('/');

            // If both paths are empty or whitespace, they're equal at this point
            if (string.IsNullOrWhiteSpace(pathX) && string.IsNullOrWhiteSpace(pathY))
            {
                return 0;
            }

            // Handle parent/child path relationships
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

            // Both paths exist and neither is a parent of the other, compare alphabetically
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
                    return ExtractBaseServiceName(serviceLabel);
                }
            }

            // Second priority: PR labels (use the first one as primary context)
            if (entry.PRLabels?.Count > 0)
            {
                var prLabel = entry.PRLabels[0].TrimStart('%').Trim();
                if (!string.IsNullOrEmpty(prLabel))
                {
                    return ExtractBaseServiceName(prLabel);
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Extract the base service name from a label. For hierarchical services like "Communication - Call Automation",
        /// this returns "Communication". For simple services like "Storage", this returns "Storage".
        /// </summary>
        private string ExtractBaseServiceName(string label)
        {
            if (string.IsNullOrEmpty(label))
                return string.Empty;

            // For hierarchical services separated by " - ", take the first part
            var dashIndex = label.IndexOf(" - ");
            if (dashIndex > 0)
            {
                return label.Substring(0, dashIndex).Trim();
            }

            return label.Trim();
        }
    }
}
