using Sdk.Tools.Cli.Models;

namespace Sdk.Tools.Cli.Services.Languages;

public class JavaLanguageService : LanguageService
{
    public override SdkLanguage Language => SdkLanguage.Java;
    public override string FileExtension => ".java";
    public override string[] DefaultSourceDirectories => new[] { "src/main/java", "src" };
    public override string[] DefaultIncludePatterns => new[] { "**/*.java" };
    public override string[] DefaultExcludePatterns => new[] 
    { 
        "**/target/**", 
        "**/build/**", 
        "**/test/**",
        "**/*Test.java"
    };
}
