// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models.Codeowners;
using Azure.Sdk.Tools.Cli.Models.Responses.Codeowners;
using Azure.Sdk.Tools.CodeownersUtils.Parsing;
using Azure.Sdk.Tools.CodeownersUtils.Utils;

namespace Azure.Sdk.Tools.Cli.Helpers.Codeowners;

public interface ICheckPackageHelper
{
    /// <summary>
    /// Validates that a package directory has sufficient owners, PR labels, and service owners.
    /// Ownership is resolved from the <c>owners.yaml</c> fragment that governs
    /// <paramref name="directoryPath"/>, found by walking up from that directory.
    /// </summary>
    /// <param name="directoryPath">Relative path from repo root to the package directory.</param>
    /// <param name="repoRoot">Absolute path to the repository root.</param>
    /// <param name="repo">Repository name, used for repo-label validation and prompt generation.</param>
    /// <returns>A <see cref="CheckPackageResponse"/> describing success or all discovered issues.</returns>
    Task<CheckPackageResponse> CheckPackage(
        string directoryPath,
        string repoRoot,
        string? repo,
        CancellationToken ct);
}

public class CheckPackageHelper(IOwnerValidator ownerValidator) : ICheckPackageHelper
{
    public const string CurrentGitHubUserPlaceholder = "<github-aliases-to-add>";

    private const string PackageTargetType = "package";
    private const string PathTargetType = "path";
    private const string PrLabelPlaceholder = "<pr-label>";
    private const string ServiceAttentionLabel = "Service Attention";

    private static readonly string[] FragmentFileNames = ["owners.yaml", "owners.yml"];

    /// <summary>
    /// Renders the repository's ownership YAML and resolves <paramref name="directoryPath"/> against
    /// the result, minus the sections marked <c>exclude-from-check-package</c>.
    /// <para>
    /// Resolving through the render rather than through a single fragment means this answers the
    /// question GitHub will answer — who actually owns this path, under last-match-wins — instead of
    /// a parallel approximation of it. Excluding the guardrail sections is what makes that useful:
    /// the root, <c>/sdk/</c>, EngSys and management catch-alls own every package's path but say
    /// nothing about whether the package declared owners of its own.
    /// </para>
    /// </summary>
    public async Task<CheckPackageResponse> CheckPackage(
        string directoryPath,
        string repoRoot,
        string? repo,
        CancellationToken ct)
    {
        await ownerValidator.EnsureUsableAsync(ct);

        var repository = OwnersRepositoryLoader.Load(repoRoot, []);
        var rendered = CodeownersRenderer.Render(repository);

        var excludedSections = repository.Config.Sections
            .Where(section => section.ExcludeFromCheckPackage)
            .Select(section => section.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var entries = rendered.Entries
            .Where(entry => !excludedSections.Contains(entry.SectionName))
            .Select(entry => entry.Entry)
            .ToList();

        return Evaluate(
            directoryPath,
            repo,
            entries,
            repository.Config.Configs,
            FindGoverningFragmentPath(directoryPath, repoRoot));
    }

    /// <summary>
    /// Repo-relative path of the <c>owners.yaml</c> that governs <paramref name="directoryPath"/>,
    /// or null when there is none. This answers "which file should I edit", which is a different
    /// question from "who owns this path" — ownership can be resolved by an entry declared in a
    /// fragment further up the tree, but a fix still belongs in the nearest governing file.
    /// </summary>
    private static string? FindGoverningFragmentPath(string directoryPath, string repoRoot)
    {
        var current = directoryPath.Trim('/');

        while (!string.IsNullOrEmpty(current))
        {
            foreach (var fileName in FragmentFileNames)
            {
                var relativePath = $"{current}/{fileName}";
                var file = Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));

                if (File.Exists(file))
                {
                    return relativePath;
                }
            }

            var separator = current.LastIndexOf('/');
            current = separator < 0 ? string.Empty : current[..separator];
        }

        return null;
    }

    /// <summary>
    /// Applies the four package-ownership rules to a set of already-resolved entries. Kept separate
    /// from resolution so the rules stay directly testable.
    /// </summary>
    /// <param name="ownersFilePath">
    /// Repo-relative path of the fragment a fix belongs in. Used to tell the caller which file to
    /// edit; null when no fragment governs the directory.
    /// </param>
    public CheckPackageResponse Evaluate(
        string directoryPath,
        string? repo,
        List<CodeownersEntry> codeownersEntries,
        OwnersConfigSettings settings,
        string? ownersFilePath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new ArgumentException("Directory path is required.", nameof(directoryPath));
        }

        var packageName = ResolvePackageName(directoryPath);

        var response = new CheckPackageResponse
        {
            DirectoryPath = directoryPath,
            PackageName = packageName,
            Repo = repo,
        };

        if (directoryPath.Contains('*'))
        {
            response.Issues.Add(new CheckPackageIssue
            {
                Code = CheckPackageIssue.Codes.InvalidDirectoryPath,
                Message = $"check-package failed for path '{directoryPath}': Package directory paths must not contain '*'.",
                NextStep = $"Rerun the ownership check with a concrete package directory path instead of {FormatPromptValue(directoryPath)}",
                CurrentValues = [directoryPath],
            });

            return response;
        }

        var matchedEntry = TryFindMatchingEntry(directoryPath, codeownersEntries);
        if (matchedEntry == null)
        {
            response.Issues.Add(new CheckPackageIssue
            {
                Code = CheckPackageIssue.Codes.NoMatchingPath,
                Message = $"check-package failed: No owners.yaml entry matches path '{directoryPath}'.",
                NextStep = $"Add a path entry for {FormatPromptValue(directoryPath)} with owners and pr-labels to {FormatOwnersFile(ownersFilePath, directoryPath)} so package {FormatPromptValue(packageName)} is covered",
            });

            return response;
        }

        response.MatchedPathExpression = matchedEntry.PathExpression;
        var (resolvedTargetType, resolvedTarget) = ResolveMatchedTarget(directoryPath, matchedEntry.PathExpression);
        response.ResolvedTargetType = resolvedTargetType;
        response.ResolvedTarget = resolvedTarget;
        var owners = RejectInvalidOwners(
            GetUniqueIndividualOwners(matchedEntry.SourceOwners),
            $"the {FormatPromptValue(resolvedTarget)} path entry",
            ownersFilePath,
            directoryPath,
            response.Issues);

        response.Owners = owners;
        response.PRLabels = matchedEntry.PRLabels ?? [];

        if (owners.Count < settings.MinimumPathOwners)
        {
            response.Issues.Add(new CheckPackageIssue
            {
                Code = CheckPackageIssue.Codes.InsufficientOwners,
                Message =
                    $"check-package failed for path '{directoryPath}': " +
                    $"{BuildResolvedTargetDescription(resolvedTargetType, resolvedTarget)} has {owners.Count} unique owner(s); " +
                    $"at least {settings.MinimumPathOwners} are required. " +
                    $"Owners: [{string.Join(", ", matchedEntry.SourceOwners ?? [])}]",
                NextStep = $"Add {CurrentGitHubUserPlaceholder} to the owners list of the {FormatPromptValue(resolvedTarget)} path entry in {FormatOwnersFile(ownersFilePath, directoryPath)}",
                FoundCount = owners.Count,
                RequiredCount = settings.MinimumPathOwners,
                CurrentValues = matchedEntry.SourceOwners != null
                    ? new List<string>(matchedEntry.SourceOwners)
                    : null,
            });
        }

        if (response.PRLabels.Count == 0)
        {
            response.Issues.Add(new CheckPackageIssue
            {
                Code = CheckPackageIssue.Codes.MissingPrLabel,
                Message =
                    $"check-package failed for path '{directoryPath}': " +
                    $"{BuildResolvedTargetDescription(resolvedTargetType, resolvedTarget)} has no PR label.",
                NextStep = $"Add a pr-labels entry to the {FormatPromptValue(resolvedTarget)} path entry in {FormatOwnersFile(ownersFilePath, directoryPath)}",
            });

            return response;
        }

        var serviceOwnerLabels = GetServiceOwnerPromptLabels(response.PRLabels);
        var (matchingServiceEntry, serviceOwners) = FindMatchingServiceEntry(matchedEntry, codeownersEntries);
        if (matchingServiceEntry == null)
        {
            response.ServiceLabels = [.. serviceOwnerLabels];
            response.Issues.Add(new CheckPackageIssue
            {
                Code = CheckPackageIssue.Codes.InsufficientServiceOwners,
                Message = BuildServiceOwnerIssueMessage(directoryPath, serviceOwnerLabels, 0, settings.MinimumLabelOwners, null),
                NextStep = BuildServiceOwnerNextStep(serviceOwnerLabels, ownersFilePath, directoryPath),
                FoundCount = 0,
                RequiredCount = settings.MinimumLabelOwners,
            });

            return response;
        }

        response.ServiceLabels = matchingServiceEntry.ServiceLabels ?? [];
        serviceOwnerLabels = GetServiceOwnerPromptLabels(response.ServiceLabels);

        serviceOwners = RejectInvalidOwners(
            serviceOwners,
            $"the {FormatPromptValue(string.Join(", ", serviceOwnerLabels))} label owners entry",
            ownersFilePath,
            directoryPath,
            response.Issues);

        response.ServiceOwners = serviceOwners;

        if (serviceOwners.Count < settings.MinimumLabelOwners)
        {
            response.Issues.Add(new CheckPackageIssue
            {
                Code = CheckPackageIssue.Codes.InsufficientServiceOwners,
                Message = BuildServiceOwnerIssueMessage(directoryPath, serviceOwnerLabels, serviceOwners.Count, settings.MinimumLabelOwners, matchingServiceEntry.ServiceOwners),
                NextStep = BuildServiceOwnerNextStep(serviceOwnerLabels, ownersFilePath, directoryPath),
                FoundCount = serviceOwners.Count,
                RequiredCount = settings.MinimumLabelOwners,
                CurrentValues = matchingServiceEntry.ServiceOwners != null
                    ? new List<string>(matchingServiceEntry.ServiceOwners)
                    : null,
            });
        }

        return response;
    }

    /// <summary>
    /// Finds the matching CODEOWNERS entry by scanning in reverse order (last match wins).
    /// Only considers entries that have a path expression (skips service-label-only entries).
    /// Tries the path as-is first, then with a trailing slash appended to handle
    /// directory-style CODEOWNERS patterns (e.g., /sdk/foo/ matching sdk/foo).
    /// </summary>
    internal static CodeownersEntry? TryFindMatchingEntry(string directoryPath, List<CodeownersEntry> entries)
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

        return null;
    }

    /// <summary>
    /// Finds the last CODEOWNERS entry whose ServiceLabels, after removing Service Attention,
    /// are fully contained within the matched entry's PRLabels, and collects its unique service
    /// owners for the caller to measure against the configured minimum.
    /// </summary>
    internal static (CodeownersEntry? matchingEntry, List<string> serviceOwners) FindMatchingServiceEntry(
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
                return (entry, uniqueServiceOwners);
            }
        }

        return (null, []);
    }

    /// <summary>
    /// Drops owners who are not code owners and records why, returning only those left.
    /// <para>
    /// Invalid owners are removed before counting rather than reported alongside the count, because
    /// an owner GitHub will not route a review to does not own the package in any sense that matters.
    /// Counting them would let a package with one real owner and one departed one look adequately
    /// owned right up until the review request goes nowhere.
    /// </para>
    /// </summary>
    private List<string> RejectInvalidOwners(
        List<string> owners,
        string location,
        string? ownersFilePath,
        string directoryPath,
        List<CheckPackageIssue> issues)
    {
        var valid = new List<string>();

        foreach (var owner in owners)
        {
            var violation = ownerValidator.Validate(owner, where: null);
            if (violation == null)
            {
                valid.Add(owner);
                continue;
            }

            issues.Add(new CheckPackageIssue
            {
                Code = CheckPackageIssue.Codes.InvalidOwner,
                Message =
                    $"check-package failed for path '{directoryPath}': {violation.Description} " +
                    violation.Detail,
                NextStep =
                    $"Remove {FormatPromptValue(owner)} from {location} in " +
                    $"{FormatOwnersFile(ownersFilePath, directoryPath)}, or have them meet the requirements above",
                CurrentValues = [owner],
            });
        }

        return valid;
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

    internal static string ResolvePackageName(string directoryPath)
    {
        var trimmedPath = directoryPath.TrimEnd('/');
        if (string.IsNullOrEmpty(trimmedPath))
        {
            return "<package-name>";
        }

        var lastSegment = trimmedPath.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        return string.IsNullOrEmpty(lastSegment) ? "<package-name>" : lastSegment;
    }

    private static string BuildServiceOwnerIssueMessage(
        string directoryPath,
        IReadOnlyList<string> labels,
        int foundCount,
        int requiredCount,
        IEnumerable<string>? serviceOwners)
    {
        var message =
            $"check-package failed for path '{directoryPath}': " +
            $"{FormatPrLabelTargetForMessage(labels)} {GetPluralVerb(labels)} {foundCount} unique service owner(s); " +
            $"at least {requiredCount} are required.";

        var currentServiceOwners = serviceOwners?.ToList();
        if (currentServiceOwners?.Count > 0)
        {
            message += $" Service owners: [{string.Join(", ", currentServiceOwners)}]";
        }

        return message;
    }

    private static string BuildServiceOwnerNextStep(IReadOnlyList<string> labels, string? ownersFilePath, string directoryPath)
    {
        return $"Add {CurrentGitHubUserPlaceholder} to the service-owners list of the {FormatPrLabelTargetForPrompt(labels)} "
            + $"label-owners block in {FormatOwnersFile(ownersFilePath, directoryPath)}";
    }

    /// <summary>
    /// Names the fragment the caller should edit. When no fragment governs the directory, suggests
    /// where one should be created: the service directory two segments below the repo root.
    /// </summary>
    private static string FormatOwnersFile(string? ownersFilePath, string directoryPath)
    {
        if (!string.IsNullOrEmpty(ownersFilePath))
        {
            return ownersFilePath;
        }

        var segments = directoryPath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 2
            ? $"{segments[0]}/{segments[1]}/owners.yaml"
            : "the owners.yaml for this service";
    }

    private static IReadOnlyList<string> GetServiceOwnerPromptLabels(IEnumerable<string>? labels)
    {
        return (labels ?? [])
            .Where(label => !label.Equals(ServiceAttentionLabel, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static (string resolvedTargetType, string resolvedTarget) ResolveMatchedTarget(
        string directoryPath,
        string matchedPathExpression)
    {
        var normalizedRequestedPath = NormalizeResolvedTarget(directoryPath);
        var normalizedMatchedPath = NormalizeResolvedTarget(matchedPathExpression);
        var resolvedTargetType = string.Equals(normalizedRequestedPath, normalizedMatchedPath, StringComparison.OrdinalIgnoreCase)
            ? PackageTargetType
            : PathTargetType;

        return (resolvedTargetType, normalizedMatchedPath);
    }

    private static string NormalizeResolvedTarget(string path)
    {
        var trimmedPath = path.Trim();
        if (!trimmedPath.StartsWith('/'))
        {
            trimmedPath = "/" + trimmedPath.TrimStart('/');
        }

        trimmedPath = trimmedPath.TrimEnd('/');
        return string.IsNullOrEmpty(trimmedPath) ? "/" : trimmedPath;
    }

    private static string BuildResolvedTargetDescription(string resolvedTargetType, string resolvedTarget)
    {
        return resolvedTargetType == PackageTargetType
            ? $"resolved package entry '{resolvedTarget}'"
            : $"resolved service-level path entry '{resolvedTarget}'";
    }

    private static string FormatPrLabelTargetForMessage(IReadOnlyList<string> labels)
    {
        if (labels.Count == 0)
        {
            return $"PR label {FormatQuotedValue(PrLabelPlaceholder)}";
        }

        return labels.Count == 1
            ? $"PR label {FormatQuotedValue(labels[0])}"
            : $"PR labels [{string.Join(", ", labels.Select(FormatQuotedValue))}]";
    }

    private static string FormatPrLabelTargetForPrompt(IReadOnlyList<string> labels)
    {
        if (labels.Count == 0)
        {
            return $"label {FormatQuotedValue(PrLabelPlaceholder)}";
        }

        return labels.Count == 1
            ? $"label {FormatQuotedValue(labels[0])}"
            : $"labels {string.Join(", ", labels.Select(FormatQuotedValue))}";
    }

    private static string GetPluralVerb(IReadOnlyList<string> labels) => labels.Count <= 1 ? "has" : "have";

    private static string FormatPromptValue(string value)
    {
        return value.Contains(' ', StringComparison.Ordinal)
            ? $"\"{value}\""
            : value;
    }

    private static string FormatQuotedValue(string value) => $"\"{value}\"";
}
