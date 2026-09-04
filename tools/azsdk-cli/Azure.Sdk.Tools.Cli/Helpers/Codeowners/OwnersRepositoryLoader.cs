// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models.Codeowners;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Azure.Sdk.Tools.Cli.Helpers.Codeowners;

/// <summary>
/// Reads a checkout's ownership YAML off disk. This is the only place in the CODEOWNERS pipeline
/// that enumerates or opens files; the renderer and the validators work from the loaded model.
/// </summary>
public static class OwnersRepositoryLoader
{
    public const string ConfigPath = ".github/owners.config.yaml";

    /// <summary>Fragment file names, matched case-insensitively.</summary>
    private static readonly string[] FragmentFileNames = ["owners.yaml", "owners.yml"];

    /// <summary>
    /// Loads the config and every fragment it admits.
    /// </summary>
    /// <param name="errors">
    /// Receives <c>CFG-LOC-001</c> for fragments found outside <c>allowed-owner-yaml-paths</c> and
    /// schema errors for fragments that fail to parse. A malformed fragment is skipped so the rest
    /// of the repository still validates and the author sees every problem at once.
    /// </param>
    /// <exception cref="OwnersYamlException">
    /// The config itself is missing or unreadable. Nothing downstream is meaningful without it, so
    /// this is the one failure that stops the run.
    /// </exception>
    public static OwnersRepository Load(string repoRoot, List<OwnersValidationError> errors)
    {
        var configFile = Path.Combine(repoRoot, ConfigPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(configFile))
        {
            throw new OwnersYamlException($"{ConfigPath} not found under {repoRoot}.");
        }

        var config = OwnersYamlLoader.LoadConfig(File.ReadAllText(configFile), ConfigPath);

        var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        matcher.AddIncludePatterns(config.Configs.AllowedOwnerYamlPaths);

        var fragments = new List<OwnersFragment>();

        foreach (var relativePath in FindFragmentFiles(repoRoot))
        {
            if (!matcher.Match(relativePath).HasMatches)
            {
                errors.Add(new OwnersValidationError("CFG-LOC-001",
                    $"{relativePath}: ownership file is outside configs.allowed-owner-yaml-paths " +
                    $"({string.Join(", ", config.Configs.AllowedOwnerYamlPaths)}). " +
                    "Move it to an allowed location or add the location to the config."));
                continue;
            }

            try
            {
                fragments.Add(OwnersYamlLoader.LoadFragment(
                    File.ReadAllText(Path.Combine(repoRoot, relativePath)), relativePath));
            }
            catch (OwnersYamlException ex)
            {
                errors.Add(new OwnersValidationError("CFG-SCHEMA-001", ex.Message));
            }
        }

        return new OwnersRepository
        {
            RepoRoot = repoRoot,
            Config = config,
            // Provenance order. Everything downstream -- union order, error order, unsorted section
            // order -- derives from this, so it is established once, here.
            Fragments = [.. fragments.OrderBy(f => f.FilePath, StringComparer.Ordinal)],
        };
    }

    /// <summary>
    /// Walks the whole checkout rather than just the allowed globs, because a fragment hiding outside
    /// them is exactly what <c>CFG-LOC-001</c> exists to catch.
    /// </summary>
    public static IEnumerable<string> FindFragmentFiles(string repoRoot)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            MatchCasing = MatchCasing.CaseInsensitive,
        };

        return Directory.EnumerateFiles(repoRoot, "owners.y*ml", options)
            .Where(file => FragmentFileNames.Contains(Path.GetFileName(file), StringComparer.OrdinalIgnoreCase))
            .Select(file => Path.GetRelativePath(repoRoot, file).Replace('\\', '/'))
            .Where(path => !path.StartsWith(".git/", StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal);
    }
}
