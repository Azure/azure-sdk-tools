// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services.Repair;

/// <summary>
/// Defines the source paths a bounded customization repair may modify.
/// Discovery does not follow links, and checks are repeated by mutation tools before writing.
/// </summary>
public class CustomizationFilePolicy(SdkLanguage language, string packagePath)
{
    private readonly string _packagePath = Path.GetFullPath(packagePath);
    private static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".github", ".artifacts", "eng", "node_modules", "bin", "obj", "dist",
        "packages", "TestResults", "__pycache__", ".tox", ".venv", "venv", "target",
        "tests", "test", "samples", "perf", "stress"
    };
    private static readonly HashSet<string> JavaScriptExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ts", ".tsx", ".mts", ".cts", ".js", ".jsx", ".mjs", ".cjs"
    };
    private static readonly HashSet<string> MetadataFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "AssemblyInfo.cs", "GlobalAssemblyInfo.cs", "SharedAssemblyInfo.cs", "AssemblyVersion.cs",
        "AssemblyVersions.cs", "AssemblyVersionInfo.cs", "VersionInfo.cs", "package-info.java", "module-info.java"
    };
    private static readonly HashSet<string> MetadataFileStems = new(StringComparer.OrdinalIgnoreCase)
    {
        "_version", "__version__", "sdk-version", "sdkVersion", "package-version", "packageVersion",
        "package-metadata", "packageMetadata"
    };
    private static readonly Regex SourceMetadata = new(
        @"\[\s*assembly\s*:|\b(?:SDK_VERSION|PACKAGE_VERSION|[Ss]dkVersion|[Pp]ackageVersion|__version__)\s*(?::\s*string\s*)?=",
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(500));

    public bool IsCustomFile(string fullPath)
    {
        if (!TryGetSegments(fullPath, out var segments) ||
            segments.Any(s => s.Equals("generated", StringComparison.OrdinalIgnoreCase) ||
                              s.Equals("_generated", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var fileName = segments[^1];
        var stem = Path.GetFileNameWithoutExtension(fileName);
        if (MetadataFileNames.Contains(fileName) || MetadataFileStems.Contains(stem) ||
            (stem.Equals("version", StringComparison.OrdinalIgnoreCase) &&
             (segments.Length == 2 || segments[^2].Equals("Properties", StringComparison.OrdinalIgnoreCase) ||
              segments[^2].Equals("utils", StringComparison.OrdinalIgnoreCase) ||
              segments[^2].Equals("metadata", StringComparison.OrdinalIgnoreCase))))
        {
            return false;
        }

        var extension = Path.GetExtension(fileName);
        var isLanguageFile = language switch
        {
            SdkLanguage.DotNet => StartsWith(segments, "src") && extension.Equals(".cs", StringComparison.OrdinalIgnoreCase),
            SdkLanguage.Java => StartsWith(segments, "customization", "src", "main", "java") && extension.Equals(".java", StringComparison.OrdinalIgnoreCase),
            SdkLanguage.JavaScript => StartsWith(segments, "src") && JavaScriptExtensions.Contains(extension),
            SdkLanguage.Python => segments[^1].Equals("_patch.py", StringComparison.Ordinal),
            _ => false
        };
        return isLanguageFile && (!File.Exists(fullPath) || IsCustomizationContentAllowed(File.ReadAllText(fullPath)));
    }

    // Mixed source/metadata files are deliberately read-only in bounded repair, regardless of filename.
    internal static bool IsCustomizationContentAllowed(string content) => !SourceMetadata.IsMatch(content);

    public bool IsGeneratedFile(string fullPath)
    {
        if (!TryGetSegments(fullPath, out var segments))
        {
            return false;
        }

        var extension = Path.GetExtension(segments[^1]);
        return language switch
        {
            SdkLanguage.DotNet => StartsWith(segments, "src", "Generated") && extension.Equals(".cs", StringComparison.OrdinalIgnoreCase),
            SdkLanguage.Java => StartsWith(segments, "src", "main", "java") && extension.Equals(".java", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    public IReadOnlyList<string> GetCustomizationFiles()
    {
        if (!Directory.Exists(_packagePath) || !ToolHelpers.IsPathWithinDirectoryWithoutLinks(_packagePath, _packagePath))
        {
            return [];
        }

        return Directory.EnumerateFiles(_packagePath, "*", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                AttributesToSkip = FileAttributes.ReparsePoint,
                IgnoreInaccessible = false
            })
            .Where(IsCustomFile)
            .Where(f => language != SdkLanguage.Python || HasNonEmptyPythonExports(f))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToArray();
    }

    internal static bool HasNonEmptyPythonExports(string patchFilePath)
    {
        foreach (var line in File.ReadLines(patchFilePath))
        {
            if (line.Contains("__all__") && line.Contains('='))
            {
                if (line.Contains('"') || line.Contains('\''))
                {
                    return true;
                }

                var valueAfterEquals = line[(line.LastIndexOf('=') + 1)..].Trim();
                return valueAfterEquals.StartsWith('[') && !valueAfterEquals.StartsWith("[]");
            }
        }

        return false;
    }

    private bool TryGetSegments(string fullPath, out string[] segments)
    {
        segments = [];
        if (!ToolHelpers.IsPathWithinDirectoryWithoutLinks(_packagePath, fullPath))
        {
            return false;
        }

        segments = Path.GetRelativePath(_packagePath, Path.GetFullPath(fullPath))
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Length > 0 && segments[^1] != "." &&
               !segments.Any(s => s.StartsWith('.') || ExcludedDirectories.Contains(s));
    }

    private static bool StartsWith(string[] segments, params string[] prefix) =>
        segments.Length > prefix.Length &&
        prefix.Select((part, i) => part.Equals(segments[i], StringComparison.OrdinalIgnoreCase)).All(matches => matches);
}
