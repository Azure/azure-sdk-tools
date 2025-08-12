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

            // SECOND PRIORITY: Handle catch-all patterns (like /sdk/) that should appear at the top of their sections
            bool xIsCatchAll = IsCatchAllPattern(pathX);
            bool yIsCatchAll = IsCatchAllPattern(pathY);

            if (xIsCatchAll && !yIsCatchAll)
            {
                return -1; // x (catch-all) comes before y (non-catch-all)
            }
            if (!xIsCatchAll && yIsCatchAll)
            {
                return 1; // y (catch-all) comes before x (non-catch-all)
            }

            // If both are catch-all patterns, sort them by path length (shorter = more general)
            if (xIsCatchAll && yIsCatchAll)
            {
                int lengthComparison = pathX.Length.CompareTo(pathY.Length);
                if (lengthComparison != 0)
                {
                    return lengthComparison;
                }
                // If same length, sort alphabetically
                return string.Compare(pathX, pathY, StringComparison.Ordinal);
            }

            // Third, compare by service label
            int serviceLabelComparison = CompareByServiceLabel(x, y);
            if (serviceLabelComparison != 0)
            {
                return serviceLabelComparison;
            }

            // If service labels are the same (or both empty), compare by path

            // Normalize paths by removing leading/trailing slashes for comparison
            string normalizedPathX = pathX.Trim('/');
            string normalizedPathY = pathY.Trim('/');

            // Handle empty path cases
            if (string.IsNullOrWhiteSpace(normalizedPathX) && !string.IsNullOrWhiteSpace(normalizedPathY))
            {
                return 1; // y has path, x doesn't - y comes first
            }
            if (!string.IsNullOrWhiteSpace(normalizedPathX) && string.IsNullOrWhiteSpace(normalizedPathY))
            {
                return -1; // x has path, y doesn't - x comes first
            }
            
            // If both paths are empty, they're equal at this level
            if (string.IsNullOrWhiteSpace(normalizedPathX) && string.IsNullOrWhiteSpace(normalizedPathY))
            {
                return 0;
            }

            // Handle parent/child path relationships - ensure parent paths come before their subpaths
            if (!string.IsNullOrWhiteSpace(normalizedPathX) && !string.IsNullOrWhiteSpace(normalizedPathY))
            {
                // Check if one path is a parent of the other
                if (normalizedPathY.StartsWith(normalizedPathX + "/", StringComparison.Ordinal))
                {
                    return -1; // pathX is parent of pathY, so pathX comes first
                }
                if (normalizedPathX.StartsWith(normalizedPathY + "/", StringComparison.Ordinal))
                {
                    return 1; // pathY is parent of pathX, so pathY comes first
                }
            }

            // Do alphabetical comparison of paths
            int alphabeticalComparison = string.Compare(normalizedPathX, normalizedPathY, StringComparison.Ordinal);

            // Handle wildcard precedence for similar paths
            if (alphabeticalComparison != 0)
            {
                // Check for simple wildcard cases like "sdk/storage*" vs "sdk/storage"
                if (HandleSimpleWildcardCase(x, y, normalizedPathX, normalizedPathY, out int wildcardResult))
                {
                    return wildcardResult;
                }
                
                return alphabeticalComparison;
            }

            // If paths are identical alphabetically, prioritize wildcard paths
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
        /// Handle simple wildcard cases where one path ends with * and matches the other path.
        /// </summary>
        private bool HandleSimpleWildcardCase(CodeownersEntry x, CodeownersEntry y, string pathX, string pathY, out int result)
        {
            result = 0;
            bool xHasWildcard = x.ContainsWildcard;
            bool yHasWildcard = y.ContainsWildcard;

            // Simple check: if one path ends with * and matches the other path, prioritize the wildcard
            if (xHasWildcard && !yHasWildcard && pathX.EndsWith("*") && pathX.TrimEnd('*') == pathY)
            {
                result = -1; // x (with wildcard) comes before y (without wildcard)
                return true;
            }
            if (!xHasWildcard && yHasWildcard && pathY.EndsWith("*") && pathY.TrimEnd('*') == pathX)
            {
                result = 1; // y (with wildcard) comes before x (without wildcard)
                return true;
            }

            return false;
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

        /// <summary>
        /// Determines if a path is a catch-all pattern that should appear at the beginning of a section.
        /// Catch-all patterns are simple directory paths like "/sdk/" that don't contain wildcards
        /// but serve as fallback patterns for entire directory trees.
        /// </summary>
        private bool IsCatchAllPattern(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            // Normalize path by removing leading/trailing slashes
            string normalizedPath = path.Trim('/');
            
            // A catch-all pattern is a single directory name without subdirectories or wildcards
            // Examples: "/sdk/" -> "sdk", "/core/" -> "core"
            // Not catch-all: "/sdk/storage/", "/sdk/*", "/**/"
            return !string.IsNullOrEmpty(normalizedPath) && 
                   !normalizedPath.Contains('/') && 
                   !normalizedPath.Contains('*') && 
                   !normalizedPath.Contains('?');
        }
    }
}
