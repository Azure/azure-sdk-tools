// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine;
using System.ComponentModel;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Tools.Core;

namespace Azure.Sdk.Tools.Cli.Tools.EngSys;

[Description("Generate PackageInfo JSON files used by CI pipelines.")]
public class PackageInfoTool(
    IGitHelper gitHelper,
    ILogger<PackageInfoTool> _logger,
    IPackageInfoHelper packageInfoHelper,
    IEnumerable<LanguageService> languageServices
) : LanguageMcpTool(languageServices, gitHelper, _logger)
{
    public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.EngSys];

    private readonly Option<string> outDirOpt = new("--out-dir")
    {
        Description = "Output directory for PackageInfo JSON files",
        Required = true,
    };

    private readonly Option<string> serviceDirectoryOpt = new("--service-directory")
    {
        Description = "Service directory under sdk/ to scan (e.g. storage). Leave empty to scan all services.",
        Required = false,
    };

    private readonly Option<bool> ciOpt = new("--ci")
    {
        Description = "Select packages based on a CI PR diff (uses Azure Pipelines environment variables)",
        Required = false,
        DefaultValueFactory = _ => false,
    };

    private readonly Option<string> repoRootOpt = new("--repo-root")
    {
        Description = "Path to the repository root. Defaults to the repo containing the working directory.",
        Required = false,
    };

    private readonly Option<bool> addDevVersionOpt = new("--add-dev-version")
    {
        Description = "Add DevVersion to PackageInfo output (used for daily builds).",
        Required = false,
        DefaultValueFactory = _ => false,
    };

    private readonly Option<string[]> artifactListOpt = new("--artifact", "-a")
    {
        Description = "Artifact name(s) to filter the PackageInfo output (repeatable).",
        Required = false,
        AllowMultipleArgumentsPerToken = true,
    };

    protected override Command GetCommand() => new("package-info", "Generate PackageInfo JSON files for CI pipelines")
    {
        outDirOpt,
        serviceDirectoryOpt,
        ciOpt,
        repoRootOpt,
        addDevVersionOpt,
        artifactListOpt,
    };

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        try
        {
            var options = ParseOptions(parseResult);
            if (options.CiMode && !string.IsNullOrWhiteSpace(options.ServiceDirectory))
            {
                logger.LogWarning("Ignoring --service-directory because --ci is set.");
                options = options with { ServiceDirectory = string.Empty };
            }
            return await Execute(options, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate PackageInfo output.");
            return new DefaultCommandResponse { ResponseError = ex.Message };
        }
    }

    private PackageInfoOptions ParseOptions(ParseResult parseResult)
    {
        return new PackageInfoOptions(
            parseResult.GetValue(outDirOpt),
            parseResult.GetValue(serviceDirectoryOpt),
            parseResult.GetValue(ciOpt),
            parseResult.GetValue(repoRootOpt),
            parseResult.GetValue(addDevVersionOpt),
            parseResult.GetValue(artifactListOpt) ?? []);
    }

    private async Task<CommandResponse> Execute(PackageInfoOptions options, CancellationToken ct)
    {
        NormalizedPath repoRoot = !string.IsNullOrEmpty(options.RepoRootOverride)
                    ? RealPath.GetRealPath(options.RepoRootOverride)
                    : await gitHelper.DiscoverRepoRootAsync(Environment.CurrentDirectory, ct);

        var packages = await GetAllPackages(repoRoot, options.ServiceDirectory, ct);
        if (packages.Count == 0)
        {
            return new DefaultCommandResponse { Message = "No packages found to process." };
        }

        var selectedPackages = await SelectPackages(repoRoot, packages, options.CiMode, ct);
        if (selectedPackages.Count == 0)
        {
            return new DefaultCommandResponse { Message = "No packages matched the requested criteria." };
        }

        selectedPackages = packageInfoHelper.FilterPackagesByArtifact(selectedPackages, options.ArtifactList);
        var outputFiles = WritePackageInfoFiles(selectedPackages, options.OutDir, options.AddDevVersion);

        return new DefaultCommandResponse
        {
            Message = $"Files written to {options.OutDir}:",
            Result = outputFiles
        };
    }

    private async Task<List<PackageInfo>> GetAllPackages(string repoRoot, string? serviceDirectory, CancellationToken ct)
    {
        var languageService = await GetLanguageServiceAsync(repoRoot, ct)
            ?? throw new InvalidOperationException("Unable to resolve language service for repository. Ensure repository name matches azure-sdk-for-<lang>.");

        var packages = await languageService.DiscoverPackagesAsync(repoRoot, serviceDirectory, ct);
        return packages.ToList();
    }

    private async Task<List<PackageInfo>> SelectPackages(string repoRoot, List<PackageInfo> packages, bool ciMode, CancellationToken ct)
    {
        if (!ciMode)
        {
            return packages;
        }

        var diff = await BuildDiff(repoRoot, ct);
        return SelectPackagesForDiff(repoRoot, packages, diff);
    }

    private List<string> WritePackageInfoFiles(List<PackageInfo> packages, string outDir, bool addDevVersion)
    {
        var exportedPaths = new Dictionary<string, PackageInfo>(StringComparer.OrdinalIgnoreCase);
        var outputFiles = new List<string>();

        foreach (var pkg in packages)
        {
            var packageName = pkg.PackageName ?? string.Empty;
            if (string.IsNullOrWhiteSpace(packageName))
            {
                continue;
            }

            var outputPath = Path.Combine(outDir, $"{packageName}.json");
            if (exportedPaths.TryGetValue(outputPath, out var existing) && existing.IsNewSdk)
            {
                logger.LogInformation("Track 2 package info with file name {Path} already exported. Skipping export.", outputPath);
                continue;
            }

            LogPackageDetails(pkg, outputPath);

            exportedPaths[outputPath] = pkg;
            packageInfoHelper.WritePackageInfoFile(pkg, outputPath, addDevVersion);
            outputFiles.Add(outputPath);
        }

        return outputFiles;
    }

    private void LogPackageDetails(PackageInfo pkg, string outputPath)
    {
        logger.LogInformation("Package Name: {Name}", pkg.PackageName ?? "(unknown)");
        logger.LogInformation("Package Version: {Version}", pkg.PackageVersion ?? "(unknown)");
        logger.LogInformation("Package SDK Type: {SdkType}", pkg.SdkTypeString ?? "(unknown)");
        logger.LogInformation("Artifact Name: {Artifact}", pkg.ArtifactName ?? "(unknown)");
        if (!string.IsNullOrEmpty(pkg.Group))
        {
            logger.LogInformation("GroupId: {Group}", pkg.Group);
        }
        if (!string.IsNullOrEmpty(pkg.ReleaseStatus))
        {
            logger.LogInformation("Release date: {ReleaseStatus}", pkg.ReleaseStatus);
        }
        logger.LogInformation("Output path of json file: {OutputPath}", outputPath);
    }

    private async Task<PackageInfoDiff> BuildDiff(string repoRoot, CancellationToken ct)
    {
        var sourceCommitish = Environment.GetEnvironmentVariable("SYSTEM_PULLREQUEST_SOURCECOMMITID") ?? "HEAD";
        var targetBranchValue = Environment.GetEnvironmentVariable("SYSTEM_PULLREQUEST_TARGETBRANCH") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(targetBranchValue))
        {
            logger.LogWarning("SYSTEM_PULLREQUEST_TARGETBRANCH is not set. No diff will be calculated.");
            return new PackageInfoDiff([], [], [], [], Environment.GetEnvironmentVariable("SYSTEM_PULLREQUEST_PULLREQUESTNUMBER") ?? "-1");
        }

        var targetCommitish = NormalizeTargetBranch(targetBranchValue);
        var diffPath = NormalizeDiffPath(repoRoot, GetCiTargetPath(repoRoot));

        var changedFiles = await gitHelper.GetChangedFilesAsync(repoRoot, targetCommitish, sourceCommitish, diffPath, "d", ct);
        var deletedFiles = await gitHelper.GetChangedFilesAsync(repoRoot, targetCommitish, sourceCommitish, diffPath, "D", ct);

        var changedServices = GetChangedServices(changedFiles);
        var prNumber = Environment.GetEnvironmentVariable("SYSTEM_PULLREQUEST_PULLREQUESTNUMBER") ?? "-1";

        return new PackageInfoDiff(
            ChangedFiles: changedFiles,
            ChangedServices: changedServices,
            ExcludePaths: [],
            DeletedFiles: deletedFiles,
            PrNumber: prNumber
        );
    }

    private List<PackageInfo> SelectPackagesForDiff(string repoRoot, List<PackageInfo> allPackages, PackageInfoDiff diff)
    {
        var targetedFiles = new List<string>(diff.ChangedFiles);
        if (diff.DeletedFiles.Count > 0)
        {
            targetedFiles.AddRange(diff.DeletedFiles);
        }

        var triggerPaths = GetTriggerPaths(allPackages);
        targetedFiles = UpdateTargetedFilesForExclude(targetedFiles, diff.ExcludePaths);
        targetedFiles = UpdateTargetedFilesForTriggerPaths(targetedFiles, triggerPaths);
        targetedFiles = targetedFiles
            .OrderByDescending(path => path.Split('/').Length)
            .ToList();

        var packagesWithChanges = new List<PackageInfo>();
        var additionalValidationPackages = new List<NormalizedPath>();
        var lookup = new Dictionary<string, PackageInfo>(StringComparer.OrdinalIgnoreCase);
        var directoryIndex = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        NormalizedPath normalizedRepoRoot = repoRoot;

        foreach (var pkg in allPackages)
        {
            var pkgDirectory = ResolveRepoPath(repoRoot, pkg.DirectoryPath);
            var lookupKey = pkgDirectory.Replace(normalizedRepoRoot, string.Empty).TrimStart('/', '\\');
            lookup[lookupKey] = pkg;

            foreach (var file in targetedFiles)
            {
                NormalizedPath filePath = Path.Combine(repoRoot, file);
                var shouldInclude = string.Equals(filePath, pkgDirectory, StringComparison.OrdinalIgnoreCase) ||
                                    filePath.StartsWith($"{pkgDirectory}/", StringComparison.OrdinalIgnoreCase);

                if (!shouldInclude)
                {
                    foreach (var triggerPath in pkg.TriggeringPaths)
                    {
                        var resolved = ResolveRepoPath(repoRoot, triggerPath);
                        var includedForValidation =
                            string.Equals(filePath, resolved, StringComparison.OrdinalIgnoreCase) ||
                            filePath.StartsWith($"{resolved}/", StringComparison.OrdinalIgnoreCase);

                        if (includedForValidation)
                        {
                            shouldInclude = true;
                            break;
                        }
                    }

                    if (!shouldInclude)
                    {
                        var triggeringCiYmls = pkg.TriggeringPaths
                            .Where(path => path.Contains("ci", StringComparison.OrdinalIgnoreCase) &&
                                           path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase));

                        foreach (var yml in triggeringCiYmls)
                        {
                            var ciYml = ResolveRepoPath(repoRoot, yml);
                            NormalizedPath directory = Path.GetDirectoryName(ciYml) ?? string.Empty;

                            if (!filePath.StartsWith($"{directory}/", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            var relative = filePath.Substring(directory.Length + 1);
                            if (relative.Contains("/") || !Path.HasExtension(relative))
                            {
                                continue;
                            }

                            if (!directoryIndex.TryGetValue(directory, out var soleCiYml))
                            {
                                var directoryForSearch = directory.Replace("/", Path.DirectorySeparatorChar.ToString());
                                soleCiYml = Directory.Exists(directoryForSearch) &&
                                            Directory.GetFiles(directoryForSearch, "ci*.yml").Length == 1;
                                directoryIndex[directory] = soleCiYml;
                            }

                            if (soleCiYml && filePath.StartsWith(directory, StringComparison.OrdinalIgnoreCase))
                            {
                                shouldInclude = true;
                                break;
                            }
                        }
                    }
                }

                if (shouldInclude)
                {
                    packagesWithChanges.Add(pkg);
                    if (pkg.AdditionalValidationPackages != null)
                    {
                        additionalValidationPackages.AddRange(pkg.AdditionalValidationPackages);
                    }
                    break;
                }
            }
        }

        var existingPackageNames = new HashSet<string>(
            packagesWithChanges.Select(pkg => pkg.PackageName ?? string.Empty),
            StringComparer.OrdinalIgnoreCase);

        foreach (var addition in additionalValidationPackages)
        {
            if (string.IsNullOrWhiteSpace(addition))
            {
                continue;
            }

            // addition is already normalized (forward slashes) from PackageInfo
            var key = Path.IsPathRooted(addition)
                ? addition.Replace(normalizedRepoRoot, string.Empty).TrimStart('/', '\\')
                : addition.TrimStart('/', '\\');

            if (lookup.TryGetValue(key, out var pkg) && !existingPackageNames.Contains(pkg.PackageName ?? string.Empty))
            {
                pkg.IncludedForValidation = true;
                packagesWithChanges.Add(pkg);
            }
        }

        if (packagesWithChanges.Count == 0)
        {
            foreach (var pkg in allPackages.Where(pkg =>
                         string.Equals(pkg.ServiceDirectory, "template", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(pkg.ServiceDirectory, "template/aztemplate", StringComparison.OrdinalIgnoreCase)))
            {
                pkg.IncludedForValidation = true;
                packagesWithChanges.Add(pkg);
            }
        }

        return packagesWithChanges;
    }

    private static List<string> GetChangedServices(List<string> changedFiles)
    {
        var regex = new Regex(@"sdk/([^/]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        return changedFiles
            .Select(path => path.Replace("\\", "/"))
            .Select(path =>
            {
                var match = regex.Match(path);
                return match.Success ? match.Groups[1].Value : null;
            })
            .Where(value => !string.IsNullOrEmpty(value))
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> GetTriggerPaths(IEnumerable<PackageInfo> packages)
    {
        var triggerPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pkg in packages)
        {
            foreach (var triggerPath in pkg.TriggeringPaths)
            {
                if (!string.IsNullOrEmpty(triggerPath) && Path.HasExtension(triggerPath))
                {
                    triggerPaths.Add(triggerPath);
                }
            }
        }

        return triggerPaths.ToList();
    }

    private static List<string> UpdateTargetedFilesForExclude(List<string> targetedFiles, List<string> excludePaths)
    {
        var results = new List<string>();
        foreach (var file in targetedFiles)
        {
            var shouldExclude = excludePaths.Any(exclude => file.StartsWith(exclude, StringComparison.CurrentCultureIgnoreCase));
            if (!shouldExclude)
            {
                results.Add(file);
            }
        }

        return results;
    }

    private static List<string> UpdateTargetedFilesForTriggerPaths(List<string> targetedFiles, List<string> triggerPaths)
    {
        var processedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var triggers = new List<string>(triggerPaths);

        foreach (var file in targetedFiles)
        {
            var isExistingTriggerPath = false;
            var triggerIndex = -1;

            for (var i = 0; i < triggers.Count; i++)
            {
                var triggerPath = triggers[i];
                if (!string.IsNullOrEmpty(triggerPath) && string.Equals($"/{file}", triggerPath, StringComparison.OrdinalIgnoreCase))
                {
                    isExistingTriggerPath = true;
                    triggerIndex = i;
                    break;
                }
            }

            if (isExistingTriggerPath && triggerIndex >= 0)
            {
                triggers.RemoveAt(triggerIndex);
                processedFiles.Add(file);
                continue;
            }

            var normalized = file.Replace("/", Path.DirectorySeparatorChar.ToString());
            var directoryPath = Path.GetDirectoryName(normalized);
            if (!string.IsNullOrEmpty(directoryPath))
            {
                processedFiles.Add((NormalizedPath)directoryPath);
            }
            else
            {
                processedFiles.Add(file);
            }
        }

        return processedFiles.ToList();
    }

    private static string NormalizeTargetBranch(string targetBranch)
    {
        if (targetBranch.StartsWith("origin/", StringComparison.OrdinalIgnoreCase))
        {
            return targetBranch;
        }

        if (targetBranch.StartsWith("refs/heads/", StringComparison.OrdinalIgnoreCase))
        {
            return $"origin/{targetBranch["refs/heads/".Length..]}";
        }

        return $"origin/{targetBranch}";
    }

    private static string? NormalizeDiffPath(string repoRoot, string? targetPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return null;
        }

        var fullPath = Path.IsPathRooted(targetPath)
            ? targetPath
            : Path.Combine(repoRoot, targetPath);

        if (!fullPath.StartsWith(repoRoot, StringComparison.OrdinalIgnoreCase))
        {
            return targetPath;
        }

        return Path.GetRelativePath(repoRoot, fullPath).Replace("\\", "/");
    }

    private static NormalizedPath ResolveRepoPath(string repoRoot, NormalizedPath path)
    {
        if (path.IsEmpty)
        {
            return repoRoot;
        }

        return Path.IsPathRooted(path) ? path : Path.Combine(repoRoot, path);
    }

    private static string GetCiTargetPath(string repoRoot)
    {
        var envPath = Environment.GetEnvironmentVariable("BUILD_SOURCESDIRECTORY")
            ?? Environment.GetEnvironmentVariable("SYSTEM_DEFAULTWORKINGDIRECTORY");
        return string.IsNullOrWhiteSpace(envPath) ? repoRoot : envPath;
    }

    private sealed record PackageInfoOptions(
        string OutDir,
        string? ServiceDirectory,
        bool CiMode,
        string? RepoRootOverride,
        bool AddDevVersion,
        string[] ArtifactList);

    private record PackageInfoDiff(
        List<string> ChangedFiles,
        List<string> ChangedServices,
        List<string> ExcludePaths,
        List<string> DeletedFiles,
        [property: JsonPropertyName("PRNumber")] string PrNumber
    );
}
