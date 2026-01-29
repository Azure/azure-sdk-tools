using Sdk.Tools.Cli.Models;

namespace Sdk.Tools.Cli.Services.Languages;

public class GoLanguageService : LanguageService
{
    public override SdkLanguage Language => SdkLanguage.Go;
    public override string FileExtension => ".go";
    public override string[] DefaultSourceDirectories => new[] { "." };
    public override string[] DefaultIncludePatterns => new[] { "**/*.go" };
    public override string[] DefaultExcludePatterns => new[] 
    { 
        "**/vendor/**", 
        "**/*_test.go"
    };
}
