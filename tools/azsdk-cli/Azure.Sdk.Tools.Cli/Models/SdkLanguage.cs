using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SdkLanguage
{
    Unknown,
    [JsonStringEnumMemberName(".NET")]
    DotNet,
    [JsonStringEnumMemberName("Java")]
    Java,
    [JsonStringEnumMemberName("JavaScript")]
    JavaScript,
    [JsonStringEnumMemberName("Python")]
    Python,
    [JsonStringEnumMemberName("Go")]
    Go,
    [JsonStringEnumMemberName("Rust")]
    Rust
}

public static class SdkLanguageHelpers
{

    public static string ToWorkItemString(this SdkLanguage value)
    {
        var field = value.GetType().GetField(value.ToString())
            ?? throw new InvalidOperationException($"Unable to find JsonStringEnumMemberName field for SdkLanguage value '{value}'");

        var attribute = field.GetCustomAttributes(typeof(JsonStringEnumMemberNameAttribute), false)
                             .FirstOrDefault() as JsonStringEnumMemberNameAttribute;
        return attribute?.Name ?? value.ToString();
    }

    private static readonly ImmutableDictionary<string, SdkLanguage> RepoToLanguageMap = new Dictionary<string, SdkLanguage>()
    {
        { "azure-sdk-for-net", SdkLanguage.DotNet },
        { "azure-sdk-for-go", SdkLanguage.Go },
        { "azure-sdk-for-java", SdkLanguage.Java },
        { "azure-sdk-for-js", SdkLanguage.JavaScript },
        { "azure-sdk-for-python", SdkLanguage.Python },
        { "azure-sdk-for-rust", SdkLanguage.Rust}
    }.ToImmutableDictionary();

    public static async Task<SdkLanguage> GetLanguageForRepoPathAsync(IGitHelper gitHelper, string pathInRepo, CancellationToken ct = default)
    {
        string repoName = await gitHelper.GetRepoNameAsync(pathInRepo, ct);
        return GetLanguageForRepo(repoName);
    }

    public static SdkLanguage GetLanguageForRepo(string repoName)
    {
        if (string.IsNullOrEmpty(repoName))
        {
            return SdkLanguage.Unknown;
        }
        if (repoName.EndsWith("-pr"))
        {
            // Our private repos end with "-pr" so strip that off for the context of determining the language.
            repoName = repoName.Substring(0, repoName.Length - "-pr".Length);
        }
        if (RepoToLanguageMap.TryGetValue(repoName.ToLower(), out SdkLanguage language))
        {
            return language;
        }
        return SdkLanguage.Unknown;
    }

    public static SdkLanguage GetSdkLanguage(string language)
    {
        switch (language.ToLower())
        {
            case ".net":
            case "dotnet":
            case "c#":
            case "csharp":
                return SdkLanguage.DotNet;
            case "java":
                return SdkLanguage.Java;
            case "javascript":
            case "js":
            case "typescript":
                return SdkLanguage.JavaScript;
            case "python":
                return SdkLanguage.Python;
            case "go":
                return SdkLanguage.Go;
            case "rust":
                return SdkLanguage.Rust;
            default:
                return SdkLanguage.Unknown;
        }
    }
}
