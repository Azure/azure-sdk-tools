// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.InteropServices;

namespace Azure.Sdk.Tools.Cli.Models;

/// <summary>
/// Context information passed to setup verification requirements for generating context-aware instructions.
/// </summary>
public class RequirementContext
{
    /// <summary>
    /// The root directory of the repository.
    /// </summary>
    public required string RepoRoot { get; init; }

    /// <summary>
    /// The name of the repository.
    /// </summary>
    public required string RepoName { get; init; }

    /// <summary>
    /// The package path provided by the user, or null if not specified.
    /// </summary>
    public string? PackagePath { get; init; }

    /// <summary>
    /// The current operating system platform.
    /// </summary>
    public OSPlatform Platform { get; init; }

    /// <summary>
    /// The set of SDK languages to check requirements for.
    /// </summary>
    public IReadOnlySet<SdkLanguage> Languages { get; init; } = new HashSet<SdkLanguage>();

    /// <summary>
    /// Names of requirements that have failed their check. Used to skip dependent requirements early.
    /// Populated during the verification process.
    /// </summary>
    public HashSet<string> FailedRequirements { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// True if running on Windows.
    /// </summary>
    public bool IsWindows => Platform == OSPlatform.Windows;

    /// <summary>
    /// True if running on macOS.
    /// </summary>
    public bool IsMacOS => Platform == OSPlatform.OSX;

    /// <summary>
    /// True if running on Linux.
    /// </summary>
    public bool IsLinux => Platform == OSPlatform.Linux;

    /// <summary>
    /// Determines if the current repository is the azure-rest-api-specs repository.
    /// </summary>
    public bool IsSpecsRepo()
    {
        return RepoName.Equals("azure-rest-api-specs", StringComparison.OrdinalIgnoreCase) || 
               RepoName.Equals("azure-rest-api-specs-pr", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Creates a RequirementContext for the current environment.
    /// </summary>
    public static RequirementContext Create(string repoRoot, string repoName, string? packagePath, HashSet<SdkLanguage>? languages = null)
    {
        return new RequirementContext
        {
            RepoRoot = repoRoot,
            RepoName = repoName,
            PackagePath = packagePath,
            Platform = GetCurrentPlatform(),
            Languages = languages ?? new HashSet<SdkLanguage>()
        };
    }

    private static OSPlatform GetCurrentPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) 
        {
            return OSPlatform.Windows;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) 
        {
            return OSPlatform.OSX;
        }
        return OSPlatform.Linux;
    }
}
