// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Models.Codeowners;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.CodeownersUtils.Parsing;
using Azure.Sdk.Tools.CodeownersUtils.Utils;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;

namespace Azure.Sdk.Tools.Cli.Helpers;

/// <summary>
/// Renders CODEOWNERS files from Azure DevOps work items.
/// </summary>
public class CodeownersRenderHelper : ICodeownersRenderHelper
{
    private readonly IDevOpsService _devOpsService;
    private readonly IPowershellHelper _powershellHelper;
    private readonly IInputSanitizer _inputSanitizer;
    private readonly ILogger<CodeownersRenderHelper> _logger;
    private readonly List<string> _errors = [];

    public CodeownersRenderHelper(
        IDevOpsService devOpsService,
        IPowershellHelper powershellHelper,
        IInputSanitizer inputSanitizer,
        ILogger<CodeownersRenderHelper> logger)
    {
        _devOpsService = devOpsService;
        _powershellHelper = powershellHelper;
        _inputSanitizer = inputSanitizer;
        _logger = logger;
    }

    public async Task<string> RenderCodeownersAsync(
        string repoRoot,
        string repoName,
        string orgName = "azure-sdk",
        string projectName = "Release",
        List<string>? packageTypes = null,
        string sectionName = "Client Libraries",
        CancellationToken ct = default)
    {
        _errors.Clear();
        packageTypes ??= ["client"];
        _logger.LogInformation("=== RenderCodeownersFile ===");
        _logger.LogInformation("Repository Root: {RepoRoot}", repoRoot);
        _logger.LogInformation("Repository: {RepoName}", repoName);
        _logger.LogInformation("Organization: {OrgName}", orgName);
        _logger.LogInformation("Project: {ProjectName}", projectName);
        _logger.LogInformation("Package Types: {PackageTypes}", string.Join(", ", packageTypes));
        _logger.LogInformation("Section: {SectionName}", sectionName);

        // Get language from repo name
        var language = GetLanguageFromRepoName(repoName);
        _logger.LogInformation("Language: {Language}", language);

        // Step 1: Get packages from repository
        _logger.LogInformation("Step 1: Getting package paths from repository...");
        var repoPackages = await GetPackagesFromRepoAsync(repoRoot, ct);
        _logger.LogInformation("Found {Count} packages in repository", repoPackages.Count);
        var packageLookup = repoPackages.ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

        // Step 2: Fetch work items from Azure DevOps
        _logger.LogInformation("Step 2: Fetching work items from Azure DevOps...");
        var workItemData = await FetchAllWorkItemsAsync(projectName, repoName, language, packageTypes, ct);

        // Step 3: Build CODEOWNERS entries
        _logger.LogInformation("Step 3: Building CODEOWNERS entries...");
        var entries = BuildCodeownersEntries(workItemData, packageLookup, repoRoot);
        _logger.LogInformation("Total entries: {Count}", entries.Count);

        // Step 4: Sort entries
        _logger.LogInformation("Step 4: Sorting entries...");
        CodeownersEntrySorter.SortOwnersInPlace(entries);
        CodeownersEntrySorter.SortLabelsInPlace(entries);
        entries = CodeownersEntrySorter.SortEntries(entries);

        // Step 5: Update CODEOWNERS file
        _logger.LogInformation("Step 5: Updating CODEOWNERS file...");
        var codeownersPath = Path.Combine(repoRoot, ".github", "CODEOWNERS");
        var output = WriteCodeownersFile(codeownersPath, entries, sectionName);

        // Print summary
        PrintSummary(entries);

        return output;
    }

    private string GetLanguageFromRepoName(string repoName)
    {
        if (!repoName.Contains("azure-sdk-for-", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Repository name must be in the format 'Azure/azure-sdk-for-{language}'", nameof(repoName));
        }

        string suffix = repoName[(repoName.LastIndexOf('-') + 1)..].ToLowerInvariant();
        return _inputSanitizer.SanitizeLanguage(suffix);
    }

    private async Task<List<RepoPackage>> GetPackagesFromRepoAsync(string repoRoot, CancellationToken ct)
    {
        // Find the script - check the assembly directory and current directory
        string script = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts", "Get-AllPkgProperties.ps1");
        if (!File.Exists(script))
        {
            script = Path.Combine(Directory.GetCurrentDirectory(), "Scripts", "Get-AllPkgProperties.ps1");
        }

        if (!File.Exists(script))
        {
            _logger.LogWarning("Get-AllPkgProperties.ps1 not found at {Path}", script);
            return [];
        }

        string tempFile = Path.GetTempFileName();
        try
        {
            var options = new PowershellOptions(
                script,
                ["-RepoRoot", repoRoot, "-OutFile", tempFile],
                logOutputStream: false,
                timeout: TimeSpan.FromMinutes(5));

            var result = await _powershellHelper.Run(options, ct);

            if (result.ExitCode != 0)
            {
                _logger.LogWarning("Get-AllPkgProperties.ps1 failed with exit code {ExitCode}: {Output}", result.ExitCode, result.Output);
                return [];
            }

            if (File.Exists(tempFile) && new FileInfo(tempFile).Length > 0)
            {
                string json = await File.ReadAllTextAsync(tempFile, ct);
                var packages = JsonSerializer.Deserialize<List<RepoPackage>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
                return packages;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get packages from repository");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }

        return [];
    }

    private async Task<WorkItemData> FetchAllWorkItemsAsync(string projectName, string repoName, string language, List<string> packageTypes, CancellationToken ct)
    {
        // Build package type filter using IN clause
        var packageTypeList = string.Join(", ", packageTypes.Select(pt => $"'{pt}'"));
        var packageQuery = $"[System.WorkItemType] = 'Package' AND [Custom.Language] = '{language}' AND [Custom.PackageType] IN ({packageTypeList})";

        // Fetch Packages by language and package type
        var allPackages = await FetchWorkItemsAsync<PackageWorkItem>(projectName,
            packageQuery,
            MapToPackageWorkItem);
        _logger.LogInformation("Packages (all versions): {Count}", allPackages.Count);

        // Group packages by name then take the package with the latest "Custom.PackageVersionMajorMinor"
        var packages = GetLatestPackageVersions(allPackages);
        _logger.LogInformation("Packages (latest versions): {Count}", packages.Count);

        // Fetch all Owners (no filter needed)
        var owners = await FetchWorkItemsAsync<OwnerWorkItem>(projectName,
            "[System.WorkItemType] = 'Owner'",
            MapToOwnerWorkItem);
        _logger.LogInformation("Owners: {Count}", owners.Count);

        // Fetch all Labels (no filter needed)
        var labels = await FetchWorkItemsAsync<LabelWorkItem>(projectName,
            "[System.WorkItemType] = 'Label'",
            MapToLabelWorkItem);
        _logger.LogInformation("Labels: {Count}", labels.Count);

        // Fetch Label Owners by repository
        var labelOwners = await FetchWorkItemsAsync<LabelOwnerWorkItem>(projectName,
            $"[System.WorkItemType] = 'Label Owner' AND [Custom.Repository] = '{repoName}'",
            MapToLabelOwnerWorkItem);
        _logger.LogInformation("Label Owners: {Count}", labelOwners.Count);

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
        string project,
        string whereClause,
        Func<WorkItem, T> factory)
    {
        if (!System.Diagnostics.Debugger.IsAttached) {
            System.Diagnostics.Debugger.Launch();
        }

        var query = $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = '{project}' AND {whereClause}";
        var workItems = await _devOpsService.FetchWorkItemsPagedAsync(query, expand: WorkItemExpand.Relations);
        return workItems.Select(factory).ToList();
    }

    private static PackageWorkItem MapToPackageWorkItem(WorkItem wi)
    {
        return new PackageWorkItem
        {
            WorkItemId = wi.Id!.Value,
            PackageName = GetFieldValue(wi, "Custom.Package"),
            PackageVersionMajorMinor = GetFieldValue(wi, "Custom.PackageVersionMajorMinor"),
            Language = GetFieldValue(wi, "Custom.Language"),
            PackageType = GetFieldValue(wi, "Custom.PackageType"),
            ServiceName = GetFieldValue(wi, "Custom.ServiceName"),
            PackageDisplayName = GetFieldValue(wi, "Custom.PackageDisplayName"),
            GroupId = GetFieldValue(wi, "Custom.GroupId"),
            PackageRepoPath = GetFieldValue(wi, "Custom.PackageRepoPath"),
            RelatedIds = WorkItemData.ExtractRelatedIds(wi)
        };
    }

    private static OwnerWorkItem MapToOwnerWorkItem(WorkItem wi)
    {
        return new OwnerWorkItem
        {
            WorkItemId = wi.Id!.Value,
            GitHubAlias = GetFieldValue(wi, "Custom.GitHubAlias")
        };
    }

    private static LabelWorkItem MapToLabelWorkItem(WorkItem wi)
    {
        return new LabelWorkItem
        {
            WorkItemId = wi.Id!.Value,
            LabelName = GetFieldValue(wi, "Custom.Label")
        };
    }

    private static LabelOwnerWorkItem MapToLabelOwnerWorkItem(WorkItem wi)
    {
        return new LabelOwnerWorkItem
        {
            WorkItemId = wi.Id!.Value,
            LabelType = GetFieldValue(wi, "Custom.LabelType"),
            Repository = GetFieldValue(wi, "Custom.Repository"),
            RepoPath = GetFieldValue(wi, "Custom.RepoPath"),
            RelatedIds = WorkItemData.ExtractRelatedIds(wi)
        };
    }

    private static string GetFieldValue(WorkItem wi, string fieldName)
    {
        return wi.Fields.TryGetValue(fieldName, out var value) ? value?.ToString() ?? "" : "";
    }

    /// <summary>
    /// Groups packages by name and returns only the package with the highest version for each name.
    /// </summary>
    /// <param name="packages">List of all packages</param>
    /// <returns>List of packages with only the latest version for each package name</returns>
    internal static List<PackageWorkItem> GetLatestPackageVersions(List<PackageWorkItem> packages)
    {
        return packages
            .GroupBy(p => p.PackageName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(p => p.PackageVersionMajorMinor, MajorMinorVersionComparer.Default).First())
            .ToList();
    }

    private List<CodeownersEntry> BuildCodeownersEntries(
        WorkItemData data,
        Dictionary<string, RepoPackage> packageLookup,
        string repoRoot)
    {
        var entries = new List<CodeownersEntry>();

        // Build entries for packages
        foreach (var pkg in data.Packages.Values)
        {
            if (!packageLookup.TryGetValue(pkg.PackageName, out var repoPkg))
            {
                _errors.Add($"Package not found in repo: {pkg.PackageName}");
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

        // Identify Label Owners already linked to packages
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

    private static string BuildPathExpression(string dirPath, string repoRoot)
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

    private string WriteCodeownersFile(string path, List<CodeownersEntry> entries, string sectionName)
    {
        if (!File.Exists(path))
        {
            _logger.LogError("CODEOWNERS file not found: {Path}", path);
            throw new FileNotFoundException($"CODEOWNERS file not found: {path}");
        }

        var lines = File.ReadAllLines(path).ToList();
        var (headerStart, contentStart, sectionEnd) = CodeownersSectionFinder.FindSection(lines, sectionName);

        if (contentStart == -1)
        {
            _logger.LogError("'{SectionName}' section not found in CODEOWNERS file", sectionName);
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

        File.WriteAllLines(path, output);
        _logger.LogInformation("Updated: {Path}", path);

        return string.Join(Environment.NewLine, output);
    }

    private void PrintSummary(List<CodeownersEntry> entries)
    {
        int pathed = entries.Count(e => !string.IsNullOrWhiteSpace(e.PathExpression));
        int pathless = entries.Count - pathed;

        _logger.LogInformation("=== SUMMARY ===");
        _logger.LogInformation("Package entries: {Pathed}", pathed);
        _logger.LogInformation("Pathless entries: {Pathless}", pathless);
        _logger.LogInformation("Total: {Total}", entries.Count);

        if (_errors.Count > 0)
        {
            _logger.LogWarning("=== {Count} ERRORS ===", _errors.Count);
            foreach (var error in _errors)
            {
                _logger.LogWarning("  {Error}", error);
            }
        }

        _logger.LogInformation("Completed!");
    }
}
