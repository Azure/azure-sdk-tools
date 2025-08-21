using System.Diagnostics;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Helpers;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// JavaScript/Node.js-specific implementation of language repository service.
/// Uses tools like npm, prettier, eslint, jest, etc. for JavaScript development workflows.
/// </summary>
public class JavaScriptLanguageRepoService : LanguageRepoService
{
    public JavaScriptLanguageRepoService(IProcessHelper processHelper, INpxHelper npxHelper, IGitHelper gitHelper, ILogger<JavaScriptLanguageRepoService> logger) 
        : base(processHelper, npxHelper, gitHelper, logger)
    {
    }
}
