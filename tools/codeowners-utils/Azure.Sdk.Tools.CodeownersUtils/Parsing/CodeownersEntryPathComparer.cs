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
            if (x == null && y == null)
            {
                return 0;
            }
            if (x == null)
            {
                return -1;
            }
            if (y == null)
            {
                return 1;
            }

            // FIRST PRIORITY: Treat catch-all patterns (like /sdk/) as the highest priority globally
            string pathX = x.PathExpression ?? string.Empty;
            string pathY = y.PathExpression ?? string.Empty;

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
                int catchAllAlpha = string.Compare(pathX, pathY, StringComparison.Ordinal);
                if (catchAllAlpha != 0)
                {
                    return catchAllAlpha;
                }
            }

            // SECOND PRIORITY: Prioritize ** wildcards next
            bool xHasDoubleWildcard = x.ContainsDoubleWildcard;
            bool yHasDoubleWildcard = y.ContainsDoubleWildcard;

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

            // Normalize paths by removing leading/trailing slashes for comparison
            string normalizedPathX = pathX.Trim('/');
            string normalizedPathY = pathY.Trim('/');

            // THIRD PRIORITY: Handle parent/child path relationships - ensure parent paths come before their subpaths
            // This MUST come before service label comparison because CODEOWNERS uses last-match-wins semantics.
            // If a child path appears before its parent, the parent's owners would incorrectly override
            // the child's more specific owners.
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

            // FOURTH PRIORITY: Compare by service label
            int serviceLabelComparison = CompareByServiceLabel(x, y);
            if (serviceLabelComparison != 0)
            {
                return serviceLabelComparison;
            }

            // If service labels are the same (or both empty), compare by path

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

            // Do alphabetical comparison of paths
            int alphabeticalComparison = string.Compare(normalizedPathX, normalizedPathY, StringComparison.Ordinal);

            // Handle wildcard precedence for similar paths
            if (alphabeticalComparison != 0)
            {
                // Check for simple wildcard cases like "sdk/storage*" vs "sdk/storage"
                if (HandleSimpleWildcardCase(x, y, out int wildcardResult))
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

            // FINAL TIEBREAKER: Ensure deterministic ordering for idempotent sorting
            // Compare the raw path expression first
            int finalPathComparison = string.Compare(
                x.PathExpression ?? string.Empty,
                y.PathExpression ?? string.Empty,
                StringComparison.Ordinal);

            if (finalPathComparison != 0)
            {
                return finalPathComparison;
            }

            // If paths are identical, compare by formatted output (includes all labels/owners)
            // This ensures no two semantically different entries ever compare as equal
            return string.Compare(
                x.FormatCodeownersEntry(useOriginalOwners: true),
                y.FormatCodeownersEntry(useOriginalOwners: true),
                StringComparison.Ordinal);
        }

        /// <summary>
        /// Handle simple wildcard cases where one path ends with * and matches the other path.
        /// </summary>
        private bool HandleSimpleWildcardCase(CodeownersEntry x, CodeownersEntry y, out int result)
        {
            result = 0;
            bool xHasWildcard = x.ContainsWildcard;
            bool yHasWildcard = y.ContainsWildcard;

            string pathX = NormalizePathExpression(x.PathExpression);
            string pathY = NormalizePathExpression(y.PathExpression);

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
        /// Normalizes a path expression by removing leading and trailing slashes.
        /// </summary>
        private static string NormalizePathExpression(string pathExpression)
        {
            return string.IsNullOrEmpty(pathExpression)
                ? string.Empty
                : pathExpression.Trim('/');
        }

        /// <summary>
        /// Compare two CodeownersEntry objects by their service labels.
        /// Uses primary service context (first ServiceLabel or PRLabel) for grouping,
        /// then compares all labels lexicographically for deterministic ordering within groups.
        /// </summary>
        private static int CompareByServiceLabel(CodeownersEntry x, CodeownersEntry y)
        {
            // First, compare by primary service context (for grouping like-service entries together)
            string primaryLabelX = GetPrimaryServiceContext(x);
            string primaryLabelY = GetPrimaryServiceContext(y);

            // If both have primary labels, compare them first for grouping
            if (!string.IsNullOrEmpty(primaryLabelX) && !string.IsNullOrEmpty(primaryLabelY))
            {
                int primaryCmp = string.Compare(primaryLabelX, primaryLabelY, StringComparison.OrdinalIgnoreCase);
                if (primaryCmp != 0)
                {
                    return primaryCmp;
                }
            }
            else if (!string.IsNullOrEmpty(primaryLabelX) && string.IsNullOrEmpty(primaryLabelY))
            {
                return -1; // x has primary label, y doesn't - x comes first
            }
            else if (string.IsNullOrEmpty(primaryLabelX) && !string.IsNullOrEmpty(primaryLabelY))
            {
                return 1; // y has primary label, x doesn't - y comes first
            }

            // Primary labels are equal (or both empty), now compare ALL labels for deterministic ordering
            // Compare all service labels lexicographically
            var serviceLabelsX = x.ServiceLabels ?? new List<string>();
            var serviceLabelsY = y.ServiceLabels ?? new List<string>();

            int minServiceCount = Math.Min(serviceLabelsX.Count, serviceLabelsY.Count);
            for (int i = 0; i < minServiceCount; i++)
            {
                string labelX = serviceLabelsX[i].TrimStart('%').Trim();
                string labelY = serviceLabelsY[i].TrimStart('%').Trim();
                int cmp = string.Compare(labelX, labelY, StringComparison.OrdinalIgnoreCase);
                if (cmp != 0)
                {
                    return cmp;
                }
            }

            // If all compared service labels are equal, the one with more labels comes later
            int serviceCountCmp = serviceLabelsX.Count.CompareTo(serviceLabelsY.Count);
            if (serviceCountCmp != 0)
            {
                return serviceCountCmp;
            }

            // Compare all PR labels lexicographically
            var prLabelsX = x.PRLabels ?? new List<string>();
            var prLabelsY = y.PRLabels ?? new List<string>();

            int minPrCount = Math.Min(prLabelsX.Count, prLabelsY.Count);
            for (int i = 0; i < minPrCount; i++)
            {
                string labelX = prLabelsX[i].TrimStart('%').Trim();
                string labelY = prLabelsY[i].TrimStart('%').Trim();
                int cmp = string.Compare(labelX, labelY, StringComparison.OrdinalIgnoreCase);
                if (cmp != 0)
                {
                    return cmp;
                }
            }

            // If all compared PR labels are equal, the one with more labels comes later
            return prLabelsX.Count.CompareTo(prLabelsY.Count);
        }

        /// <summary>
        /// Get the primary service context for an entry. This prioritizes ServiceLabel but falls back to the first PRLabel.
        /// This ensures entries with the same service name are grouped together regardless of label type.
        /// </summary>
        private static string GetPrimaryServiceContext(CodeownersEntry entry)
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
        private static bool IsCatchAllPattern(string path)
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
