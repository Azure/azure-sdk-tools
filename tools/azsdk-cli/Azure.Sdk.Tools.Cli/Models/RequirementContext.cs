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
    /// The root directory of the repository, or null if not in a repo.
    /// </summary>
    public string? RepoRoot { get; init; }

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
    /// The name of the repository (folder name), or null if not in a repo.
    /// </summary>
    public string? RepoName => RepoRoot != null ? Path.GetFileName(RepoRoot) : null;

    /// <summary>
    /// Creates a RequirementContext for the current environment.
    /// </summary>
    public static RequirementContext Create(string? repoRoot, string? packagePath, HashSet<SdkLanguage>? languages = null)
    {
        return new RequirementContext
        {
            RepoRoot = repoRoot,
            PackagePath = packagePath,
            Platform = GetCurrentPlatform(),
            Languages = languages ?? new HashSet<SdkLanguage>()
        };
    }

    private static OSPlatform GetCurrentPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            return OSPlatform.Windows;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
            return OSPlatform.OSX;
        }
        return OSPlatform.Linux;
    }
}
