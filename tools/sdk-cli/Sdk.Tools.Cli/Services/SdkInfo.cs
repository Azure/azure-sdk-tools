// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using Sdk.Tools.Cli.Models;

namespace Sdk.Tools.Cli.Services;

/// <summary>
/// Unified SDK detection: language, source folder, and samples folder in one scan.
/// This is the single source of truth for SDK structure detection.
/// </summary>
public class SdkInfo
{
    private static readonly ConcurrentDictionary<string, Lazy<SdkInfo>> _cache = new(StringComparer.OrdinalIgnoreCase);
    
    // Static enumeration options - reuse to avoid allocations
    private static readonly EnumerationOptions ShallowOptions = new()
    {
        RecurseSubdirectories = false,
        IgnoreInaccessible = true
    };
    
    private static readonly EnumerationOptions DeepOptions = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        MaxRecursionDepth = 5
    };
    
    private static readonly EnumerationOptions MediumOptions = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        MaxRecursionDepth = 3
    };

    // Samples folder candidates in priority order
    private static readonly string[] SamplesFolderPatterns =
    [
        "samples",
        "examples", 
        "sample",
        "example",
        "demo",
        "demos",
        "quickstarts",
        "tests/samples",
        "docs/samples"
    ];
    
    // Language-specific patterns - order matters (more specific first)
    private static readonly LanguagePattern[] LanguagePatterns =
    [
        // .NET
        new(SdkLanguage.DotNet, "dotnet", ".cs",
            new[] { "*.csproj", "*.sln" }, 
            new[] { "src", "lib", "source" }),
        
        // Python
        new(SdkLanguage.Python, "python", ".py",
            new[] { "setup.py", "pyproject.toml" },
            new[] { "src", "azure", "sdk", "." }),
        
        // Java
        new(SdkLanguage.Java, "java", ".java",
            new[] { "pom.xml", "build.gradle", "build.gradle.kts" },
            new[] { "src/main/java", "src" }),
        
        // TypeScript (before JavaScript - has tsconfig.json)
        new(SdkLanguage.TypeScript, "typescript", ".ts",
            new[] { "tsconfig.json" },
            new[] { "src", "lib" }),
        
        // JavaScript (package.json but typically no tsconfig)
        new(SdkLanguage.JavaScript, "javascript", ".js",
            new[] { "package.json" },
            new[] { "src", "lib" }),
        
        // Go
        new(SdkLanguage.Go, "go", ".go",
            new[] { "go.mod" },
            new[] { "pkg", "internal", "cmd", "." })
    ];
    
    // Folders to exclude from source detection
    private static readonly HashSet<string> ExcludedFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", "node_modules", "dist", "build", "target", 
        ".git", ".vs", ".idea", "__pycache__", ".venv", "venv",
        "vendor", "packages", "artifacts", ".nuget"
    };

    /// <summary>Root path of the SDK.</summary>
    public string RootPath { get; }
    
    /// <summary>Detected language enum, or null if unknown.</summary>
    public SdkLanguage? Language { get; }
    
    /// <summary>Language name (e.g., "dotnet", "python").</summary>
    public string? LanguageName { get; }
    
    /// <summary>Primary file extension for this language.</summary>
    public string? FileExtension { get; }
    
    /// <summary>Path to the source code folder.</summary>
    public string SourceFolder { get; }
    
    /// <summary>Path to existing samples folder, if found.</summary>
    public string? SamplesFolder { get; }
    
    /// <summary>Suggested path for samples folder (existing or default).</summary>
    public string SuggestedSamplesFolder { get; }
    
    /// <summary>All detected samples folder candidates.</summary>
    public IReadOnlyList<string> AllSamplesCandidates { get; }
    
    /// <summary>Whether the SDK was successfully detected.</summary>
    public bool IsValid => Language != null || SourceFolder != RootPath;

    private SdkInfo(
        string rootPath,
        SdkLanguage? language,
        string? languageName,
        string? fileExtension,
        string sourceFolder,
        string? samplesFolder,
        List<string> allSamplesCandidates)
    {
        RootPath = rootPath;
        Language = language;
        LanguageName = languageName;
        FileExtension = fileExtension;
        SourceFolder = sourceFolder;
        SamplesFolder = samplesFolder;
        SuggestedSamplesFolder = samplesFolder ?? Path.Combine(rootPath, "examples");
        AllSamplesCandidates = allSamplesCandidates.AsReadOnly();
    }

    /// <summary>
    /// Scans the SDK root and returns detection results.
    /// Results are cached by path.
    /// </summary>
    public static SdkInfo Scan(string sdkRoot)
    {
        sdkRoot = Path.GetFullPath(sdkRoot);
        return _cache.GetOrAdd(sdkRoot, path => new Lazy<SdkInfo>(() => ScanInternal(path))).Value;
    }
    
    /// <summary>
    /// Clears the detection cache.
    /// </summary>
    public static void ClearCache() => _cache.Clear();
    
    /// <summary>
    /// Detects just the language without full folder scanning.
    /// Faster than full Scan() when you only need the language.
    /// </summary>
    public static SdkLanguage? DetectLanguage(string sdkRoot)
    {
        if (!Directory.Exists(sdkRoot))
            return null;
        
        sdkRoot = Path.GetFullPath(sdkRoot);
        
        // Check cache first
        if (_cache.TryGetValue(sdkRoot, out var cached))
            return cached.Value.Language;
        
        // Quick detection without full scan
        foreach (var pattern in LanguagePatterns)
        {
            if (HasBuildMarker(sdkRoot, pattern.BuildFilePatterns))
            {
                // Special case: TypeScript vs JavaScript
                if (pattern.LanguageEnum == SdkLanguage.JavaScript &&
                    File.Exists(Path.Combine(sdkRoot, "tsconfig.json")))
                {
                    return SdkLanguage.TypeScript;
                }
                
                return pattern.LanguageEnum != SdkLanguage.Unknown ? pattern.LanguageEnum : null;
            }
        }
        
        return null;
    }

    private static SdkInfo ScanInternal(string root)
    {
        // Detect language and source folder
        var (sourceFolder, languageEnum, languageName, fileExt) = DetectSourceFolder(root);
        
        // Detect samples folders
        var (samplesFolder, allCandidates) = DetectSamplesFolder(root);
        
        return new SdkInfo(
            rootPath: root,
            language: languageEnum,
            languageName: languageName,
            fileExtension: fileExt,
            sourceFolder: sourceFolder,
            samplesFolder: samplesFolder,
            allSamplesCandidates: allCandidates
        );
    }
    
    private static (string SourceFolder, SdkLanguage? Language, string? LanguageName, string? FileExt) DetectSourceFolder(string root)
    {
        // Try each language pattern
        foreach (var pattern in LanguagePatterns)
        {
            if (!HasBuildMarker(root, pattern.BuildFilePatterns))
                continue;
            
            // Special case: distinguish TypeScript from JavaScript
            var actualPattern = pattern;
            if (pattern.LanguageEnum == SdkLanguage.JavaScript &&
                File.Exists(Path.Combine(root, "tsconfig.json")))
            {
                actualPattern = LanguagePatterns.First(p => p.LanguageEnum == SdkLanguage.TypeScript);
            }
            
            // Find best source folder
            foreach (var srcPattern in actualPattern.SourceFolderPatterns)
            {
                var candidate = srcPattern == "." ? root : Path.Combine(root, srcPattern);
                if (!Directory.Exists(candidate))
                    continue;
                
                if (HasSourceFiles(candidate, actualPattern.FileExtension))
                {
                    var langEnum = actualPattern.LanguageEnum != SdkLanguage.Unknown 
                        ? actualPattern.LanguageEnum 
                        : (SdkLanguage?)null;
                    return (candidate, langEnum, actualPattern.Name, actualPattern.FileExtension);
                }
            }
        }
        
        // Fallback: look for any common source folder
        var fallbackFolders = new[] { "src", "lib", "source", "sdk", "pkg" };
        foreach (var folder in fallbackFolders)
        {
            var candidate = Path.Combine(root, folder);
            if (Directory.Exists(candidate) && HasAnySourceFiles(candidate))
            {
                return (candidate, null, null, null);
            }
        }
        
        // Last resort: use root if it has source files
        if (HasSourceFilesShallow(root))
        {
            return (root, null, null, null);
        }
        
        return (root, null, null, null);
    }
    
    private static (string? SamplesFolder, List<string> AllCandidates) DetectSamplesFolder(string root)
    {
        List<(string Path, int Score)> candidates = [];
        
        foreach (var pattern in SamplesFolderPatterns)
        {
            var candidate = Path.Combine(root, pattern);
            if (!Directory.Exists(candidate))
                continue;
            
            var fileCount = CountFilesQuick(candidate);
            if (fileCount > 0)
            {
                candidates.Add((candidate, fileCount));
            }
        }
        
        // Sort by score descending
        candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
        
        var allPaths = candidates.ConvertAll(c => c.Path);
        var bestMatch = candidates.Count > 0 ? candidates[0].Path : null;
        
        return (bestMatch, allPaths);
    }
    
    private static bool HasBuildMarker(string root, string[] patterns)
    {
        foreach (var pattern in patterns)
        {
            // Check if pattern contains wildcards
            if (pattern.Contains('*'))
            {
                // Use glob matching for wildcard patterns
                try
                {
                    // Check root
                    if (Directory.EnumerateFiles(root, pattern).Any())
                        return true;
                    
                    // Check immediate subdirectories
                    foreach (var dir in Directory.EnumerateDirectories(root))
                    {
                        if (ExcludedFolders.Contains(Path.GetFileName(dir)))
                            continue;
                        
                        if (Directory.EnumerateFiles(dir, pattern).Any())
                            return true;
                    }
                }
                catch (UnauthorizedAccessException) { }
            }
            else
            {
                // Exact file name - use File.Exists for efficiency
                if (File.Exists(Path.Combine(root, pattern)))
                    return true;
                
                // Check immediate subdirectories
                try
                {
                    foreach (var dir in Directory.EnumerateDirectories(root))
                    {
                        if (ExcludedFolders.Contains(Path.GetFileName(dir)))
                            continue;
                        
                        if (File.Exists(Path.Combine(dir, pattern)))
                            return true;
                    }
                }
                catch (UnauthorizedAccessException) { }
            }
        }
        return false;
    }
    
    private static bool HasSourceFiles(string folder, string extension)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(folder, "*" + extension, DeepOptions))
            {
                return true;
            }
        }
        catch (UnauthorizedAccessException) { }
        
        return false;
    }
    
    private static bool HasAnySourceFiles(string folder)
    {
        var commonExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".cs", ".py", ".java", ".ts", ".js", ".go"
        };
        
        try
        {
            foreach (var file in Directory.EnumerateFiles(folder, "*.*", MediumOptions))
            {
                if (commonExtensions.Contains(Path.GetExtension(file)))
                    return true;
            }
        }
        catch (UnauthorizedAccessException) { }
        
        return false;
    }
    
    private static bool HasSourceFilesShallow(string folder)
    {
        var commonExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".cs", ".py", ".java", ".ts", ".js", ".go"
        };
        
        try
        {
            foreach (var file in Directory.EnumerateFiles(folder))
            {
                if (commonExtensions.Contains(Path.GetExtension(file)))
                    return true;
            }
        }
        catch (UnauthorizedAccessException) { }
        
        return false;
    }
    
    private static int CountFilesQuick(string folder)
    {
        try
        {
            var count = 0;
            foreach (var _ in Directory.EnumerateFiles(folder, "*.*", MediumOptions))
            {
                if (++count >= 10)
                    break;
            }
            return count;
        }
        catch (UnauthorizedAccessException)
        {
            return 0;
        }
    }
    
    private record LanguagePattern(
        SdkLanguage LanguageEnum,
        string Name,
        string FileExtension,
        string[] BuildFilePatterns,
        string[] SourceFolderPatterns
    );
}
