using System.Diagnostics;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Helpers;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// .NET/C#-specific implementation of language repository service.
/// Uses tools like dotnet build, dotnet test, dotnet format, etc. for .NET development workflows.
/// </summary>
public class DotNetLanguageRepoService : LanguageRepoService
{
    public DotNetLanguageRepoService(IProcessHelper processHelper, IGitHelper gitHelper, ILogger<DotNetLanguageRepoService> logger) 
        : base(processHelper, gitHelper, logger)
    {
    }
}
