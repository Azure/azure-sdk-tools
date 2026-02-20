// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Azure.Sdk.Tools.Cli.Configuration;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Models.Codeowners;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.CodeownersUtils.Parsing;
using Azure.Sdk.Tools.CodeownersUtils.Utils;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;

namespace Azure.Sdk.Tools.Cli.Helpers;

/// <summary>
/// Generates CODEOWNERS files from Azure DevOps work items.
/// </summary>
public class CodeownersGenerateHelper(
    IDevOpsService devOpsService,
    IPowershellHelper powershellHelper,
    ILogger<CodeownersGenerateHelper> logger
) : ICodeownersGenerateHelper
{
    private readonly IDevOpsService devOpsService = devOpsService;
    private readonly IPowershellHelper powershellHelper = powershellHelper;
    private readonly ILogger<CodeownersGenerateHelper> logger = logger;

    public async Task GenerateCodeowners(
        string repoRoot,
        string repoName,
        string[] packageTypes,
        string sectionName,
        CancellationToken ct = default)
    {

        // Get language from full repo name (e.g. Azure/azure-sdk-for-net -> .NET)
        var language = SdkLanguageHelpers.GetLanguageForRepo(
            repoName.Split('/').Last()
        );

        logger.LogInformation("""
            Repository Root: {repoRoot}
            Repository: {repoName}
            Project: {Constants.AZURE_SDK_DEVOPS_RELEASE_PROJECT}
            Package Types: {packageTypes}
            Section: {sectionName}
            Language: {Language}
            """,
            repoRoot,
            repoName,
            Constants.AZURE_SDK_DEVOPS_RELEASE_PROJECT,
            string.Join(", ", packageTypes),
            sectionName,
            language
        );

        logger.LogInformation("Getting package paths from repository...");
        var repoPackages = await GetPackagesFromRepoAsync(repoRoot, ct);
        logger.LogInformation("Found {Count} packages in repository", repoPackages.Count);
        var packageLookup = repoPackages.ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

        logger.LogInformation("Fetching work items from Azure DevOps...");
        var workItemData = await FetchAllWorkItemsAsync(repoName, language, packageTypes, ct);

        logger.LogInformation("Building CODEOWNERS entries...");
        var entries = BuildCodeownersEntries(workItemData, packageLookup, repoRoot);
        logger.LogInformation("Total entries: {Count}", entries.Count);

        logger.LogInformation("Sorting entries...");
        CodeownersEntrySorter.SortOwnersInPlace(entries);
        CodeownersEntrySorter.SortLabelsInPlace(entries);
        entries = CodeownersEntrySorter.SortEntries(entries);

        logger.LogInformation("Updating CODEOWNERS file...");
        var codeownersPath = Path.Combine(repoRoot, ".github", "CODEOWNERS");
        await WriteCodeownersFile(codeownersPath, entries, sectionName);
    }

    private async Task<List<RepoPackage>> GetPackagesFromRepoAsync(string repoRoot, CancellationToken ct)
    {
        string tempFile = Path.GetTempFileName();
        string command = $". (Join-Path '{repoRoot}' 'eng/common/scripts/common.ps1'); $pkgProperties = Get-AllPkgProperties; $pkgProperties | ConvertTo-Json -Depth 100 | Set-Content -Path '{tempFile}'";

        try
        {
            var options = new PowershellOptions(
                [command],
                workingDirectory: repoRoot,
                logOutputStream: false,
                timeout: TimeSpan.FromMinutes(5)
            );

            var result = await powershellHelper.Run(options, ct);

            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException($"Getting package properties failed with exit code {result.ExitCode}: {result.Output}");
            }

            var packages = JsonSerializer.Deserialize<List<RepoPackage>>(
                await File.ReadAllTextAsync(tempFile, ct),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return packages;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get packages from repository");
            throw;
        }
        finally
        {
            try
            {
                if (File.Exists(tempFile)) {
                    File.Delete(tempFile);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to delete temporary file: {TempFile}", tempFile);
            }
        }
    }

    private async Task<WorkItemData> FetchAllWorkItemsAsync(string repoName, SdkLanguage language, string[] packageTypes, CancellationToken ct)
    {
        // Build package type filter using IN clause
        var packageTypeList = string.Join(", ", packageTypes.Select(pt => $"'{pt}'"));
        var packageQuery = $"[System.WorkItemType] = 'Package' AND [Custom.Language] = '{language.ToWorkItemString()}' AND [Custom.PackageType] IN ({packageTypeList})";

        // Fetch Packages by language and package type
        var allPackages = await FetchWorkItemsAsync<PackageWorkItem>(
            packageQuery,
            WorkItemMappers.MapToPackageWorkItem);
        logger.LogInformation("Packages (all versions): {Count}", allPackages.Count);

        // Group packages by name then take the package with the latest "Custom.PackageVersionMajorMinor"
        var packages = WorkItemMappers.GetLatestPackageVersions(allPackages);
        logger.LogInformation("Packages (latest versions): {Count}", packages.Count);

        // Fetch all Owners (no filter needed)
        var owners = await FetchWorkItemsAsync<OwnerWorkItem>(
            "[System.WorkItemType] = 'Owner'",
            WorkItemMappers.MapToOwnerWorkItem);
        logger.LogInformation("Owners: {Count}", owners.Count);

        // Fetch all Labels (no filter needed)
        var labels = await FetchWorkItemsAsync<LabelWorkItem>(
            "[System.WorkItemType] = 'Label'",
            WorkItemMappers.MapToLabelWorkItem);
        logger.LogInformation("Labels: {Count}", labels.Count);

        // Fetch Label Owners by repository
        var labelOwners = await FetchWorkItemsAsync<LabelOwnerWorkItem>(
            $"[System.WorkItemType] = 'Label Owner' AND [Custom.Repository] = '{repoName}'",
            WorkItemMappers.MapToLabelOwnerWorkItem);
        logger.LogInformation("Label Owners: {Count}", labelOwners.Count);

        var data = new WorkItemData(
            packages.ToDictionary(p => p.WorkItemId),
            owners.ToDictionary(o => o.WorkItemId),
            labels.ToDictionary(l => l.WorkItemId),
            labelOwners);

        // Hydrate relationships to populate direct references between work items
        data.HydrateRelationships();

        return data;
    }

    private async Task<List<T>> FetchWorkItemsAsync<T>(
        string whereClause,
        Func<WorkItem, T> factory)
    {
        var query = $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = '{Constants.AZURE_SDK_DEVOPS_RELEASE_PROJECT}' AND {whereClause}";
        var workItems = await devOpsService.FetchWorkItemsPagedAsync(query, expand: WorkItemExpand.Relations);
        return workItems.Select(factory).ToList();
    }

    private List<CodeownersEntry> BuildCodeownersEntries(
        WorkItemData data,
        Dictionary<string, RepoPackage> packageLookup,
        string repoRoot)
    {
        var entries = new List<CodeownersEntry>();
        var packagesNotFound = new List<string>();

        // Build entries for packages
        foreach (var pkg in data.Packages.Values)
        {
            if (!packageLookup.TryGetValue(pkg.PackageName, out var repoPkg))
            {
                packagesNotFound.Add(pkg.PackageName);
                continue;
            }

            var entry = new CodeownersEntry
            {
                PathExpression = BuildPathExpression(repoPkg.DirectoryPath, repoRoot),
                SourceOwners = pkg.Owners
                    .Select(o => o.GitHubAlias)
                    .Where(a => !string.IsNullOrWhiteSpace(a))
                    .ToList(),
                PRLabels = pkg.Labels
                    .Select(l => l.LabelName)
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .ToList()
            };
            entry.OriginalSourceOwners = [.. entry.SourceOwners];

            // Add metadata from related label owners
            foreach (var lo in pkg.LabelOwners)
            {
                AddLabelOwnerMetadata(entry, lo);
            }

            entries.Add(entry);
        }

        // Label Owners already linked to packages
        var packageRelatedLabelOwnerIds = data.Packages.Values
            .SelectMany(p => p.LabelOwners)
            .Select(lo => lo.WorkItemId)
            .ToHashSet();

        // Get Label Owners not linked to packages
        var unlinkedLabelOwners = data.LabelOwners
            .Where(lo => !packageRelatedLabelOwnerIds.Contains(lo.WorkItemId))
            .ToList();

        // Service-level path entries: Label Owners with RepoPath
        var serviceLevelPathEntries = new Dictionary<string, CodeownersEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var lo in unlinkedLabelOwners.Where(lo => !string.IsNullOrEmpty(lo.RepoPath)))
        {
            string pathExpression = "/" + lo.RepoPath.TrimStart('/').TrimEnd('/') + "/";

            var sourceOwners = lo.Owners
                .Select(o => o.GitHubAlias)
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .ToList();
            if (sourceOwners.Count == 0)
            {
                continue;
            }

            var prLabels = lo.Labels
                .Select(l => l.LabelName)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();

            // Group entries with the same path expression together
            if (!serviceLevelPathEntries.TryGetValue(pathExpression, out var entry))
            {
                entry = new CodeownersEntry
                {
                    PathExpression = pathExpression,
                    SourceOwners = [],
                    OriginalSourceOwners = [],
                    PRLabels = []
                };
                serviceLevelPathEntries[pathExpression] = entry;
            }

            AddUniqueOwners(entry.SourceOwners, sourceOwners);
            AddUniqueOwners(entry.OriginalSourceOwners, sourceOwners);
            foreach (var label in prLabels)
            {
                if (!entry.PRLabels.Contains(label, StringComparer.OrdinalIgnoreCase))
                {
                    entry.PRLabels.Add(label);
                }
            }
        }
        entries.AddRange(serviceLevelPathEntries.Values);

        // Pathless entries: Label Owners without RepoPath (Service Owner / Azure SDK Owner types for triage)
        var pathlessEntriesByLabel = new Dictionary<string, CodeownersEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var lo in unlinkedLabelOwners.Where(lo => string.IsNullOrEmpty(lo.RepoPath)))
        {
            var labels = lo.Labels
                .Select(l => l.LabelName)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();

            if (labels.Count == 0)
            {
                continue;
            }

            // Group pathless entries by their set of labels to avoid duplicates
            string key = string.Join("|", labels.OrderBy(l => l, StringComparer.OrdinalIgnoreCase));
            if (!pathlessEntriesByLabel.TryGetValue(key, out var entry))
            {
                entry = new CodeownersEntry { PathExpression = "", ServiceLabels = labels };
                pathlessEntriesByLabel[key] = entry;
            }

            AddLabelOwnerMetadata(entry, lo);
        }
        entries.AddRange(pathlessEntriesByLabel.Values);

        if (packagesNotFound.Count > 0)
        {
            logger.LogWarning("The following packages have work items but were not found in the repository and skipped: {Packages}", string.Join(", ", packagesNotFound));
        }

        return entries;
    }

    private static void AddLabelOwnerMetadata(CodeownersEntry entry, LabelOwnerWorkItem lo)
    {
        var labels = lo.Labels
            .Select(l => l.LabelName)
            .Where(l => !string.IsNullOrWhiteSpace(l));
        var owners = lo.Owners
            .Select(o => o.GitHubAlias)
            .Where(a => !string.IsNullOrWhiteSpace(a));

        foreach (var label in labels)
        {
            if (!entry.ServiceLabels.Contains(label, StringComparer.OrdinalIgnoreCase))
            {
                entry.ServiceLabels.Add(label);
            }
        }

        if (lo.LabelType.Equals("Service Owner", StringComparison.OrdinalIgnoreCase))
        {
            AddUniqueOwners(entry.ServiceOwners, owners);
            AddUniqueOwners(entry.OriginalServiceOwners, owners);
        }
        else if (lo.LabelType.Equals("Azure SDK Owner", StringComparison.OrdinalIgnoreCase))
        {
            AddUniqueOwners(entry.AzureSdkOwners, owners);
            AddUniqueOwners(entry.OriginalAzureSdkOwners, owners);
        }
    }

    private static void AddUniqueOwners(List<string> target, IEnumerable<string> owners)
    {
        foreach (var owner in owners)
        {
            if (!target.Contains(owner, StringComparer.OrdinalIgnoreCase))
            {
                target.Add(owner);
            }
        }
    }

    /// <summary>
    /// Builds a CODEOWNERS path expression for a given directory path
    /// </summary>
    /// <param name="dirPath">The directory path to convert (absolute or relative)</param>
    /// <param name="repoRoot">The repository root path (used to calculate relative paths)</param>
    /// <returns>A CODEOWNERS path expression based on the repo root</returns>
    public static string BuildPathExpression(string dirPath, string repoRoot)
    {
        string normalized = dirPath.Replace('\\', '/').TrimEnd('/');
        string normalizedRoot = repoRoot.Replace('\\', '/').TrimEnd('/');

        if (normalized.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            string relative = normalized[normalizedRoot.Length..];
            if (!relative.StartsWith('/'))
            {
                relative = "/" + relative;
            }
            return relative + "/";
        }

        return "/" + normalized.TrimStart('/') + "/";
    }

    private async Task WriteCodeownersFile(string path, List<CodeownersEntry> entries, string sectionName)
    {
        if (!File.Exists(path))
        {
            logger.LogError("CODEOWNERS file not found: {Path}", path);
            throw new FileNotFoundException($"CODEOWNERS file not found: {path}");
        }

        var lines = (await File.ReadAllLinesAsync(path)).ToList();
        var (headerStart, contentStart, sectionEnd) = CodeownersSectionFinder.FindSection(lines, sectionName);

        if (contentStart == -1)
        {
            logger.LogError("'{SectionName}' section not found in CODEOWNERS file", sectionName);
            throw new InvalidOperationException($"'{sectionName}' section not found in CODEOWNERS file");
        }

        var output = new List<string>();

        // Content before section
        output.AddRange(lines[0..headerStart]);

        // Section header
        output.Add("####################");
        output.Add($"# {sectionName}");
        output.Add("####################");
        output.Add("");

        // Insert entries (with blank line between each)
        foreach (var entry in entries)
        {
            string formatted = entry.FormatCodeownersEntry();
            if (!string.IsNullOrWhiteSpace(formatted))
            {
                output.Add(formatted);
                output.Add("");
            }
        }

        // Content after section
        output.AddRange(lines[sectionEnd..]);
        // Newline at end of file
        output.Add("");

        await File.WriteAllTextAsync(path, string.Join("\n", output));
        logger.LogInformation("Updated: {Path}", path);
    }
}
