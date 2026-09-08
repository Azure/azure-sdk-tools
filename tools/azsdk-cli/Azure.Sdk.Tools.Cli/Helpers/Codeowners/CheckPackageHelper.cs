// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
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
    /// <exception cref="ArgumentException">
    /// <paramref name="directoryPath"/> does not name one concrete directory.
    /// </exception>
    Task<CheckPackageResponse> CheckPackage(
        string directoryPath,
        string repoRoot,
        string? repo,
        CancellationToken ct);
}

public class CheckPackageHelper(ICodeownersModelBuilder modelBuilder, IOwnerValidator ownerValidator) : ICheckPackageHelper
{
    public const string CurrentGitHubUserPlaceholder = "<github-aliases-to-add>";

    private const string PackageTargetType = "package";
    private const string PathTargetType = "path";
    private const string PrLabelPlaceholder = "<pr-label>";
    private const string ServiceAttentionLabel = "Service Attention";

    /// <summary>
    /// Resolves <paramref name="directoryPath"/> against the rendered CODEOWNERS model, minus the
    /// sections marked <c>exclude-from-check-package</c>.
    /// <para>
    /// Resolving through the render rather than through a single fragment means this answers the
    /// question GitHub will answer — who actually owns this path, under last-match-wins — instead of
    /// a parallel approximation of it. Excluding the guardrail sections is what makes that useful:
    /// the root, <c>/sdk/</c>, EngSys and management catch-alls own every package's path but say
    /// nothing about whether the package declared owners of its own.
    /// </para>
    /// <para>
    /// Owners are not re-validated here. <see cref="ICodeownersModelBuilder"/> has already dropped
    /// everyone the membership caches reject, so what remains is what GitHub would actually route to.
    /// </para>
    /// </summary>
    public async Task<CheckPackageResponse> CheckPackage(
        string directoryPath,
        string repoRoot,
        string? repo,
        CancellationToken ct)
    {
        // Before anything expensive. Rendering the repository to then reject the argument we were
        // handed reads as a finding about the repository rather than a mistake in the request.
        RequireOneConcreteDirectory(directoryPath);

        var model = await modelBuilder.Build(repoRoot, omitFallbackSections: true, ct);

        // The model already loaded the repository under its own discovery rules -- the configured
        // fragment file names and allowed-owner-yaml-paths -- so ask it which file governs the
        // directory rather than walking the checkout again and risking a different answer.
        var governingFragment = model.Repository.FindGoverningFragment(directoryPath)?.FilePath;

        var response = Evaluate(
            directoryPath,
            repo,
            [.. model.Entries.Select(entry => entry.Entry)],
            model.Settings,
            governingFragment);

        if (response.Issues.Count > 0 && governingFragment != null)
        {
            response.DroppedOwners =
            [
                .. model.Dropped.Where(item =>
                    item.Where.StartsWith(governingFragment + ":", StringComparison.OrdinalIgnoreCase))
            ];
        }

        return response;
    }

    /// <summary>
    /// Says why <paramref name="directoryPath"/> cannot be checked, or null when it can. Ownership is
    /// a property of a specific directory, so a pattern matching several has no single answer and a
    /// blank one has no subject at all.
    /// <para>
    /// Separate from <see cref="RequireOneConcreteDirectory"/> so a caller that can report the
    /// problem to a user does not have to reach for an exception to learn about it.
    /// </para>
    /// </summary>
    public static string? DescribeUnusableDirectory(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return "Directory path is required.";
        }

        if (directoryPath.Contains('*'))
        {
            return "Package directory paths must not contain '*'.";
        }

        return null;
    }

    /// <summary>
    /// Enforces <see cref="DescribeUnusableDirectory"/> for callers that have no way to report the
    /// problem, so an unusable path stops at the boundary instead of resolving to nothing further in.
    /// </summary>
    public static void RequireOneConcreteDirectory(string directoryPath)
    {
        var problem = DescribeUnusableDirectory(directoryPath);

        if (problem != null)
        {
            throw new ArgumentException($"{problem} Got '{directoryPath}'.", nameof(directoryPath));
        }
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
        RequireOneConcreteDirectory(directoryPath);

        var packageName = ResolvePackageName(directoryPath);

        // Named once: every "here is what to do about it" below points at the same file.
        var ownersFile = FormatOwnersFile(ownersFilePath, directoryPath);

        var response = new CheckPackageResponse
        {
            DirectoryPath = directoryPath,
            PackageName = packageName,
            Repo = repo,
        };

        if (!TryFindMatchingEntry(directoryPath, codeownersEntries, out var matchedEntry))
        {
            response.Issues.Add(new CheckPackageIssue
            {
                Code = CheckPackageIssue.Codes.NoMatchingPath,
                Message = $"check-package failed: No owners.yaml entry matches path '{directoryPath}'.",
                NextStep = $"Add a path entry for '{directoryPath}' with owners and pr-labels to {ownersFile} so package '{packageName}' is covered",
            });

            return response;
        }

        response.MatchedPathExpression = matchedEntry.PathExpression;
        var (resolvedTargetType, resolvedTarget) = ResolveMatchedTarget(directoryPath, matchedEntry.PathExpression);
        response.ResolvedTargetType = resolvedTargetType;
        response.ResolvedTarget = resolvedTarget;
        List<string> owners = [.. ownerValidator.ExpandToIndividuals(matchedEntry.SourceOwners ?? [])];

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
                NextStep = $"Add {CurrentGitHubUserPlaceholder} to the owners list of the '{resolvedTarget}' path entry in {ownersFile}",
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
                NextStep = $"Add a pr-labels entry to the '{resolvedTarget}' path entry in {ownersFile}",
            });

            return response;
        }

        var serviceOwnerLabels = GetServiceOwnerLabels(response.PRLabels);
        var (matchingServiceEntry, serviceOwners) = FindMatchingServiceEntry(matchedEntry, codeownersEntries);
        if (matchingServiceEntry == null)
        {
            response.ServiceLabels = [.. serviceOwnerLabels];
            response.Issues.Add(new CheckPackageIssue
            {
                Code = CheckPackageIssue.Codes.InsufficientServiceOwners,
                Message = BuildServiceOwnerIssueMessage(directoryPath, serviceOwnerLabels, 0, settings.MinimumLabelOwners, null),
                NextStep = BuildServiceOwnerNextStep(serviceOwnerLabels, ownersFile),
                FoundCount = 0,
                RequiredCount = settings.MinimumLabelOwners,
            });

            return response;
        }

        response.ServiceLabels = matchingServiceEntry.ServiceLabels ?? [];
        serviceOwnerLabels = GetServiceOwnerLabels(response.ServiceLabels);

        response.ServiceOwners = serviceOwners;

        if (serviceOwners.Count < settings.MinimumLabelOwners)
        {
            response.Issues.Add(new CheckPackageIssue
            {
                Code = CheckPackageIssue.Codes.InsufficientServiceOwners,
                Message = BuildServiceOwnerIssueMessage(directoryPath, serviceOwnerLabels, serviceOwners.Count, settings.MinimumLabelOwners, matchingServiceEntry.ServiceOwners),
                NextStep = BuildServiceOwnerNextStep(serviceOwnerLabels, ownersFile),
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
    /// Finds the CODEOWNERS entry that owns <paramref name="directoryPath"/> by scanning in reverse
    /// order, the way GitHub resolves the rendered file: last match wins. Entries without a path
    /// expression are service-label blocks and are skipped.
    /// <para>
    /// A directory pattern such as <c>/sdk/foo/</c> only matches paths *under* that directory, so a
    /// path authored without the trailing slash is tried both ways.
    /// </para>
    /// </summary>
    internal static bool TryFindMatchingEntry(
        string directoryPath,
        List<CodeownersEntry> entries,
        [NotNullWhen(true)] out CodeownersEntry? matchedEntry)
    {
        var pathsToTry = new List<string> { directoryPath };
        if (!directoryPath.EndsWith('/'))
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

            if (pathsToTry.Any(targetPath =>
                DirectoryUtils.PathExpressionMatchesTargetPath(entry.PathExpression, targetPath)))
            {
                matchedEntry = entry;
                return true;
            }
        }

        matchedEntry = null;
        return false;
    }

    /// <summary>
    /// Finds the last CODEOWNERS entry whose ServiceLabels, after removing Service Attention,
    /// are fully contained within the matched entry's PRLabels, and expands its service owners to
    /// the individuals GitHub would notify.
    /// </summary>
    internal (CodeownersEntry? matchingEntry, List<string> serviceOwners) FindMatchingServiceEntry(
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
                return (entry, [.. ownerValidator.ExpandToIndividuals(entry.ServiceOwners ?? [])]);
            }
        }

        return (null, []);
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
            $"{FormatPrLabels(labels)} has {foundCount} unique service owner(s); " +
            $"at least {requiredCount} are required.";

        var currentServiceOwners = serviceOwners?.ToList();
        if (currentServiceOwners?.Count > 0)
        {
            message += $" Service owners: [{string.Join(", ", currentServiceOwners)}]";
        }

        return message;
    }

    private static string BuildServiceOwnerNextStep(IReadOnlyList<string> labels, string ownersFile) =>
        $"Add {CurrentGitHubUserPlaceholder} to the service-owners list of the label-owners block "
        + $"for {FormatPrLabels(labels)} in {ownersFile}";

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

        // A repository may admit more than one spelling, so this could be derived from
        // configs.allowed-owner-yaml-paths in the future. Until one does, naming the file a new
        // fragment should be created under is a suggestion, and owners.yaml is the one to make.
        const string fragmentFileName = "owners.yaml";

        var segments = directoryPath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 2
            ? $"{segments[0]}/{segments[1]}/{fragmentFileName}"
            : $"the {fragmentFileName} for this service";
    }

    /// <summary>
    /// The labels that a label-owners block must cover. <c>Service Attention</c> is excluded: it is
    /// the routing label for the partner team, not a label anyone owns.
    /// </summary>
    private static IReadOnlyList<string> GetServiceOwnerLabels(IEnumerable<string>? labels) =>
    [
        .. (labels ?? [])
            .Where(label => !label.Equals(ServiceAttentionLabel, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
    ];

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

    /// <summary>
    /// Names the PR labels a service-owners block is keyed on. Labels contain spaces, so they are
    /// quoted; when the package declares none, the placeholder stands in for the one to add.
    /// </summary>
    private static string FormatPrLabels(IReadOnlyList<string> labels)
    {
        if (labels.Count == 0)
        {
            return $"PR label '{PrLabelPlaceholder}'";
        }

        var quoted = labels.Select(label => $"'{label}'");

        return labels.Count == 1
            ? $"PR label {quoted.First()}"
            : $"PR labels {string.Join(", ", quoted)}";
    }
}
