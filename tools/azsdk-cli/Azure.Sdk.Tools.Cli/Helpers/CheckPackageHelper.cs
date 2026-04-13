// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models.Responses.Codeowners;
using Azure.Sdk.Tools.CodeownersUtils.Parsing;
using Azure.Sdk.Tools.CodeownersUtils.Utils;

namespace Azure.Sdk.Tools.Cli.Helpers;

public interface ICheckPackageHelper
{
    /// <summary>
    /// Validates that a path has sufficient owners, PR labels, and service owners
    /// based on parsed CODEOWNERS entries. Throws on validation failure.
    /// </summary>
    /// <param name="directoryPath">Relative path from repo root to the package directory.</param>
    /// <param name="codeownersEntries">Pre-parsed CODEOWNERS entries (teams already expanded).</param>
    /// <returns>A <see cref="CheckPackageResponse"/> on success.</returns>
    /// <exception cref="InvalidOperationException">Thrown when validation fails.</exception>
    CheckPackageResponse CheckPackage(
        string directoryPath,
        List<CodeownersEntry> codeownersEntries);
}

public class CheckPackageHelper : ICheckPackageHelper
{
    private const int MinimumOwnerCount = 2;

    public CheckPackageResponse CheckPackage(
        string directoryPath,
        List<CodeownersEntry> codeownersEntries)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new ArgumentException("Directory path is required.", nameof(directoryPath));
        }
        if (codeownersEntries == null || codeownersEntries.Count == 0)
        {
            throw new ArgumentException("CODEOWNERS entries list is empty.", nameof(codeownersEntries));
        }

        var matchedEntry = FindMatchingEntry(directoryPath, codeownersEntries);
        ThrowIfInsufficientOwners(matchedEntry, directoryPath);
        ThrowIfNoPRLabels(matchedEntry, directoryPath);
        var (serviceOwners, serviceLabels) = ThrowIfInsufficientServiceOwners(matchedEntry, codeownersEntries);

        return new CheckPackageResponse
        {
            DirectoryPath = directoryPath,
            Owners = matchedEntry.SourceOwners,
            PRLabels = matchedEntry.PRLabels,
            ServiceOwners = serviceOwners,
            ServiceLabels = serviceLabels,
        };
    }

    /// <summary>
    /// Finds the matching CODEOWNERS entry by scanning in reverse order (last match wins).
    /// Only considers entries that have a path expression (skips service-label-only entries).
    /// Tries the path as-is first, then with a trailing slash appended to handle
    /// directory-style CODEOWNERS patterns (e.g., /sdk/foo/ matching sdk/foo).
    /// </summary>
    internal static CodeownersEntry FindMatchingEntry(string directoryPath, List<CodeownersEntry> entries)
    {
        // Build the set of target paths to try: the original path, and if it doesn't
        // end with '/', also try with a trailing slash appended. CODEOWNERS directory
        // patterns like /sdk/foo/ only match paths *under* that directory, so
        // "sdk/foo/" will match but "sdk/foo" will not.
        var pathsToTry = new List<string> { directoryPath };
        if (!directoryPath.EndsWith("/"))
        {
            pathsToTry.Add(directoryPath + "/");
        }

        for (int i = entries.Count - 1; i >= 0; i--)
        {
            var entry = entries[i];
            if (string.IsNullOrWhiteSpace(entry.PathExpression))
            {
                continue;
            }

            foreach (var targetPath in pathsToTry)
            {
                if (DirectoryUtils.PathExpressionMatchesTargetPath(entry.PathExpression, targetPath))
                {
                    return entry;
                }
            }
        }

        throw new InvalidOperationException(
            $"check-package failed: No CODEOWNERS entry matches path '{directoryPath}'.");
    }

    /// <summary>
    /// Validates that the matched entry has at least <see cref="MinimumOwnerCount"/> unique source owners.
    /// </summary>
    internal static void ThrowIfInsufficientOwners(CodeownersEntry entry, string directoryPath)
    {
        var uniqueOwners = entry.SourceOwners
            .Select(o => o.TrimStart('@').Trim())
            .Where(o => !string.IsNullOrEmpty(o))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (uniqueOwners.Count < MinimumOwnerCount)
        {
            throw new InvalidOperationException(
                $"check-package failed for path '{directoryPath}': " +
                $"Found {uniqueOwners.Count} unique owner(s) but at least {MinimumOwnerCount} are required. " +
                $"Owners: [{string.Join(", ", entry.SourceOwners)}]");
        }
    }

    /// <summary>
    /// Validates that the matched entry has at least one PR label.
    /// </summary>
    internal static void ThrowIfNoPRLabels(CodeownersEntry entry, string directoryPath)
    {
        if (entry.PRLabels == null || entry.PRLabels.Count == 0)
        {
            throw new InvalidOperationException(
                $"check-package failed for path '{directoryPath}': " +
                $"No PR labels found on the matching CODEOWNERS entry (path expression: '{entry.PathExpression}').");
        }
    }

    /// <summary>
    /// Finds the first CODEOWNERS entry whose ServiceLabels are a superset of the matched entry's PRLabels,
    /// and validates that it has at least <see cref="MinimumOwnerCount"/> unique service owners.
    /// </summary>
    internal static (List<string> serviceOwners, List<string> serviceLabels) ThrowIfInsufficientServiceOwners(
        CodeownersEntry matchedEntry,
        List<CodeownersEntry> allEntries)
    {
        var requiredLabels = new HashSet<string>(matchedEntry.PRLabels, StringComparer.OrdinalIgnoreCase);

        foreach (var entry in allEntries)
        {
            if (entry.ServiceLabels == null || entry.ServiceLabels.Count == 0)
            {
                continue;
            }

            var entryServiceLabels = new HashSet<string>(entry.ServiceLabels, StringComparer.OrdinalIgnoreCase);
            if (requiredLabels.IsSubsetOf(entryServiceLabels))
            {
                var uniqueServiceOwners = entry.ServiceOwners
                    .Select(o => o.TrimStart('@').Trim())
                    .Where(o => !string.IsNullOrEmpty(o))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (uniqueServiceOwners.Count < MinimumOwnerCount)
                {
                    throw new InvalidOperationException(
                        $"check-package failed for path '{matchedEntry.PathExpression}': " +
                        $"Found service label entry matching PR labels [{string.Join(", ", matchedEntry.PRLabels)}] " +
                        $"but it has only {uniqueServiceOwners.Count} unique service owner(s) " +
                        $"(at least {MinimumOwnerCount} required). " +
                        $"Service owners: [{string.Join(", ", entry.ServiceOwners)}]");
                }

                return (entry.ServiceOwners, entry.ServiceLabels);
            }
        }

        throw new InvalidOperationException(
            $"check-package failed for path '{matchedEntry.PathExpression}': " +
            $"No service label entry found whose labels are a superset of PR labels " +
            $"[{string.Join(", ", matchedEntry.PRLabels)}].");
    }
}
