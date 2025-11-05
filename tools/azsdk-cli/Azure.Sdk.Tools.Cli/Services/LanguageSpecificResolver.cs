using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services;

public class LanguageSpecificResolver<T>(
    IGitHelper _gitHelper,
    IPowershellHelper _powershellHelper,
    ILogger<LanguageSpecificResolver<T>> _logger,
    [FromKeyedServices(SdkLanguage.DotNet)]
    T? dotnetService = default,
    [FromKeyedServices(SdkLanguage.Java)]
    T? javaService = default,
    [FromKeyedServices(SdkLanguage.Python)]
    T? pythonService = default,
    [FromKeyedServices(SdkLanguage.JavaScript)]
    T? javaScriptService = default,
    [FromKeyedServices(SdkLanguage.Go)]
    T? goService = default
    // If adding languages in future, add a corresponding entry here.
) : ILanguageSpecificResolver<T> where T : class
{
    public async Task<T?> Resolve(string packagePath, CancellationToken ct = default)
    {
        var language = DetectLanguage(packagePath, ct);
        return await Resolve(language, ct);
    }

    public Task<T?> Resolve(SdkLanguage language, CancellationToken ct = default)
    {
        var service = language switch
        {
            SdkLanguage.DotNet => dotnetService,
            SdkLanguage.Java => javaService,
            SdkLanguage.Python => pythonService,
            SdkLanguage.JavaScript => javaScriptService,
            SdkLanguage.Go => goService,
            // If adding languages in future, add a corresponding entry here.
            _ => default,
        };
        return Task.FromResult(service);
    }

    private SdkLanguage DetectLanguage(string pathInRepo, CancellationToken ct)
    {
        return SdkLanguageExtensions.GetLanguageForRepo(_gitHelper, pathInRepo);
    }
}
