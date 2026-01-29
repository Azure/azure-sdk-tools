using Sdk.Tools.Cli.Models;

namespace Sdk.Tools.Cli.Services.Languages;

public abstract class LanguageService
{
    public abstract SdkLanguage Language { get; }
    public abstract string FileExtension { get; }
    public abstract string[] DefaultSourceDirectories { get; }
    public abstract string[] DefaultIncludePatterns { get; }
    public abstract string[] DefaultExcludePatterns { get; }
    
    public virtual Models.SourceInput GetDefaultSourceInput()
    {
        return new Models.SourceInput(
            string.Join(",", DefaultIncludePatterns),
            string.Join(",", DefaultExcludePatterns),
            10
        );
    }
}
