using Sdk.Tools.Cli.Models;

namespace Sdk.Tools.Cli.Services;

/// <summary>
/// Scans for SDK source folders, samples folders, and detects language.
/// This is a thin wrapper around SdkInfo for backward compatibility.
/// Consider using SdkInfo directly for new code.
/// </summary>
public class SamplesFolderScanner
{
    /// <summary>
    /// Scans the SDK root to find source and samples folders.
    /// </summary>
    public ScanResult Scan(string sdkRoot)
    {
        var info = SdkInfo.Scan(sdkRoot);
        
        return new ScanResult(
            SourceFolder: info.SourceFolder,
            ExistingSamplesFolder: info.SamplesFolder,
            SuggestedSamplesFolder: info.SuggestedSamplesFolder,
            AllSamplesCandidates: info.AllSamplesCandidates.ToList(),
            DetectedLanguage: info.LanguageName,
            Language: info.Language
        );
    }
    
    /// <summary>
    /// Detects just the SDK language without full folder scanning.
    /// </summary>
    public SdkLanguage? DetectLanguage(string sdkRoot) => SdkInfo.DetectLanguage(sdkRoot);
}

/// <summary>
/// Result of SDK scanning.
/// </summary>
public record ScanResult(
    string SourceFolder,
    string? ExistingSamplesFolder,
    string SuggestedSamplesFolder,
    IReadOnlyList<string> AllSamplesCandidates,
    string? DetectedLanguage,
    SdkLanguage? Language
);
