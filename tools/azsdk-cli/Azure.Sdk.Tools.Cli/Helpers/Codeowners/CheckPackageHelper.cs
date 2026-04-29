// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models.Responses.Codeowners;
using Azure.Sdk.Tools.CodeownersUtils.Parsing;
using Azure.Sdk.Tools.CodeownersUtils.Utils;

namespace Azure.Sdk.Tools.Cli.Helpers.Codeowners;

public interface ICheckPackageHelper
{
    /// <summary>
    /// Validates that a path has sufficient owners, PR labels, and service owners
    /// based on parsed CODEOWNERS entries. Throws on validation failure.
    /// </summary>
    /// <param name="directoryPath">Relative path from repo root to the package directory.</param>
    /// <param name="codeownersEntries">
    /// Pre-parsed CODEOWNERS entries from <see cref="CodeownersParser"/>. This helper relies on parser behavior
    /// such as metadata block parsing and attempted team expansion when evaluating owners and labels.
    /// </param>
    /// <returns>A <see cref="CheckPackageResponse"/> on success.</returns>
    /// <exception cref="InvalidOperationException">Thrown when validation fails.</exception>
    CheckPackageResponse CheckPackage(
        string directoryPath,
        List<CodeownersEntry> codeownersEntries);
}

public class CheckPackageHelper : ICheckPackageHelper
{
    private const int MinimumOwnerCount = 2;
    private const string ServiceAttentionLabel = "Service Attention";

    /// <summary>
    /// Validates a package path against CODEOWNERS entries produced by <see cref="CodeownersParser"/>.
    /// </summary>
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
        var owners = GetUniqueIndividualOwners(matchedEntry.SourceOwners);
        if (owners.Count < MinimumOwnerCount)
        {
            throw new InvalidOperationException(
                $"check-package failed for path '{directoryPath}': " +
                $"Found {owners.Count} unique owner(s) but at least {MinimumOwnerCount} are required. " +
                $"Owners: [{string.Join(", ", matchedEntry.SourceOwners)}]");
        }
        if (matchedEntry.PRLabels == null || matchedEntry.PRLabels.Count == 0)
        {
            throw new InvalidOperationException(
                $"check-package failed for path '{directoryPath}': " +
                $"No PR labels found on the matching CODEOWNERS entry (path expression: '{matchedEntry.PathExpression}').");
        }
        var (serviceOwners, serviceLabels) = ThrowIfInsufficientServiceOwners(matchedEntry, codeownersEntries);

        return new CheckPackageResponse
        {
            DirectoryPath = directoryPath,
            Owners = owners,
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
    /// Finds the last CODEOWNERS entry whose ServiceLabels, after removing Service Attention,
    /// are fully contained within the matched entry's PRLabels, and validates that it has at least
    /// <see cref="MinimumOwnerCount"/> unique service owners.
    /// </summary>
    internal static (List<string> serviceOwners, List<string> serviceLabels) ThrowIfInsufficientServiceOwners(
        CodeownersEntry matchedEntry,
        List<CodeownersEntry> allEntries)
    {
        var requiredLabels = new HashSet<string>(matchedEntry.PRLabels, StringComparer.OrdinalIgnoreCase);

        for (int i = allEntries.Count - 1; i >= 0; i--)
        {
            var entry = allEntries[i];
            if (entry.ServiceLabels == null || entry.ServiceLabels.Count == 0)
            {
                continue;
            }

            // Service Attention can still appear on service-owner entries, but it is not part of
            // the package's required ownership identity. Ignore it here to match GitHubEventProcessor.
            var entryServiceLabels = entry.ServiceLabels
                .Where(label => !label.Equals(ServiceAttentionLabel, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (requiredLabels.Intersect(entryServiceLabels, StringComparer.OrdinalIgnoreCase).Count() == entryServiceLabels.Count)
            {
                var uniqueServiceOwners = GetUniqueIndividualOwners(entry.ServiceOwners);

                if (uniqueServiceOwners.Count < MinimumOwnerCount)
                {
                    throw new InvalidOperationException(
                        $"check-package failed for path '{matchedEntry.PathExpression}': " +
                        $"Found service label entry matching PR labels [{string.Join(", ", matchedEntry.PRLabels)}] " +
                        $"but it has only {uniqueServiceOwners.Count} unique service owner(s) " +
                        $"(at least {MinimumOwnerCount} required). " +
                        $"Service owners: [{string.Join(", ", entry.ServiceOwners)}]");
                }

                return (uniqueServiceOwners, entry.ServiceLabels);
            }
        }

        throw new InvalidOperationException(
            $"check-package failed for path '{matchedEntry.PathExpression}': " +
            $"No service label entry found whose labels are fully contained within PR labels " +
            $"[{string.Join(", ", matchedEntry.PRLabels)}].");
    }

    /// <summary>
    /// Returns a distinct list of individual owner aliases.
    /// GitHub team aliases are intentionally omitted because the CODEOWNERS parser can leave
    /// unresolved teams in the owner list when team expansion fails against the cache.
    /// </summary>
    internal static List<string> GetUniqueIndividualOwners(IEnumerable<string>? owners)
    {
        return (owners ?? [])
            .Select(o => o.TrimStart('@').Trim())
            .Where(o => !string.IsNullOrEmpty(o) && !ParsingUtils.IsGitHubTeam(o))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
