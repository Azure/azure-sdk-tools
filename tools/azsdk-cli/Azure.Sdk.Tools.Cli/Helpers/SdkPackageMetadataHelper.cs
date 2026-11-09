using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;

namespace Azure.Sdk.Tools.Cli.Helpers;

public sealed class SdkPackageMetadataHelper(HttpClient http, string token)
{
    internal const string ManagementPlane = "management-plane";
    internal const string DataPlane = "data-plane";
    internal const string UnknownPlane = "unknown";
    public sealed record GenerationEvidence(List<PackageApiSource> Sources, string Plane, List<string> Issues);
    private sealed record ChangedPackages(HashSet<string> Packages, HashSet<string> KnownAtHead, List<string> Issues);

    private const int MaxMetadataFiles = 32;
    private const int MaxDirectoryEntries = 1000;
    private const int MaxPackageDirectories = 128;
    private const string GenerationLocationFile = "tsp-location.yaml";
    private readonly Dictionary<(string Repository, string Commit, string Directory), Task<string?>> _generationLocations = [];
    internal readonly record struct SdkSnapshot(string Repository, string HeadSha, SdkLanguage Language, string BaseRepository, string BaseSha, PackageInfo? LocalPackage = null);
    private sealed record GenerationLocation(string Directory, string CommitSha, string Repository);
    private sealed record GenerationLocationEvidence(GenerationLocation? Location, string Plane, List<string> Issues);

    public Task<GenerationEvidence> ReadLocalGenerationEvidenceAsync(PackageInfo package, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var path = Path.GetFullPath(package.PackagePath);
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException("The local SDK package directory does not exist.");
        }
        var relativePath = string.IsNullOrEmpty(package.RepoRoot) ? Path.GetFileName(path) : Path.GetRelativePath(package.RepoRoot, path).Replace('\\', '/');
        package.DirectoryPath = relativePath;
        var sdk = new SdkSnapshot("", "", package.Language, "", "", package);
        return ReadGenerationSourcesAsync(new([relativePath], [relativePath], []), sdk, ct);
    }

    internal async Task<GenerationEvidence> ReadGenerationEvidenceAsync(
        string prPath, SdkSnapshot sdk, CancellationToken ct)
    {
        var changedPackages = await GetChangedPackagesAsync(prPath, sdk, ct);
        if (changedPackages is null)
        {
            return new([], UnknownPlane, ["The changed-file listing reached GitHub's 3000-file limit; package discovery is incomplete."]);
        }
        return await ReadGenerationSourcesAsync(changedPackages, sdk, ct);
    }

    private async Task<GenerationEvidence> ReadGenerationSourcesAsync(
        ChangedPackages changedPackages, SdkSnapshot sdk, CancellationToken ct)
    {
        var packageEvidence = new List<GenerationEvidence>();
        foreach (var package in changedPackages.Packages.Order(StringComparer.Ordinal))
        {
            var evidence = await ReadPackageGenerationEvidenceAsync(package, changedPackages.KnownAtHead.Contains(package), sdk, ct);
            if (evidence is not null)
            {
                packageEvidence.Add(evidence);
            }
        }
        var issues = changedPackages.Issues.Concat(packageEvidence.SelectMany(evidence => evidence.Issues)).ToList();
        var planes = packageEvidence.Select(evidence => evidence.Plane).ToHashSet(StringComparer.Ordinal);
        if (changedPackages.Issues.Count > 0)
        {
            planes.Add(UnknownPlane);
        }
        if (planes.Count > 1)
        {
            issues.Add("Mixed SDK API planes require separate checks.");
        }
        if (packageEvidence.Count == 0)
        {
            issues.Add("No currently changed SDK packages remain at the checked head.");
        }
        return new([.. packageEvidence.SelectMany(evidence => evidence.Sources)], planes.Count == 1 ? planes.Single() : UnknownPlane, issues);
    }

    private async Task<GenerationEvidence?> ReadPackageGenerationEvidenceAsync(string package, bool knownAtHead, SdkSnapshot sdk, CancellationToken ct)
    {
        // Deleted and renamed source packages still count if their directory survives at this head.
        bool? exists = knownAtHead ? true : await PackageExistsAtHeadAsync(package, sdk, ct);
        if (exists == false)
        {
            return null;
        }
        if (exists is null)
        {
            return new([], UnknownPlane, [$"{package}: SDK package directory could not be established at the checked head."]);
        }
        var evidence = await ReadGenerationLocationAsync(package, sdk, ct);
        if (evidence.Location is null || evidence.Plane == DataPlane)
        {
            return new([], evidence.Plane, evidence.Issues);
        }
        var sources = await ReadPackageSourcesAsync(package, evidence.Location, sdk, evidence.Issues, ct);
        return new(sources, evidence.Plane, evidence.Issues);
    }

    private async Task<bool?> PackageExistsAtHeadAsync(string package, SdkSnapshot sdk, CancellationToken ct)
    {
        var encoded = string.Join('/', package.Split('/').Select(Uri.EscapeDataString));
        var listing = await ReadGitHubAsync($"repos/{sdk.Repository}/contents/{encoded}?ref={sdk.HeadSha}", ct);
        return listing switch { null => false, JsonArray => true, _ => null };
    }

    private async Task<GenerationLocationEvidence> ReadGenerationLocationAsync(
        string package, SdkSnapshot sdk, CancellationToken ct)
    {
        var path = $"{package}/{GenerationLocationFile}";
        var text = sdk.LocalPackage is null
            ? await ReadGenerationLocationTextAsync(sdk.Repository, sdk.HeadSha, package, ct)
            : await ReadLocalFileAsync(Path.Combine(sdk.LocalPackage.PackagePath, GenerationLocationFile), ct);
        if (text is null)
        {
            return new(null, UnknownPlane, [$"{package}: missing SDK generation metadata ({path})."]);
        }
        try
        {
            var metadata = GeneratedSdkMetadataHelper.ParseYaml(text);
            var directory = GeneratedSdkMetadataHelper.Scalar(metadata, "directory");
            var commit = GeneratedSdkMetadataHelper.Scalar(metadata, "commit");
            var repository = NormalizeSpecRepository(GeneratedSdkMetadataHelper.Scalar(metadata, "repo"));
            // Validate provenance before a data-plane hint can make the check not applicable.
            if (!IsSpecificationProjectDirectory(directory) || !GeneratedSdkMetadataHelper.IsCommitSha(commit) || repository is null)
            {
                return new(null, UnknownPlane, [$"{package}: missing or invalid TypeSpec directory, pinned spec commit, or spec repository."]);
            }
            return new(new(directory, commit, repository), GetProjectPlane(directory), []);
        }
        catch (Exception exception) when (exception is YamlDotNet.Core.YamlException or FormatException)
        {
            return new(null, UnknownPlane, [$"{package}: invalid YAML generation metadata."]);
        }
    }

    private static bool IsSpecificationProjectDirectory(string directory)
    {
        var segments = directory.Split('/');
        return segments.Length > 1 && segments[0] == "specification" &&
            segments.Skip(1).All(segment => !string.IsNullOrWhiteSpace(segment) && segment is not "." and not ".." && !segment.Contains('\\'));
    }

    private static string GetProjectPlane(string directory)
    {
        var segments = directory.Split('/');
        if (segments.Contains(DataPlane))
        {
            return DataPlane;
        }
        if (segments.Contains("resource-manager") || directory.Contains(".Management", StringComparison.OrdinalIgnoreCase))
        {
            return ManagementPlane;
        }
        return UnknownPlane;
    }

    private async Task<List<string>> ReadMetadataPathsAsync(string package, SdkSnapshot sdk, List<string> issues, CancellationToken ct)
    {
        var location = GeneratedSdkMetadataHelper.GetMetadataFileLocation(sdk.Language);
        var directory = location.Directory.Length == 0 ? package : $"{package}/{location.Directory}";
        if (string.IsNullOrEmpty(location.Pattern))
        {
            issues.Add($"{package}: SDK metadata layout is unavailable for this language.");
            return [];
        }
        if (sdk.Language != SdkLanguage.Java)
        {
            return [$"{directory}/{location.Pattern}"];
        }
        if (sdk.LocalPackage is not null)
        {
            var localDirectory = Path.Combine(sdk.LocalPackage.PackagePath, location.Directory);
            var localPaths = Directory.Exists(localDirectory)
                ? Directory.EnumerateFiles(localDirectory, location.Pattern, SearchOption.TopDirectoryOnly).Take(MaxMetadataFiles + 1)
                    .Order(StringComparer.Ordinal).Select(path => $"{directory}/{Path.GetFileName(path)}").ToList()
                : [];
            if (localPaths.Count > MaxMetadataFiles)
            {
                issues.Add($"{package}: Java metadata exceeds the 32-file evidence limit.");
                return [];
            }
            if (localPaths.Count == 0)
            {
                issues.Add($"{package}: missing SDK generation metadata ({directory}/{location.Pattern}).");
            }
            return localPaths;
        }
        var encoded = string.Join('/', directory.Split('/').Select(Uri.EscapeDataString));
        var listing = await ReadGitHubAsync($"repos/{sdk.Repository}/contents/{encoded}?ref={sdk.HeadSha}", ct);
        if (listing is not null && (listing is not JsonArray entries || entries.Count >= MaxDirectoryEntries))
        {
            issues.Add($"{package}: Java metadata directory listing is invalid or incomplete.");
            return [];
        }
        var paths = (listing as JsonArray ?? []).Where(entry => Text(entry?["type"]) == "file")
            .Select(entry => Text(entry?["name"]))
            .Where(name => name.EndsWith(location.Pattern[1..], StringComparison.Ordinal) && name.IndexOfAny(['/', '\\', '\0']) < 0)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Select(name => $"{directory}/{name}").ToList();
        if (paths.Count > MaxMetadataFiles)
        {
            issues.Add($"{package}: Java metadata exceeds the 32-file evidence limit.");
            return [];
        }
        if (paths.Count == 0)
        {
            issues.Add($"{package}: missing SDK generation metadata ({directory}/{location.Pattern}).");
        }
        return paths;
    }

    private async Task<List<PackageApiSource>> ReadPackageSourcesAsync(
        string package, GenerationLocation location, SdkSnapshot sdk, List<string> issues, CancellationToken ct)
    {
        var metadataPaths = await ReadMetadataPathsAsync(package, sdk, issues, ct);
        var sources = new List<PackageApiSource>();
        string? inferredProvider = null;
        foreach (var metadataPath in metadataPaths)
        {
            var text = sdk.LocalPackage is null
                ? await ReadFileAsync(sdk.Repository, sdk.HeadSha, metadataPath, ct)
                : await ReadLocalFileAsync(Path.Combine(sdk.LocalPackage.PackagePath, metadataPath[(package.Length + 1)..]), ct);
            if (text is null)
            {
                issues.Add($"{package}: missing SDK generation metadata ({metadataPath}).");
                continue;
            }
            var versions = ReadPackageApiVersions(package, text, location, issues);
            foreach (var entry in versions)
            {
                var provider = entry.Key;
                if (provider.Length == 0)
                {
                    // Scalar apiVersion has no provider key; use the pinned TypeSpec namespace.
                    inferredProvider ??= GeneratedSdkMetadataHelper.ProviderNamespace(await ReadFileAsync(location.Repository, location.CommitSha, $"{location.Directory}/main.tsp", ct) ?? "");
                    provider = inferredProvider;
                }
                if (!Regex.IsMatch(provider, @"\A[\w-]+(?:\.[\w-]+)+\z") ||
                    !Regex.IsMatch(entry.Value, @"\A\d{4}-\d{2}-\d{2}(?:-[a-z0-9-]+)?\z", RegexOptions.IgnoreCase))
                {
                    issues.Add($"{package}: invalid namespace/API-version entry.");
                    continue;
                }
                sources.Add(new PackageApiSource
                {
                    PackagePath = package,
                    Configuration = $"{location.Directory}/tspconfig.yaml",
                    CommitSha = location.CommitSha,
                    SpecRepository = location.Repository,
                    ExpectedNamespace = provider,
                    NamespaceIsInferred = entry.Key.Length == 0,
                    ApiVersion = entry.Value,
                    MetadataFiles = [$"{package}/{GenerationLocationFile}", .. metadataPaths]
                });
            }
        }
        if (sources.GroupBy(source => source.ExpectedNamespace, StringComparer.OrdinalIgnoreCase)
            .Any(group => group.Select(source => source.ApiVersion).Distinct(StringComparer.Ordinal).Count() > 1))
        {
            issues.Add($"{package}: conflicting API versions across SDK metadata files; no latest-version selection.");
            return [];
        }
        return sources.GroupBy(source => source.ExpectedNamespace, StringComparer.OrdinalIgnoreCase)
            .SelectMany(group => group.DistinctBy(source => source.NamespaceIsInferred)).ToList();
    }

    private static Dictionary<string, string> ReadPackageApiVersions(string package, string text, GenerationLocation location, List<string> issues)
    {
        try
        {
            if (JsonNode.Parse(text) is not JsonObject metadata)
            {
                issues.Add($"{package}: invalid SDK API-version metadata.");
                return [];
            }
            if (metadata["commit"] is not null && Text(metadata["commit"]) != location.CommitSha ||
                metadata["typespec_src"] is not null && Text(metadata["typespec_src"]) != location.Directory ||
                metadata["repository_url"] is not null && NormalizeSpecRepository(Text(metadata["repository_url"])) != location.Repository)
            {
                issues.Add($"{package}: SDK metadata conflicts with {GenerationLocationFile}.");
                return [];
            }
            var versions = PackageInfoHelper.ReadApiVersionsByProvider(metadata);
            if (versions.Count == 0)
            {
                issues.Add($"{package}: missing or invalid API-version metadata (apiVersions/apiVersion).");
            }
            return versions;
        }
        catch (InvalidOperationException)
        {
            issues.Add($"{package}: conflicting API versions; no latest-version selection.");
        }
        catch (Exception exception) when (exception is JsonException or FormatException)
        {
            issues.Add($"{package}: invalid JSON generation metadata.");
        }
        return [];
    }

    private async Task<ChangedPackages?> GetChangedPackagesAsync(
        string prPath, SdkSnapshot sdk, CancellationToken ct)
    {
        const int pageSize = 100;
        const int maxPages = 30;
        var changedFiles = new Dictionary<string, bool>(StringComparer.Ordinal);
        for (var page = 1; page <= maxPages; page++)
        {
            var files = await ReadGitHubAsync($"{prPath}/files?per_page={pageSize}&page={page}", ct) as JsonArray
                ?? throw new InvalidOperationException("Changed SDK files are unavailable.");
            foreach (var file in files)
            {
                var filename = Text(file?["filename"]);
                var status = Text(file?["status"]);
                changedFiles[filename] = status != "removed" || changedFiles.GetValueOrDefault(filename);
                if (status == "renamed")
                {
                    changedFiles.TryAdd(Text(file?["previous_filename"]), false);
                }
            }
            if (files.Count < pageSize)
            {
                return await FindChangedPackagesAsync(changedFiles, sdk, ct);
            }
        }
        return null;
    }

    private async Task<ChangedPackages> FindChangedPackagesAsync(Dictionary<string, bool> changedFiles, SdkSnapshot sdk, CancellationToken ct)
    {
        var packages = new HashSet<string>(StringComparer.Ordinal);
        var knownAtHead = new HashSet<string>(StringComparer.Ordinal);
        var issues = new List<string>();
        foreach (var file in changedFiles.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            var directories = GetPackageDirectories(file.Key, sdk.Language);
            if (directories.Length == 0)
            {
                continue;
            }
            var package = await FindPackageRootAsync(directories, sdk.Repository, sdk.HeadSha, ct);
            if (!file.Value && GeneratedSdkMetadataHelper.IsCommitSha(sdk.BaseSha) && !string.IsNullOrEmpty(sdk.BaseRepository))
            {
                var previousPackage = await FindPackageRootAsync(directories, sdk.BaseRepository, sdk.BaseSha, ct);
                if (previousPackage is not null && (package is null || previousPackage.StartsWith($"{package}/", StringComparison.Ordinal)))
                {
                    package = previousPackage;
                }
            }
            if (package is null)
            {
                issues.Add($"{file.Key}: package root cannot be established from {GenerationLocationFile} at the checked SDK head or pinned PR base.");
                continue;
            }
            packages.Add(package);
            if (file.Value)
            {
                knownAtHead.Add(package);
            }
        }
        return new(packages, knownAtHead, issues);
    }

    private async Task<string?> FindPackageRootAsync(IEnumerable<string> directories, string repository, string commit, CancellationToken ct)
    {
        foreach (var directory in directories)
        {
            if (await ReadGenerationLocationTextAsync(repository, commit, directory, ct) is not null)
            {
                return directory;
            }
        }
        return null;
    }

    private Task<string?> ReadGenerationLocationTextAsync(string repository, string commit, string directory, CancellationToken ct)
    {
        var key = (repository, commit, directory);
        if (!_generationLocations.TryGetValue(key, out var reading))
        {
            if (_generationLocations.Count >= MaxPackageDirectories)
            {
                throw new InvalidOperationException("SDK package discovery exceeded its directory evidence limit.");
            }
            reading = ReadFileAsync(repository, commit, $"{directory}/{GenerationLocationFile}", ct);
            _generationLocations.Add(key, reading);
        }
        return reading;
    }

    private static string[] GetPackageDirectories(string changedFile, SdkLanguage language)
    {
        var segments = changedFile.Split('/');
        if (segments.Length < 3 || segments[0] != "sdk" || segments[1] == "test" ||
            segments.Any(segment => segment is "" or "." or ".." || segment.Contains('\\')) ||
            language == SdkLanguage.Go && segments[1] != "resourcemanager")
        {
            return [];
        }
        return Enumerable.Range(2, segments.Length - 2).Reverse()
            .Select(length => string.Join('/', segments.Take(length))).ToArray();
    }

    private static string? NormalizeSpecRepository(string value)
    {
        var repository = value.TrimEnd('/').Replace("https://github.com/", "", StringComparison.OrdinalIgnoreCase);
        if (repository.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            repository = repository[..^4];
        }
        return repository.Equals("Azure/azure-rest-api-specs", StringComparison.OrdinalIgnoreCase) ? "Azure/azure-rest-api-specs" :
            repository.Equals("Azure/azure-rest-api-specs-pr", StringComparison.OrdinalIgnoreCase) ? "Azure/azure-rest-api-specs-pr" : null;
    }

    private Task<JsonNode?> ReadGitHubAsync(string path, CancellationToken ct) =>
        GitHubService.ReadJsonEvidenceAsync(http, path, token, ct);

    private Task<string?> ReadFileAsync(string repository, string commit, string path, CancellationToken ct) =>
        GitHubService.ReadFileEvidenceAsync(http, repository, commit, path, token, ct);

    private static async Task<string?> ReadLocalFileAsync(string path, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!File.Exists(path))
        {
            return null;
        }
        await using var stream = File.OpenRead(path);
        if (stream.Length > HttpResponseHelper.MaxEvidenceBytes)
        {
            throw new InvalidOperationException("SDK metadata exceeded the evidence size limit.");
        }
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(ct);
    }

    private static string Text(JsonNode? value) => value is JsonValue scalar && scalar.TryGetValue<string>(out var text) ? text : "";
}
