// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.RegularExpressions;

namespace Azure.Sdk.Tools.Cli.Helpers;

/// <summary>
/// Helper for creating and updating Azure SDK CI YAML pipeline files.
/// Currently supports .NET client SDK ci.yml files.
/// </summary>
public interface ICiYamlHelper
{
    /// <summary>
    /// Creates a new ci.yml file content for a client SDK package.
    /// </summary>
    string CreateClientCiYaml(string serviceDirectory, string packageName);

    /// <summary>
    /// Adds a new artifact entry to an existing ci.yml file.
    /// Returns the updated YAML content, or null if the artifact already exists.
    /// </summary>
    string? AddArtifactToCiYaml(string existingYaml, string packageName);

    /// <summary>
    /// Checks whether a package is already listed as an artifact in the given ci.yml content.
    /// </summary>
    bool HasArtifact(string yamlContent, string packageName);

    /// <summary>
    /// Generates a safe name for pipeline variable usage by removing dots.
    /// e.g., "Azure.Storage.Blobs" → "AzureStorageBlobs"
    /// </summary>
    string GenerateSafeName(string packageName);

    /// <summary>
    /// Finds the path to an existing ci.yml for a service directory, or returns null.
    /// </summary>
    string? FindCiYamlPath(string repoRoot, string serviceDirectory);
}

public partial class CiYamlHelper : ICiYamlHelper
{
    private const string CiYamlTemplate =
        """
        # NOTE: Please refer to https://aka.ms/azsdk/engsys/ci-yaml before editing this file.

        trigger:
          branches:
            include:
            - main
            - hotfix/*
            - release/*
          paths:
            include:
            - sdk/{serviceDirectory}/

        pr:
          branches:
            include:
            - main
            - feature/*
            - hotfix/*
            - release/*
          paths:
            include:
            - sdk/{serviceDirectory}/

        extends:
          template: /eng/pipelines/templates/stages/archetype-sdk-client.yml
          parameters:
            ServiceDirectory: {serviceDirectory}
            ArtifactName: packages
            Artifacts:
            - name: {packageName}
              safeName: {safeName}
        """;

    public string CreateClientCiYaml(string serviceDirectory, string packageName)
    {
        var safeName = GenerateSafeName(packageName);
        return CiYamlTemplate
            .Replace("{serviceDirectory}", serviceDirectory)
            .Replace("{packageName}", packageName)
            .Replace("{safeName}", safeName)
            + Environment.NewLine;
    }

    public string? AddArtifactToCiYaml(string existingYaml, string packageName)
    {
        if (HasArtifact(existingYaml, packageName))
        {
            return null;
        }

        var safeName = GenerateSafeName(packageName);
        var artifactEntry = $"    - name: {packageName}{Environment.NewLine}      safeName: {safeName}";

        // Find the last artifact entry and insert after it.
        // Artifacts follow the pattern "    - name: <name>\n      safeName: <safeName>"
        var lastArtifactMatch = LastArtifactPattern().Match(existingYaml);
        if (lastArtifactMatch.Success)
        {
            var insertPosition = lastArtifactMatch.Index + lastArtifactMatch.Length;
            return existingYaml.Insert(insertPosition, Environment.NewLine + artifactEntry);
        }

        // Fallback: look for just "Artifacts:" and append after it
        var artifactsHeaderMatch = Regex.Match(existingYaml, @"Artifacts:\s*\r?\n");
        if (artifactsHeaderMatch.Success)
        {
            var insertPosition = artifactsHeaderMatch.Index + artifactsHeaderMatch.Length;
            return existingYaml.Insert(insertPosition, artifactEntry + Environment.NewLine);
        }

        return null;
    }

    public bool HasArtifact(string yamlContent, string packageName)
    {
        // Match "- name: <packageName>" with flexible whitespace
        return Regex.IsMatch(yamlContent, $@"-\s+name:\s+{Regex.Escape(packageName)}\s*$", RegexOptions.Multiline);
    }

    public string GenerateSafeName(string packageName)
    {
        return packageName.Replace(".", "");
    }

    public string? FindCiYamlPath(string repoRoot, string serviceDirectory)
    {
        var ciPath = Path.Combine(repoRoot, "sdk", serviceDirectory, "ci.yml");
        return File.Exists(ciPath) ? ciPath : null;
    }

    // Matches the last artifact block: "    - name: Foo\n      safeName: Bar" (with optional extra fields)
    [GeneratedRegex(@"-\s+name:\s+\S+[^\S\r\n]*(?:\r?\n[^\S\r\n]+(?!-\s+name:)\S[^\r\n]*)*", RegexOptions.Multiline | RegexOptions.RightToLeft)]
    private static partial Regex LastArtifactPattern();
}
