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

    /// <summary>
    /// Reads the owners config, or returns null when the repository has not adopted one.
    /// A config that exists but does not parse still throws: silently substituting defaults would
    /// apply the wrong ownership minimums without saying so.
    /// </summary>
    public static OwnersConfig? TryLoadConfig(string repoRoot)
    {
        var configFile = Path.Combine(repoRoot, ConfigPath.Replace('/', Path.DirectorySeparatorChar));

        return File.Exists(configFile)
            ? OwnersYamlLoader.LoadConfig(File.ReadAllText(configFile), ConfigPath)
            : null;
    }

    /// <summary>
    /// Loads the config and every fragment it admits.
    /// </summary>
    /// <param name="errors">
    /// Receives <c>CFG-LOC-001</c> for fragments found outside <c>allowed-owner-yaml-paths</c> and
    /// schema errors for fragments that fail to parse. A malformed fragment is skipped so the rest
    /// of the repository still validates and the author sees every problem at once.
    /// </param>
    /// <exception cref="OwnersYamlException">
    /// The config is missing or unreadable. Nothing downstream is meaningful without it, so it stops
    /// the run.
    /// </exception>
    public static OwnersRepository Load(string repoRoot, List<OwnersValidationError> errors)
    {
        var config = TryLoadConfig(repoRoot)
            ?? throw new OwnersYamlException($"{ConfigPath} not found under {repoRoot}.");

        var matcher = CreateMatcher(settings: config.Configs);

        var fragments = new List<OwnersFragment>();

        foreach (var relativePath in FindFragmentFiles(repoRoot, config.Configs))
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
    /// Repo-relative paths of every ownership fragment in the checkout, under the file name
    /// <paramref name="settings"/> declares. This is the one fragment scan: <c>generate</c>,
    /// <c>check-package</c> and <c>lint-fragments</c> all reach the same set of files through it,
    /// rather than each deciding for itself what counts as a fragment.
    /// <para>
    /// Walks the whole checkout rather than the allowed globs, because a fragment hiding outside them
    /// is what <c>CFG-LOC-001</c> exists to catch.
    /// </para>
    /// </summary>
    public static IReadOnlyList<string> FindFragmentFiles(string repoRoot, OwnersConfigSettings settings)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            MatchCasing = MatchCasing.CaseInsensitive,
        };

        return settings.FragmentFileNames
            .SelectMany(fileName => Directory.EnumerateFiles(repoRoot, fileName, options))
            .Select(file => Path.GetRelativePath(repoRoot, file).Replace('\\', '/'))
            .Where(path => !path.StartsWith(".git/", StringComparison.Ordinal))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();
    }

    internal static Matcher CreateMatcher(OwnersConfigSettings settings)
    {
        var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        matcher.AddIncludePatterns(settings.AllowedOwnerYamlPaths);

        return matcher;
    }

}
