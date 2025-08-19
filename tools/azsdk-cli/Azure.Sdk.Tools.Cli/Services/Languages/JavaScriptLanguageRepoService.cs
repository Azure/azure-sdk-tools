using System.Diagnostics;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// JavaScript/Node.js-specific implementation of language repository service.
/// Uses tools like npm, prettier, eslint, jest, etc. for JavaScript development workflows.
/// </summary>
public class JavaScriptLanguageRepoService : LanguageRepoService
{
    public JavaScriptLanguageRepoService(IProcessHelper processHelper, IGitHelper gitHelper) 
        : base(processHelper, gitHelper)
    {
    }

    public override string Language => "javascript";
}
